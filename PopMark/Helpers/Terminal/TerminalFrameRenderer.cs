using PopMark.Models;
using System.Text;

namespace PopMark.Helpers.Terminal;

internal static class TerminalFrameRenderer
{
    private static List<string>? _lastRenderedLines;
    private static int _lastRenderedWidth;
    private static int _lastRenderedHeight;

    public static void DrawCommandCenter(PlayerSnapshot snapshot, string notice, bool showHelp = false)
    {
        var (width, height) = TerminalHost.GetWindowSize();
        Render(new RenderContext(
            width,
            height,
            snapshot,
            notice,
            Input: string.Empty,
            ShowHelp: showHelp,
            MiniMode: false,
            AnimationFrame: 0),
            forceFullPaint: true);
    }

    public static void DrawMiniPlayer(PlayerSnapshot snapshot, string notice)
    {
        var (width, height) = TerminalHost.GetWindowSize();
        Render(new RenderContext(
            width,
            height,
            snapshot,
            notice,
            Input: string.Empty,
            ShowHelp: false,
            MiniMode: true,
            AnimationFrame: 0),
            forceFullPaint: true);
    }

    public static void Render(RenderContext context, bool forceFullPaint = false)
    {
        var width = context.Width > 0 ? context.Width : 80;
        var height = context.Height > 0 ? context.Height : 24;
        var lines = BuildTerminalFrame(context with { Width = width, Height = height });

        while (lines.Count < height)
            lines.Add(string.Empty);

        if (lines.Count > height)
            lines.RemoveRange(height, lines.Count - height);

        for (var i = 0; i < lines.Count; i++)
            lines[i] = TerminalText.TrimAnsiAware(lines[i], width);

        if (Console.IsOutputRedirected)
        {
            Console.Write(string.Join(Environment.NewLine, lines.Select(line => TerminalText.PadAnsiAware(line, width))));
            _lastRenderedLines = [.. lines];
            _lastRenderedWidth = width;
            _lastRenderedHeight = height;
            return;
        }

        var requiresFullPaint = forceFullPaint ||
                                _lastRenderedLines is null ||
                                _lastRenderedWidth != width ||
                                _lastRenderedHeight != height;
        var output = new StringBuilder();
        output.Append("\u001b[?25l\u001b[?7l");
        output.Append(requiresFullPaint ? "\u001b[H\u001b[2J" : "\u001b[H");

        for (var i = 0; i < lines.Count; i++)
        {
            var changed = requiresFullPaint ||
                          i >= _lastRenderedLines!.Count ||
                          !string.Equals(lines[i], _lastRenderedLines[i], StringComparison.Ordinal);
            if (changed)
                output.Append(TerminalText.PadAnsiAware(lines[i], width));

            if (i < lines.Count - 1)
                output.Append("\u001b[1E");
        }

        output.Append("\u001b[?7h");
        Console.Write(output.ToString());
        _lastRenderedLines = [.. lines];
        _lastRenderedWidth = width;
        _lastRenderedHeight = height;
    }

    public static void ResetFrameCache()
    {
        _lastRenderedLines = null;
        _lastRenderedWidth = 0;
        _lastRenderedHeight = 0;
    }

    private static List<string> BuildTerminalFrame(RenderContext context) =>
        context.MiniMode
            ? BuildMiniFrame(context)
            : BuildCommandFrame(context);

    private static List<string> BuildCommandFrame(RenderContext context)
    {
        var width = Math.Max(24, context.Width);
        var height = Math.Max(1, context.Height);
        var prompt = PromptComponent(context.Input, width);
        var available = Math.Max(0, height - 1);
        var lines = new List<string>();
        if (available == 0)
            return [prompt];

        var footer = FooterComponent(width, context.ShowHelp);
        var playbackHeight = available >= 9 ? 5 : available >= 8 ? 4 : available >= 5 ? 3 : 0;
        var footerHeight = Math.Min(footer.Count, Math.Max(0, available - 1 - playbackHeight));
        var mainBudget = Math.Max(0, available - 1 - playbackHeight - footerHeight);

        lines.Add(HeaderComponent(context, width));

        if (context.Snapshot.Current is null && context.Snapshot.Pending.Count == 0)
        {
            lines.AddRange(EmptyStateComponent(width, mainBudget));
        }
        else if (mainBudget > 0)
        {
            var nowHeight = mainBudget <= 5
                ? mainBudget
                : Math.Min(7, Math.Max(4, mainBudget / 2));
            var queueHeight = Math.Max(0, mainBudget - nowHeight);
            lines.AddRange(NowPlayingComponent(context, width, nowHeight));
            lines.AddRange(QueueComponent(context.Snapshot, width, queueHeight));
        }

        if (playbackHeight > 0)
            lines.AddRange(PlaybackStripComponent(context, width, playbackHeight));

        if (footerHeight > 0)
            lines.AddRange(footer.Take(footerHeight));

        while (lines.Count < available)
            lines.Add(string.Empty);

        if (lines.Count > available)
            lines.RemoveRange(available, lines.Count - available);

        lines.Add(prompt);
        return lines;
    }

