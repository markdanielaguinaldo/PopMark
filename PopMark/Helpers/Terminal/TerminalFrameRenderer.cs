using PopMark.Models;
using System.Text;

namespace PopMark.Helpers.Terminal;

internal static class TerminalFrameRenderer
{
    private static List<string>? _lastRenderedLines;
    private static int _lastRenderedWidth;
    private static int _lastRenderedHeight;
    private static ProgressHitbox? _lastProgressHitbox;
    private static IReadOnlyList<PlaylistHitbox> _lastPlaylistHitboxes = [];

    public static void DrawCommandCenter(PlayerSnapshot snapshot, string notice, bool showHelp = false, int queueScrollOffset = 0, bool showControls = false)
    {
        var (width, height) = TerminalHost.GetWindowSize();
        Render(new RenderContext(
            width,
            height,
            snapshot,
            notice,
            Input: string.Empty,
            ShowHelp: showHelp,
            ShowControls: showControls,
            MiniMode: false,
            AnimationFrame: 0,
            QueueScrollOffset: queueScrollOffset),
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
            ShowControls: false,
            MiniMode: true,
            AnimationFrame: 0,
            QueueScrollOffset: 0),
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

    public static bool TryResolveProgressClick(int x, int y, PlayerSnapshot snapshot, out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;
        if (_lastProgressHitbox is not { } hitbox ||
            snapshot.Current?.Duration is not { TotalSeconds: > 0 } duration ||
            y != hitbox.Y ||
            x < hitbox.X ||
            x >= hitbox.X + hitbox.Width)
        {
            return false;
        }

        var ratio = hitbox.Width <= 1
            ? 0
            : (double)(x - hitbox.X) / (hitbox.Width - 1);
        timestamp = TimeSpan.FromSeconds(duration.TotalSeconds * Math.Clamp(ratio, 0, 1));
        return true;
    }

    public static bool TryResolvePlaylistClick(int x, int y, PlayerSnapshot snapshot, out int trackIndex)
    {
        trackIndex = -1;
        var totalTracks = QueueCount(snapshot);
        var hitbox = _lastPlaylistHitboxes.FirstOrDefault(candidate =>
            y == candidate.Y &&
            x >= candidate.X &&
            x < candidate.X + candidate.Width);

        if (hitbox is null || hitbox.TrackIndex < 0 || hitbox.TrackIndex >= totalTracks)
            return false;

        trackIndex = hitbox.TrackIndex;
        return true;
    }

    public static void ResetFrameCache()
    {
        _lastRenderedLines = null;
        _lastRenderedWidth = 0;
        _lastRenderedHeight = 0;
        _lastProgressHitbox = null;
        _lastPlaylistHitboxes = [];
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
        _lastPlaylistHitboxes = [];
        if (available == 0)
        {
            _lastProgressHitbox = null;
            return [prompt];
        }

        var footer = FooterComponent(width, context.ShowHelp, context.ShowControls);
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
            lines.AddRange(QueueComponent(context.Snapshot, width, queueHeight, context.QueueScrollOffset, lines.Count));
        }

        if (playbackHeight > 0)
        {
            _lastProgressHitbox = CreateProgressHitbox(context.Snapshot, width, lines.Count + 2);
            lines.AddRange(PlaybackStripComponent(context, width, playbackHeight));
        }
        else
        {
            _lastProgressHitbox = null;
        }

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
        {
            _lastProgressHitbox = null;
            _lastPlaylistHitboxes = [];
            return [prompt];
        }

        _lastProgressHitbox = null;
        _lastPlaylistHitboxes = [];

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

    private static IReadOnlyList<string> QueueComponent(PlayerSnapshot snapshot, int width, int height, int scrollOffset, int topLineIndex)
    {
        if (height <= 0)
            return [];

        var visibleRows = Math.Max(0, height - 2);
        var playlist = BuildPlaylistRows(snapshot);
        var maxOffset = Math.Max(0, playlist.Count - visibleRows);
        var offset = Math.Clamp(scrollOffset, 0, maxOffset);
        var visiblePlaylistRows = playlist
            .Skip(offset)
            .Take(visibleRows)
            .ToList();
        var rows = visiblePlaylistRows
            .Select(row => row.Text)
            .ToList();

        _lastPlaylistHitboxes = visiblePlaylistRows
            .Select((row, rowOffset) => new PlaylistHitbox(
                row.TrackIndex,
                X: 3,
                Y: topLineIndex + rowOffset + 2,
                Width: Math.Max(1, width - 4)))
            .ToList();

        if (playlist.Count == 0)
            rows.Add($"{TerminalStyles.AnsiMuted}Queue is empty{TerminalStyles.Reset}");

        var title = playlist.Count > visibleRows && visibleRows > 0
            ? $"Playlist {offset + 1}-{Math.Min(offset + visibleRows, playlist.Count)}/{playlist.Count}"
            : "Playlist";

        return Box(title, rows, width, height, TerminalStyles.AnsiChrome);
    }

    private static IReadOnlyList<PlaylistRow> BuildPlaylistRows(PlayerSnapshot snapshot)
    {
        var rows = new List<PlaylistRow>();
        var index = 0;

        foreach (var track in snapshot.Previous)
        {
            rows.Add(new PlaylistRow(
                index,
                PlaylistTrackRow(index + 1, track.Title, PlaylistTrackState.Done)));
            index++;
        }

        if (snapshot.Current is not null)
        {
            rows.Add(new PlaylistRow(
                index,
                PlaylistTrackRow(index + 1, snapshot.Current.Title, PlaylistTrackState.Current)));
            index++;
        }

        foreach (var track in snapshot.Pending)
        {
            rows.Add(new PlaylistRow(
                index,
                PlaylistTrackRow(index + 1, track.Title, PlaylistTrackState.Next)));
            index++;
        }

        return rows;
    }

    private static string PlaylistTrackRow(int displayIndex, string title, PlaylistTrackState state)
    {
        var titleStyle = state switch
        {
            PlaylistTrackState.Done => TerminalStyles.AnsiMuted,
            PlaylistTrackState.Current => $"{TerminalStyles.Bold}{TerminalStyles.AnsiWhite}",
            PlaylistTrackState.Next => TerminalStyles.AnsiDirtyWhite,
            _ => TerminalStyles.AnsiDirtyWhite
        };

        return $"{TerminalStyles.AnsiMuted}{displayIndex,2}.{TerminalStyles.Reset} {PlaylistStatusRail(state)} {titleStyle}{title}{TerminalStyles.Reset}";
    }

    private static string PlaylistStatusRail(PlaylistTrackState state) =>
        state switch
        {
            PlaylistTrackState.Done => $"{TerminalStyles.AnsiMuted}✓──{TerminalStyles.Reset}",
            PlaylistTrackState.Current => $"{TerminalStyles.AnsiGreen}▶──{TerminalStyles.Reset}",
            PlaylistTrackState.Next => $"{TerminalStyles.AnsiDirtyWhite}›──{TerminalStyles.Reset}",
            _ => $"{TerminalStyles.AnsiChrome}›──{TerminalStyles.Reset}"
        };

    private static IReadOnlyList<string> PlaybackStripComponent(RenderContext context, int width, int height)
    {
        if (height <= 0)
            return [];

        var contentWidth = Math.Max(8, width - 6);
        var metrics = PlaybackMetrics(context.Snapshot);
        var progressWidth = Math.Max(8, contentWidth - TerminalText.VisibleLength(metrics) - 3);
        var rows = new List<string>
        {
            $"{ProgressBar(ProgressRatio(context.Snapshot), progressWidth)} {metrics}"
        };

        if (height >= 5)
            rows.Add(string.Empty);

        rows.Add(BrailleWaveVisualizer(context.Snapshot.Status, context.AnimationFrame, contentWidth));

        return Box("Playback", rows, width, height, TerminalStyles.AnsiChrome);
    }

    private static IReadOnlyList<string> FooterComponent(int width, bool showHelp, bool showControls)
    {
        if (!showHelp && !showControls)
        {
            return
            [
                TerminalText.PadAnsiAware($"{TerminalStyles.AnsiMuted}help{TerminalStyles.Reset} commands {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}controls{TerminalStyles.Reset} shortcuts {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}SPACE{TerminalStyles.Reset} play/pause {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {TerminalStyles.AnsiMuted}[ / ]{TerminalStyles.Reset} prev/next", width)
            ];
        }

        if (showControls)
        {
            return Box(
                "Controls",
                [
                    $"{TerminalStyles.AnsiAccent}Space{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}toggle play or pause when the command field is empty{TerminalStyles.Reset}",
                    $"{TerminalStyles.AnsiAccent}Left / Right arrows{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}seek backward or forward by 10 seconds{TerminalStyles.Reset}",
                    $"{TerminalStyles.AnsiAccent}Up / Down arrows{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}scroll playlist by one row{TerminalStyles.Reset}",
                    $"{TerminalStyles.AnsiAccent}PageUp / PageDown{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}scroll playlist faster{TerminalStyles.Reset}",
                    $"{TerminalStyles.AnsiAccent}Home / End{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}jump to top or bottom of playlist{TerminalStyles.Reset}",
                    $"{TerminalStyles.AnsiAccent}[ / ]{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}previous or next track; repeat quickly to jump multiple tracks{TerminalStyles.Reset}",
                    $"{TerminalStyles.AnsiAccent}- / ={TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}decrease or increase volume by 10%{TerminalStyles.Reset}",
                    $"{TerminalStyles.AnsiAccent}Mouse wheel{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}scroll playlist when supported{TerminalStyles.Reset}",
                    $"{TerminalStyles.AnsiAccent}Click playlist song{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}play that song directly{TerminalStyles.Reset}",
                    $"{TerminalStyles.AnsiAccent}Click progress bar{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}jump to that timestamp when supported{TerminalStyles.Reset}"
                ],
                width,
                12,
                TerminalStyles.AnsiChrome);
        }

        return Box(
            "Commands",
            [
                $"{TerminalStyles.AnsiAccent}add <url>{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}load a YouTube video or playlist{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiAccent}play/pause{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}toggle playback{TerminalStyles.Reset}   {TerminalStyles.AnsiAccent}next [count] / prev [count]{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}skip or return tracks{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiAccent}goto <#|title>{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}play a playlist song directly{TerminalStyles.Reset}   {TerminalStyles.AnsiAccent}shuffle{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}randomize the playlist{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiAccent}clear playlist{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}stop playback and empty the queue{TerminalStyles.Reset}",
                $"{TerminalStyles.AnsiAccent}q/quit/exit{TerminalStyles.Reset}  {TerminalStyles.AnsiMuted}stop playback and exit{TerminalStyles.Reset}"
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
        var metrics = $"{TerminalStyles.AnsiMuted}{time}{TerminalStyles.Reset} {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {VolumeText(snapshot.VolumePercent)}";
        var rows = new List<string>
        {
            $"{TerminalStyles.Bold}{TerminalStyles.AnsiAccent}PopMark{TerminalStyles.Reset} {StatusPill(snapshot.Status)}",
            $"{TerminalStyles.AnsiWhite}{TerminalText.TrimForWidget(title, contentWidth)}{TerminalStyles.Reset}",
            $"{ProgressBar(ProgressRatio(snapshot), Math.Max(8, contentWidth - TerminalText.VisibleLength(metrics) - 3))} {metrics}"
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

    private static string PlaybackMetrics(PlayerSnapshot snapshot)
    {
        var time = $"{FormatDuration(snapshot.Elapsed)} / {FormatDuration(snapshot.Current?.Duration)}";
        return $"{TerminalStyles.AnsiMuted}{time}{TerminalStyles.Reset} {TerminalStyles.AnsiChrome}|{TerminalStyles.Reset} {VolumeIndicator(snapshot.VolumePercent)}";
    }

    private static string VolumeIndicator(int volumePercent)
    {
        var volume = Math.Clamp(volumePercent, 0, 130);
        var normalVolume = Math.Min(volume, 100);
        const int meterWidth = 8;
        var filled = Math.Clamp((int)Math.Round(normalVolume / 100d * meterWidth), 0, meterWidth);
        var empty = meterWidth - filled;

        return $"{TerminalStyles.AnsiMuted}Vol{TerminalStyles.Reset} {TerminalStyles.AnsiAccent}{new string('█', filled)}{TerminalStyles.AnsiChrome}{new string('░', empty)}{TerminalStyles.Reset} {VolumeText(volume)}";
    }

    private static string VolumeText(int volumePercent)
    {
        var volume = Math.Clamp(volumePercent, 0, 130);
        var style = volume > 100 ? TerminalStyles.AnsiSecondary : TerminalStyles.AnsiMuted;
        return $"{style}{volume}%{TerminalStyles.Reset}";
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
        snapshot.Previous.Count + snapshot.Pending.Count + (snapshot.Current is null ? 0 : 1);

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

    private static ProgressHitbox? CreateProgressHitbox(PlayerSnapshot snapshot, int width, int y)
    {
        if (snapshot.Current?.Duration is not { TotalSeconds: > 0 })
            return null;

        var contentWidth = Math.Max(8, width - 6);
        var metrics = PlaybackMetrics(snapshot);
        var progressWidth = Math.Max(8, contentWidth - TerminalText.VisibleLength(metrics) - 3);
        return new ProgressHitbox(X: 3, Y: y, Width: progressWidth);
    }

    private enum PlaylistTrackState
    {
        Done,
        Current,
        Next
    }

    private sealed record PlaylistRow(int TrackIndex, string Text);

    private sealed record PlaylistHitbox(int TrackIndex, int X, int Y, int Width);

    private sealed record ProgressHitbox(int X, int Y, int Width);
}