    private static List<string> BuildMiniFrame(RenderContext context)
    {
        var width = Math.Max(24, context.Width);
        var height = Math.Max(1, context.Height);
        var prompt = PromptComponent(context.Input, width);
        var available = Math.Max(0, height - 1);
        if (available == 0)
            return [prompt];

        var blockWidth = Math.Min(width, Math.Max(42, Math.Min(72, width - 2)));
        var leftPad = Math.Max(0, (width - blockWidth) / 2);
        var blockHeight = Math.Min(8, available);
        var block = MiniPlayerComponent(context, blockWidth, blockHeight)
            .Select(line => $"{new string(' ', leftPad)}{line}")
            .ToList();
        var lines = new List<string>();
        var topPadding = Math.Max(0, available - block.Count);
        lines.AddRange(Enumerable.Repeat(string.Empty, topPadding));
        lines.AddRange(block);

        while (lines.Count < available)
            lines.Add(string.Empty);

        if (lines.Count > available)
            lines.RemoveRange(0, lines.Count - available);

        lines.Add(prompt);
        return lines;
    }

    private static string HeaderComponent(RenderContext context, int width)
    {
        var queue = QueueCount(context.Snapshot);
        var left = $"{TerminalStyles.Bold}{TerminalStyles.AnsiAccent}PopMark{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}{StatusText(context.Snapshot.Status)}{TerminalStyles.Reset}";
        var right = $"{TerminalStyles.AnsiMuted}Queue{TerminalStyles.Reset} {TerminalStyles.AnsiWhite}{queue}{TerminalStyles.Reset}";
        var gap = Math.Max(1, width - TerminalText.VisibleLength(left) - TerminalText.VisibleLength(right));
        return TerminalText.PadAnsiAware($"{left}{new string(' ', gap)}{right}", width);
    }

    private static IReadOnlyList<string> NowPlayingComponent(RenderContext context, int width, int height)
    {
        if (height <= 0)
            return [];

        var snapshot = context.Snapshot;
        var track = snapshot.Current ?? snapshot.Pending.FirstOrDefault();
        var title = track?.Title ?? "Nothing loaded";
        var source = track?.DisplaySource ?? "add <url>";
        var duration = FormatDuration(track?.Duration);
        var titleLine = NowPlayingTitleLine(title, NowPlayingMascot(snapshot.Status, context.Notice), Math.Max(8, width - 4));
        var rows = new List<string>
        {
            titleLine,
            $"{TerminalStyles.AnsiMuted}{TerminalText.TrimForWidget(source, Math.Max(8, width - 18))}{TerminalStyles.Reset}",
            $"{StatusPill(snapshot.Status)} {TerminalStyles.AnsiMuted}{TerminalText.TrimForWidget(context.Notice, Math.Max(8, width - 18))}{TerminalStyles.Reset}",
            $"{TerminalStyles.AnsiMuted}Elapsed{TerminalStyles.Reset} {TerminalStyles.AnsiWhite}{FormatDuration(snapshot.Elapsed)}{TerminalStyles.Reset} {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}Duration{TerminalStyles.Reset} {TerminalStyles.AnsiWhite}{duration}{TerminalStyles.Reset}"
        };

        return Box("Now Playing", rows, width, height, TerminalStyles.AnsiAccent);
    }

    private static IReadOnlyList<string> QueueComponent(PlayerSnapshot snapshot, int width, int height)
    {
        if (height <= 0)
            return [];

        var rows = new List<string>();
        if (snapshot.Current is not null)
            rows.Add($"{TerminalStyles.AnsiAccent}> {TerminalText.TrimForWidget(snapshot.Current.Title, Math.Max(8, width - 10))}{TerminalStyles.Reset}");

        var availableTracks = Math.Max(0, height - 3 - rows.Count);
        var index = 1;
        foreach (var track in snapshot.Pending.Take(availableTracks))
        {
            var color = index == 1 ? TerminalStyles.AnsiWhite : TerminalStyles.AnsiMuted;
            rows.Add($"{color}{index,2}. {TerminalText.TrimForWidget(track.Title, Math.Max(8, width - 12))}{TerminalStyles.Reset}");
            index++;
        }

        if (snapshot.Pending.Count == 0 && snapshot.Current is not null)
            rows.Add($"{TerminalStyles.AnsiMuted}End of queue{TerminalStyles.Reset}");
        else if (snapshot.Pending.Count > availableTracks)
            rows.Add($"{TerminalStyles.AnsiMuted}+ {snapshot.Pending.Count - availableTracks} more{TerminalStyles.Reset}");

        return Box("Queue", rows, width, height, TerminalStyles.AnsiChrome);
    }

    private static IReadOnlyList<string> PlaybackStripComponent(RenderContext context, int width, int height)
    {
        if (height <= 0)
            return [];

        var contentWidth = Math.Max(8, width - 6);
        var time = $"{FormatDuration(context.Snapshot.Elapsed)} / {FormatDuration(context.Snapshot.Current?.Duration)}";
        var progressWidth = Math.Max(8, contentWidth - TerminalText.VisibleLength(time) - 3);
        var rows = new List<string>
        {
            $"{ProgressBar(ProgressRatio(context.Snapshot), progressWidth)} {TerminalStyles.AnsiMuted}{time}{TerminalStyles.Reset}"
        };

        if (height >= 5)
            rows.Add(string.Empty);

        rows.Add(BrailleWaveVisualizer(context.Snapshot.Status, context.AnimationFrame, contentWidth));

        return Box("Playback", rows, width, height, TerminalStyles.AnsiChrome);
    }

    private static IReadOnlyList<string> FooterComponent(int width, bool showHelp)
    {
        if (!showHelp)
        {
            return
            [
                TerminalText.PadAnsiAware($"{TerminalStyles.AnsiMuted}SPACE{TerminalStyles.Reset} play/pause {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}N{TerminalStyles.Reset} next {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}P +/-30{TerminalStyles.Reset} seek {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}clear playlist{TerminalStyles.Reset} {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}M{TerminalStyles.Reset} mini {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}Q{TerminalStyles.Reset} quit", width)
            ];
        }

        return Box(
            "Commands",
            [
                $"{TerminalStyles.AnsiAccent}add <url>{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}load a YouTube video or playlist{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiAccent}play/pause{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}toggle playback{TerminalStyles.Reset}   {TerminalStyles.AnsiAccent}next{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}skip track{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiAccent}p <seconds>{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}relative seek, for example p 30 or p -30{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiAccent}clear playlist{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}stop playback and empty the queue{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiAccent}mini{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}compact view{TerminalStyles.Reset}   {TerminalStyles.AnsiAccent}cls{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}redraw{TerminalStyles.Reset}   {TerminalStyles.AnsiAccent}q{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}quit{TerminalStyles.Reset}"
            ],
            width,
            7,
            TerminalStyles.AnsiChrome);
    }

    private static IReadOnlyList<string> EmptyStateComponent(int width, int height)
    {
        if (height <= 0)
            return [];

        return Box(
            "Ready",
            [
                $"{TerminalStyles.Bold}{TerminalStyles.AnsiAccent}PopMark{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiMuted}Queue is empty.{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiWhite}add <url>{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}to start playback from YouTube.{TerminalStyles.Reset}"
            ],
            width,
            height,
            TerminalStyles.AnsiAccent);
    }

    private static IReadOnlyList<string> MiniPlayerComponent(RenderContext context, int width, int height)
    {
        if (height <= 0)
            return [];

        var snapshot = context.Snapshot;
        var track = snapshot.Current ?? snapshot.Pending.FirstOrDefault();
        var title = track?.Title ?? "Queue is empty";
        var contentWidth = Math.Max(8, width - 6);
        var time = $"{FormatDuration(snapshot.Elapsed)} / {FormatDuration(track?.Duration)}";
        var rows = new List<string>
        {
            $"{TerminalStyles.Bold}{TerminalStyles.AnsiAccent}PopMark{TerminalStyles.Reset} {StatusPill(snapshot.Status)}",
            $"{TerminalStyles.AnsiWhite}{TerminalText.TrimForWidget(title, contentWidth)}{TerminalStyles.Reset}",
            $"{ProgressBar(ProgressRatio(snapshot), Math.Max(8, contentWidth - TerminalText.VisibleLength(time) - 3))} {TerminalStyles.AnsiMuted}{time}{TerminalStyles.Reset}"
        };

        if (height >= 8)
            rows.Add(string.Empty);

        rows.Add(CompactBarsVisualizer(snapshot.Status, context.AnimationFrame, contentWidth));
        rows.Add($"{TerminalStyles.AnsiMuted}{TerminalText.TrimForWidget(context.Notice, contentWidth)}{TerminalStyles.Reset}");

        return Box("Mini", rows, width, height, TerminalStyles.AnsiAccent);
    }

    private static IReadOnlyList<string> Box(string title, IReadOnlyList<string> rows, int width, int height, string borderStyle)
    {
        width = Math.Max(8, width);
        height = Math.Max(1, height);
        if (height == 1)
            return [TerminalText.PadAnsiAware($"{borderStyle}{TerminalText.TrimForWidget(title, width)}{TerminalStyles.Reset}", width)];

        var innerWidth = width - 2;
        var titleText = $" {TerminalText.TrimForWidget(title, Math.Max(0, innerWidth - 2))} ";
        var topFill = Math.Max(0, innerWidth - TerminalText.VisibleLength(titleText));
        var lines = new List<string>
        {
            $"{borderStyle}┌{titleText}{new string('─', topFill)}┐{TerminalStyles.Reset}"
        };

        var contentHeight = height - 2;
        for (var i = 0; i < contentHeight; i++)
        {
            var content = i < rows.Count ? TerminalText.TrimAnsiAware(rows[i], Math.Max(0, innerWidth - 2)) : string.Empty;
            lines.Add($"{borderStyle}│{TerminalStyles.Reset} {TerminalText.PadAnsiAware(content, Math.Max(0, innerWidth - 2))} {borderStyle}│{TerminalStyles.Reset}");
        }

        lines.Add($"{borderStyle}└{new string('─', innerWidth)}┘{TerminalStyles.Reset}");
        return lines;
    }

    private static string PromptComponent(string input, int width)
    {
        var prefix = $"{TerminalStyles.Bold}{TerminalStyles.AnsiAccent}popmark{TerminalStyles.Reset}{TerminalStyles.AnsiMuted} > {TerminalStyles.Reset}";
        var maxInputWidth = Math.Max(0, width - TerminalText.VisibleLength(prefix) - 1);
        var displayInput = TerminalText.TailForWidth(input, maxInputWidth);
        return TerminalText.PadAnsiAware($"{prefix}{TerminalStyles.AnsiWhite}{displayInput}{TerminalStyles.Reset}{TerminalStyles.AnsiAccent}|{TerminalStyles.Reset}", width);
    }

    private static string StatusText(PlaybackStatus status) =>
        status switch
        {
            PlaybackStatus.Playing => "playing",
            PlaybackStatus.Loading => "loading",
            PlaybackStatus.Paused => "paused",
            PlaybackStatus.Detached => "detached",
            _ => "stopped"
        };

    private static string StatusPill(PlaybackStatus status)
    {
        var (style, label) = status switch
        {
            PlaybackStatus.Playing => (TerminalStyles.AnsiAccent, "PLAYING"),
            PlaybackStatus.Loading => (TerminalStyles.AnsiSecondary, "LOADING"),
            PlaybackStatus.Paused => (TerminalStyles.AnsiAccent, "PAUSED"),
            PlaybackStatus.Detached => (TerminalStyles.AnsiSecondary, "DETACHED"),
            _ => (TerminalStyles.AnsiMuted, "STOPPED")
        };

        return $"{style}{label}{TerminalStyles.Reset}";
    }

    private static string NowPlayingTitleLine(string title, string mascot, int width)
    {
        var mascotWidth = TerminalText.VisibleLength(mascot);
        var titleWidth = Math.Max(1, width - mascotWidth - 1);
        var trimmedTitle = TerminalText.TrimForWidget(title, titleWidth);
        var gap = Math.Max(1, width - TerminalText.VisibleLength(trimmedTitle) - mascotWidth);
        return $"{TerminalStyles.Bold}{TerminalStyles.AnsiWhite}{trimmedTitle}{TerminalStyles.Reset}{new string(' ', gap)}{TerminalStyles.AnsiAccent}{mascot}{TerminalStyles.Reset}";
    }

    private static string NowPlayingMascot(PlaybackStatus status, string notice)
    {
        if (LooksLikeError(notice))
            return "[x_x]";

        return status switch
        {
            PlaybackStatus.Playing => "[^_^]",
            PlaybackStatus.Paused => "[-_-]",
            PlaybackStatus.Loading => "[•_•]",
            _ => "[-_-]"
        };
    }

    private static bool LooksLikeError(string notice) =>
        notice.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
        notice.Contains("error", StringComparison.OrdinalIgnoreCase) ||
        notice.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
        notice.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
        notice.Contains("unexpected", StringComparison.OrdinalIgnoreCase);

    private static string ProgressBar(double ratio, int width)
    {
        width = Math.Max(1, width);
        var filled = (int)Math.Round(width * Math.Clamp(ratio, 0, 1));
        filled = Math.Clamp(filled, 0, width);
        var empty = width - filled;
        return $"{TerminalStyles.AnsiAccent}{new string('█', filled)}{TerminalStyles.AnsiChrome}{new string('░', empty)}{TerminalStyles.Reset}";
    }

    private static string BrailleWaveVisualizer(PlaybackStatus status, int frame, int width)
    {
        width = Math.Max(1, width);
        var style = status switch
        {
            PlaybackStatus.Playing or PlaybackStatus.Loading => TerminalStyles.AnsiAccent,
            PlaybackStatus.Paused => TerminalStyles.AnsiMuted,
            _ => TerminalStyles.AnsiChrome
        };

        if (status == PlaybackStatus.Paused)
            return $"{style}{new string('⣤', width)}{TerminalStyles.Reset}";

        if (status is not (PlaybackStatus.Playing or PlaybackStatus.Loading))
            return $"{style}{new string('⣀', width)}{TerminalStyles.Reset}";

        var wave = new[] { "⣀", "⣄", "⣤", "⣦", "⣶", "⣷", "⣿", "⣷", "⣶", "⣦", "⣤", "⣄" };
        var cells = Enumerable.Range(0, width)
            .Select(i => wave[(frame + (i * 2) + (i % 3)) % wave.Length]);

        return TerminalText.TrimAnsiAware($"{style}{string.Concat(cells)}{TerminalStyles.Reset}", width);
    }

    private static string CompactBarsVisualizer(PlaybackStatus status, int frame, int width)
    {
        width = Math.Max(1, width);
        var barCount = Math.Max(1, width);
        var style = status switch
        {
            PlaybackStatus.Playing or PlaybackStatus.Loading => TerminalStyles.AnsiAccent,
            PlaybackStatus.Paused => TerminalStyles.AnsiMuted,
            _ => TerminalStyles.AnsiChrome
        };

        var bars = Enumerable.Range(0, barCount).Select(i =>
        {
            if (status == PlaybackStatus.Paused)
                return "▄";

            if (status is not (PlaybackStatus.Playing or PlaybackStatus.Loading))
                return "▂";

            var phase = (frame + (i * 3) + ((i % 5) * 2)) % TerminalStyles.VisualizerBars.Length;
            return TerminalStyles.VisualizerBars[phase];
        });

        return TerminalText.TrimAnsiAware($"{style}{string.Concat(bars)}{TerminalStyles.Reset}", width);
    }

    private static int QueueCount(PlayerSnapshot snapshot) =>
        snapshot.Pending.Count + (snapshot.Current is null ? 0 : 1);

    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
            return "--:--";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }

    private static double ProgressRatio(PlayerSnapshot snapshot)
    {
        if (snapshot.Current?.Duration is not { TotalSeconds: > 0 } duration ||
            snapshot.Elapsed is not { } elapsed)
        {
            return 0;
        }

        return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0, 1);
    }
}
