using PopMark.Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Runtime.InteropServices;
using System.Text;

namespace PopMark.Helpers;

public static class ConsoleHelper
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private const string Accent = "#00d4ff";
    private const string SuccessColor = "#00d4ff";
    private const string Muted = "grey70";
    private const string Reset = "\u001b[0m";
    private const string Bold = "\u001b[1m";
    private const string AnsiAccent = "\u001b[38;2;0;212;255m";
    private const string AnsiSecondary = "\u001b[38;2;139;92;246m";
    private const string AnsiMuted = "\u001b[38;2;150;150;150m";
    private const string AnsiChrome = "\u001b[38;2;82;82;82m";
    private const string AnsiWhite = "\u001b[38;2;245;245;245m";
    private static readonly string[] VisualizerBars = ["▁", "▂", "▃", "▄", "▅", "▆", "▇"];
    private static List<string>? _lastRenderedLines;
    private static int _lastRenderedWidth;
    private static int _lastRenderedHeight;
    private static bool _usingAlternateScreen;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

    public static void InitializeTerminalCapabilities()
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.InputEncoding = Encoding.UTF8;
        }
        catch
        {
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            var outputHandle = GetStdHandle(StdOutputHandle);
            if (outputHandle == 0 || outputHandle == -1)
                return;

            if (!GetConsoleMode(outputHandle, out var mode))
                return;

            _ = SetConsoleMode(outputHandle, mode | EnableVirtualTerminalProcessing);
        }
        catch
        {
        }
    }

    public static void ShowStartupSplash()
    {
        TryClear();
        AnsiConsole.Cursor.Hide();

        try
        {
            AnsiConsole.Live(CreateSplashFrame(0))
                .Overflow(VerticalOverflow.Crop)
                .Start(ctx =>
                {
                    var frame = 0;
                    while (!Console.KeyAvailable)
                    {
                        ctx.UpdateTarget(CreateSplashFrame(frame));
                        Thread.Sleep(52);
                        frame = (frame + 1) % 160;
                    }

                    _ = Console.ReadKey(intercept: true);
                });
        }
        finally
        {
            AnsiConsole.Cursor.Show();
            TryClear();
        }
    }

    public static void EnterInteractiveScreen()
    {
        if (Console.IsOutputRedirected || _usingAlternateScreen)
            return;

        Console.Write("\u001b[?1049h\u001b[?25l\u001b[H\u001b[2J");
        _usingAlternateScreen = true;
        ResetFrameCache();
    }

    public static void LeaveInteractiveScreen()
    {
        if (Console.IsOutputRedirected || !_usingAlternateScreen)
            return;

        ResetFrameCache();
        Console.Write("\u001b[?25h\u001b[?1049l");
        _usingAlternateScreen = false;
    }

    public static void DrawCommandCenter(PlayerSnapshot snapshot, string notice, bool showHelp = false)
    {
        var (width, height) = GetWindowSize();
        RenderFrame(new RenderContext(
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
        var (width, height) = GetWindowSize();
        RenderFrame(new RenderContext(
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

    public static string ReadReactiveInput(
        ref int lastWidth,
        ref int lastHeight,
        List<string> commandHistory,
        Func<PlayerSnapshot>? snapshotProvider = null,
        Func<string>? noticeProvider = null,
        Func<bool>? miniModeProvider = null,
        Func<bool>? helpModeProvider = null)
    {
        if (snapshotProvider is null || noticeProvider is null)
        {
            AnsiConsole.Markup($"[bold {Accent}]popmark[/][{Muted}] >[/] ");
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }

        var buffer = new StringBuilder();
        var historyIndex = commandHistory.Count;
        var browsingHistory = false;
        var draftInput = string.Empty;
        var animationFrame = 0;
        var lastRefresh = DateTimeOffset.MinValue;
        var lastScreenSignature = string.Empty;
        var trackedWidth = lastWidth;
        var trackedHeight = lastHeight;

        bool IsInputActive() => buffer.Length > 0 || browsingHistory;

        void RefreshScreen(bool advanceAnimation = false)
        {
            if (advanceAnimation)
                animationFrame++;

            var (width, height) = GetWindowSize();
            trackedWidth = width;
            trackedHeight = height;
            var context = new RenderContext(
                width,
                height,
                snapshotProvider(),
                noticeProvider(),
                buffer.ToString(),
                helpModeProvider?.Invoke() == true,
                miniModeProvider?.Invoke() == true,
                animationFrame);
            RenderFrame(context);
            lastRefresh = DateTimeOffset.UtcNow;
            lastScreenSignature = BuildScreenSignature(
                snapshotProvider,
                noticeProvider,
                miniModeProvider,
                helpModeProvider,
                buffer.ToString(),
                includeElapsed: !IsInputActive()) ?? string.Empty;
        }

        RefreshScreen();

        while (true)
        {
            var (width, height) = GetWindowSize();
            if (width > 0 && height > 0 && (width != trackedWidth || height != trackedHeight))
            {
                RefreshScreen();
                continue;
            }

            if (!Console.KeyAvailable)
            {
                var now = DateTimeOffset.UtcNow;
                var snapshot = snapshotProvider();
                var inputActive = IsInputActive();
                var isAnimating = !inputActive && snapshot.Status is PlaybackStatus.Playing or PlaybackStatus.Loading;
                var refreshMilliseconds = isAnimating ? 110 : 250;
                if ((now - lastRefresh).TotalMilliseconds >= refreshMilliseconds)
                {
                    var screenSignature = BuildScreenSignature(
                        snapshotProvider,
                        noticeProvider,
                        miniModeProvider,
                        helpModeProvider,
                        buffer.ToString(),
                        includeElapsed: !inputActive) ?? string.Empty;
                    if (isAnimating || !string.Equals(screenSignature, lastScreenSignature, StringComparison.Ordinal))
                    {
                        RefreshScreen(advanceAnimation: isAnimating);
                    }
                }

                Thread.Sleep(isAnimating ? 24 : 40);
                continue;
            }

            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.L && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                lastWidth = trackedWidth;
                lastHeight = trackedHeight;
                return "cls";
            }

            if (buffer.Length == 0 && !browsingHistory)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Spacebar:
                        lastWidth = trackedWidth;
                        lastHeight = trackedHeight;
                        return "play";
                    case ConsoleKey.N:
                        lastWidth = trackedWidth;
                        lastHeight = trackedHeight;
                        return "next";
                    case ConsoleKey.M:
                        lastWidth = trackedWidth;
                        lastHeight = trackedHeight;
                        return "mini";
                    case ConsoleKey.Q:
                        lastWidth = trackedWidth;
                        lastHeight = trackedHeight;
                        return "q";
                    case ConsoleKey.Oem2 when key.KeyChar == '?':
                        lastWidth = trackedWidth;
                        lastHeight = trackedHeight;
                        return "help";
                }
            }

            if (key.Key == ConsoleKey.Enter)
            {
                lastWidth = trackedWidth;
                lastHeight = trackedHeight;
                return buffer.ToString().Trim();
            }

            if (key.Key == ConsoleKey.Escape)
            {
                lastWidth = trackedWidth;
                lastHeight = trackedHeight;
                return string.Empty;
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                if (commandHistory.Count == 0)
                    continue;

                if (!browsingHistory)
                {
                    draftInput = buffer.ToString();
                    browsingHistory = true;
                    historyIndex = commandHistory.Count;
                }

                if (historyIndex > 0)
                {
                    historyIndex--;
                    buffer.Clear();
                    buffer.Append(commandHistory[historyIndex]);
                    RefreshScreen();
                }

                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                if (!browsingHistory)
                    continue;

                if (historyIndex < commandHistory.Count - 1)
                {
                    historyIndex++;
                    buffer.Clear();
                    buffer.Append(commandHistory[historyIndex]);
                }
                else
                {
                    historyIndex = commandHistory.Count;
                    browsingHistory = false;
                    buffer.Clear();
                    buffer.Append(draftInput);
                }

                RefreshScreen();
                continue;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                    RefreshScreen();
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
                RefreshScreen();
            }
        }
    }

    public static string[] SplitArgs(string commandLine)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in commandLine)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return args.ToArray();
    }

    private static void RenderFrame(RenderContext context, bool forceFullPaint = false)
    {
        var width = context.Width > 0 ? context.Width : 80;
        var height = context.Height > 0 ? context.Height : 24;
        var lines = BuildTerminalFrame(context with { Width = width, Height = height });

        while (lines.Count < height)
            lines.Add(string.Empty);

        if (lines.Count > height)
            lines.RemoveRange(height, lines.Count - height);

        for (var i = 0; i < lines.Count; i++)
            lines[i] = TrimAnsiAware(lines[i], width);

        if (Console.IsOutputRedirected)
        {
            Console.Write(string.Join(Environment.NewLine, lines.Select(line => PadAnsiAware(line, width))));
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
        output.Append("\u001b[?25l");
        output.Append(requiresFullPaint ? "\u001b[H\u001b[2J" : "\u001b[H");

        for (var i = 0; i < lines.Count; i++)
        {
            var changed = requiresFullPaint ||
                          i >= _lastRenderedLines!.Count ||
                          !string.Equals(lines[i], _lastRenderedLines[i], StringComparison.Ordinal);
            if (changed)
                output.Append("\u001b[2K").Append(PadAnsiAware(lines[i], width));

            if (i < lines.Count - 1)
                output.Append("\u001b[1E");
        }

        Console.Write(output.ToString());
        _lastRenderedLines = [.. lines];
        _lastRenderedWidth = width;
        _lastRenderedHeight = height;
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
        var playbackHeight = available >= 8 ? 4 : available >= 5 ? 3 : 0;
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
        var blockHeight = Math.Min(7, available);
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
        var left = $"{Bold}{AnsiAccent}PopMark{Reset} {AnsiMuted}{StatusText(context.Snapshot.Status)}{Reset}";
        var right = $"{AnsiMuted}Queue{Reset} {AnsiWhite}{queue}{Reset}";
        var gap = Math.Max(1, width - VisibleLength(left) - VisibleLength(right));
        return PadAnsiAware($"{left}{new string(' ', gap)}{right}", width);
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
        var rows = new List<string>
        {
            $"{Bold}{AnsiWhite}{TrimForWidget(title, Math.Max(8, width - 8))}{Reset}",
            $"{AnsiMuted}{TrimForWidget(source, Math.Max(8, width - 18))}{Reset}",
            $"{StatusPill(snapshot.Status)} {AnsiMuted}{TrimForWidget(context.Notice, Math.Max(8, width - 18))}{Reset}",
            $"{AnsiMuted}Elapsed{Reset} {AnsiWhite}{FormatDuration(snapshot.Elapsed)}{Reset} {AnsiChrome}|{Reset} {AnsiMuted}Duration{Reset} {AnsiWhite}{duration}{Reset}"
        };

        return Box("Now Playing", rows, width, height, AnsiAccent);
    }

    private static IReadOnlyList<string> QueueComponent(PlayerSnapshot snapshot, int width, int height)
    {
        if (height <= 0)
            return [];

        var rows = new List<string>();
        if (snapshot.Current is not null)
            rows.Add($"{AnsiAccent}> {TrimForWidget(snapshot.Current.Title, Math.Max(8, width - 10))}{Reset}");

        var availableTracks = Math.Max(0, height - 3 - rows.Count);
        var index = 1;
        foreach (var track in snapshot.Pending.Take(availableTracks))
        {
            var color = index == 1 ? AnsiWhite : AnsiMuted;
            rows.Add($"{color}{index,2}. {TrimForWidget(track.Title, Math.Max(8, width - 12))}{Reset}");
            index++;
        }

        if (snapshot.Pending.Count == 0 && snapshot.Current is not null)
            rows.Add($"{AnsiMuted}End of queue{Reset}");
        else if (snapshot.Pending.Count > availableTracks)
            rows.Add($"{AnsiMuted}+ {snapshot.Pending.Count - availableTracks} more{Reset}");

        return Box("Queue", rows, width, height, AnsiChrome);
    }

    private static IReadOnlyList<string> PlaybackStripComponent(RenderContext context, int width, int height)
    {
        if (height <= 0)
            return [];

        var contentWidth = Math.Max(8, width - 6);
        var time = $"{FormatDuration(context.Snapshot.Elapsed)} / {FormatDuration(context.Snapshot.Current?.Duration)}";
        var progressWidth = Math.Max(8, contentWidth - VisibleLength(time) - 3);
        var rows = new List<string>
        {
            $"{ProgressBar(ProgressRatio(context.Snapshot), progressWidth)} {AnsiMuted}{time}{Reset}",
            Visualizer(context.Snapshot.Status, context.AnimationFrame, contentWidth)
        };

        return Box("Playback", rows, width, height, AnsiChrome);
    }

    private static IReadOnlyList<string> FooterComponent(int width, bool showHelp)
    {
        if (!showHelp)
        {
            return
            [
                PadAnsiAware($"{AnsiMuted}SPACE{Reset} play/pause {AnsiChrome}|{Reset} {AnsiMuted}N{Reset} next {AnsiChrome}|{Reset} {AnsiMuted}seek +/-30{Reset} {AnsiChrome}|{Reset} {AnsiMuted}clear playlist{Reset} {AnsiChrome}|{Reset} {AnsiMuted}M{Reset} mini {AnsiChrome}|{Reset} {AnsiMuted}Q{Reset} quit", width)
            ];
        }

        return Box(
            "Commands",
            [
                $"{AnsiAccent}add <url>{Reset}  {AnsiMuted}load a YouTube video or playlist{Reset}",
                $"{AnsiAccent}play/pause{Reset}  {AnsiMuted}toggle playback{Reset}   {AnsiAccent}next{Reset}  {AnsiMuted}skip track{Reset}",
                $"{AnsiAccent}seek <seconds>{Reset}  {AnsiMuted}relative seek, for example seek 30 or seek -30{Reset}",
                $"{AnsiAccent}clear playlist{Reset}  {AnsiMuted}stop playback and empty the queue{Reset}",
                $"{AnsiAccent}mini{Reset}  {AnsiMuted}compact view{Reset}   {AnsiAccent}cls{Reset}  {AnsiMuted}redraw{Reset}   {AnsiAccent}q{Reset}  {AnsiMuted}quit{Reset}"
            ],
            width,
            7,
            AnsiChrome);
    }

    private static IReadOnlyList<string> EmptyStateComponent(int width, int height)
    {
        if (height <= 0)
            return [];

        return Box(
            "Ready",
            [
                $"{Bold}{AnsiAccent}PopMark{Reset}",
                $"{AnsiMuted}Queue is empty.{Reset}",
                $"{AnsiWhite}add <url>{Reset} {AnsiMuted}to start playback from YouTube.{Reset}"
            ],
            width,
            height,
            AnsiAccent);
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
            $"{Bold}{AnsiAccent}PopMark{Reset} {StatusPill(snapshot.Status)}",
            $"{AnsiWhite}{TrimForWidget(title, contentWidth)}{Reset}",
            $"{ProgressBar(ProgressRatio(snapshot), Math.Max(8, contentWidth - VisibleLength(time) - 3))} {AnsiMuted}{time}{Reset}",
            Visualizer(snapshot.Status, context.AnimationFrame, contentWidth),
            $"{AnsiMuted}{TrimForWidget(context.Notice, contentWidth)}{Reset}"
        };

        return Box("Mini", rows, width, height, AnsiAccent);
    }

    private static IReadOnlyList<string> Box(string title, IReadOnlyList<string> rows, int width, int height, string borderStyle)
    {
        width = Math.Max(8, width);
        height = Math.Max(1, height);
        if (height == 1)
            return [PadAnsiAware($"{borderStyle}{TrimForWidget(title, width)}{Reset}", width)];

        var innerWidth = width - 2;
        var titleText = $" {TrimForWidget(title, Math.Max(0, innerWidth - 2))} ";
        var topFill = Math.Max(0, innerWidth - VisibleLength(titleText));
        var lines = new List<string>
        {
            $"{borderStyle}┌{titleText}{new string('─', topFill)}┐{Reset}"
        };

        var contentHeight = height - 2;
        for (var i = 0; i < contentHeight; i++)
        {
            var content = i < rows.Count ? TrimAnsiAware(rows[i], Math.Max(0, innerWidth - 2)) : string.Empty;
            lines.Add($"{borderStyle}│{Reset} {PadAnsiAware(content, Math.Max(0, innerWidth - 2))} {borderStyle}│{Reset}");
        }

        lines.Add($"{borderStyle}└{new string('─', innerWidth)}┘{Reset}");
        return lines;
    }

    private static string PromptComponent(string input, int width)
    {
        var prefix = $"{Bold}{AnsiAccent}popmark{Reset}{AnsiMuted} > {Reset}";
        var maxInputWidth = Math.Max(0, width - VisibleLength(prefix) - 1);
        var displayInput = TailForWidth(input, maxInputWidth);
        return PadAnsiAware($"{prefix}{AnsiWhite}{displayInput}{Reset}{AnsiAccent}|{Reset}", width);
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
            PlaybackStatus.Playing => (AnsiAccent, "PLAYING"),
            PlaybackStatus.Loading => (AnsiSecondary, "LOADING"),
            PlaybackStatus.Paused => (AnsiAccent, "PAUSED"),
            PlaybackStatus.Detached => (AnsiSecondary, "DETACHED"),
            _ => (AnsiMuted, "STOPPED")
        };

        return $"{style}{label}{Reset}";
    }

    private static string ProgressBar(double ratio, int width)
    {
        width = Math.Max(1, width);
        var filled = (int)Math.Round(width * Math.Clamp(ratio, 0, 1));
        filled = Math.Clamp(filled, 0, width);
        var empty = width - filled;
        return $"{AnsiAccent}{new string('█', filled)}{AnsiChrome}{new string('░', empty)}{Reset}";
    }

    private static string Visualizer(PlaybackStatus status, int frame, int width)
    {
        width = Math.Max(1, width);
        var barCount = Math.Max(1, (width + 1) / 2);
        var style = status switch
        {
            PlaybackStatus.Playing or PlaybackStatus.Loading => AnsiAccent,
            PlaybackStatus.Paused => AnsiMuted,
            _ => AnsiChrome
        };

        var bars = Enumerable.Range(0, barCount).Select(i =>
        {
            if (status == PlaybackStatus.Paused)
                return "▃";

            if (status is not (PlaybackStatus.Playing or PlaybackStatus.Loading))
                return "▁";

            var phase = (frame + (i * 3) + ((i % 5) * 2)) % VisualizerBars.Length;
            return VisualizerBars[phase];
        });

        return TrimAnsiAware($"{style}{string.Join(' ', bars)}{Reset}", width);
    }

    private static string PadAnsiAware(string value, int width)
    {
        var visible = VisibleLength(value);
        return visible >= width ? value : $"{value}{new string(' ', width - visible)}";
    }

    private static string TrimAnsiAware(string value, int width)
    {
        if (width <= 0)
            return string.Empty;

        var visible = 0;
        var output = new StringBuilder();
        for (var i = 0; i < value.Length && visible < width; i++)
        {
            if (value[i] == '\u001b')
            {
                var start = i;
                output.Append(value[i]);
                while (i + 1 < value.Length)
                {
                    i++;
                    output.Append(value[i]);
                    if (char.IsLetter(value[i]))
                        break;
                }

                if (i == start)
                    visible++;

                continue;
            }

            output.Append(value[i]);
            visible++;
        }

        if (VisibleLength(value) > width)
            output.Append(Reset);

        return output.ToString();
    }

    private static int VisibleLength(string value)
    {
        var visible = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\u001b')
            {
                while (i + 1 < value.Length)
                {
                    i++;
                    if (char.IsLetter(value[i]))
                        break;
                }

                continue;
            }

            visible++;
        }

        return visible;
    }

    private static string TailForWidth(string value, int width)
    {
        if (width <= 0)
            return string.Empty;

        if (value.Length <= width)
            return value;

        return value[^width..];
    }

    private sealed record RenderContext(
        int Width,
        int Height,
        PlayerSnapshot Snapshot,
        string Notice,
        string Input,
        bool ShowHelp,
        bool MiniMode,
        int AnimationFrame);

    public static void UseBarCursor()
    {
        try
        {
            if (!Console.IsOutputRedirected)
                Console.Write("\u001b[5 q\u001b[?25l");
        }
        catch
        {
        }
    }

    public static void ShowCursor()
    {
        try
        {
            if (!Console.IsOutputRedirected)
                Console.Write("\u001b[?25h");
        }
        catch
        {
        }
    }

    public static void ResetCursorStyle()
    {
        try
        {
            if (!Console.IsOutputRedirected)
                Console.Write("\u001b[?25h\u001b[0 q");
        }
        catch
        {
        }
    }

    public static (int Width, int Height) GetWindowSize()
    {
        try
        {
            return (Console.WindowWidth, Console.WindowHeight);
        }
        catch
        {
            return (0, 0);
        }
    }

    public static void Info(string message) =>
        AnsiConsole.MarkupLine($"[cyan][[Info]][/] {Markup.Escape(message)}");

    public static void Warn(string message) =>
        AnsiConsole.MarkupLine($"[yellow][[Warn]][/] {Markup.Escape(message)}");

    public static void Success(string message) =>
        AnsiConsole.MarkupLine($"[green][[Done]][/] {Markup.Escape(message)}");

    public static void Error(string message) =>
        AnsiConsole.MarkupLine($"[red][[ERR]][/] {Markup.Escape(message)}");

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

    private static string? BuildScreenSignature(
        Func<PlayerSnapshot>? snapshotProvider,
        Func<string>? noticeProvider,
        Func<bool>? miniModeProvider,
        Func<bool>? helpModeProvider,
        string input,
        bool includeElapsed)
    {
        if (snapshotProvider is null || noticeProvider is null)
            return null;

        var snapshot = snapshotProvider();
        var builder = new StringBuilder()
            .Append(miniModeProvider?.Invoke() == true ? "mini" : "full")
            .Append('|')
            .Append(helpModeProvider?.Invoke() == true ? "help" : "normal")
            .Append('|')
            .Append(snapshot.Status)
            .Append('|')
            .Append(noticeProvider())
            .Append('|')
            .Append(input)
            .Append('|')
            .Append(includeElapsed ? snapshot.Elapsed?.TotalSeconds.ToString("0") : "static-input")
            .Append('|');

        AppendTrack(builder, snapshot.Current);

        foreach (var track in snapshot.Pending)
            AppendTrack(builder.Append('|'), track);

        return builder.ToString();
    }

    private static void AppendTrack(StringBuilder builder, Track? track)
    {
        if (track is null)
        {
            builder.Append("<none>");
            return;
        }

        builder
            .Append(track.Title)
            .Append('\u001f')
            .Append(track.Url)
            .Append('\u001f')
            .Append(track.DisplaySource)
            .Append('\u001f')
            .Append(track.Duration);
    }

    private static string TrimForWidget(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        return maxLength <= 3 ? value[..maxLength] : $"{value[..(maxLength - 3)]}...";
    }

    private static IRenderable CreateSplashFrame(int frame)
    {
        var footer = new Rows(
            Align.Center(new Markup($"[bold {Accent}]PopMark[/] [silver]terminal music queue[/]")),
            Align.Center(new Markup($"[bold {SuccessColor}]Press any key to continue[/]")));

        return new Rows(
            new Text(" "),
            new Text(" "),
            Align.Center(CreateCassetteCanvas(frame)),
            new Text(" "),
            footer);
    }

    private static Canvas CreateCassetteCanvas(int frame)
    {
        const int width = 44;
        const int height = 22;
        var canvas = new Canvas(width, height);
        var bob = Math.Sin(frame * 0.32) * 0.65;
        var body = Color.DeepSkyBlue1;
        var label = Color.Grey93;
        var accent = frame % 14 < 7 ? Color.Cyan1 : Color.Turquoise2;
        var dark = Color.Grey27;

        FillRect(canvas, 6, 5 + bob, 32, 13, body);
        FillRect(canvas, 8, 8 + bob, 28, 4, label);
        FillRect(canvas, 13, 15 + bob, 20, 2, dark);

        DrawEllipse(canvas, 16, 13 + bob, 3.7, 2.5, dark);
        DrawEllipse(canvas, 29, 13 + bob, 3.7, 2.5, dark);
        DrawEllipse(canvas, 16, 13 + bob, 1.4, 0.95, accent);
        DrawEllipse(canvas, 29, 13 + bob, 1.4, 0.95, accent);

        var spin = frame % 6;
        DrawLine(canvas, 16, 13 + bob, 16 + Math.Cos(spin) * 2.8, 13 + bob + Math.Sin(spin) * 1.6, label);
        DrawLine(canvas, 29, 13 + bob, 29 - Math.Cos(spin) * 2.8, 13 + bob - Math.Sin(spin) * 1.6, label);

        DrawPixelBlock(canvas, 20, (int)Math.Round(10 + bob), dark);
        DrawPixelBlock(canvas, 25, (int)Math.Round(10 + bob), dark);
        DrawLine(canvas, 22, 11 + bob, 24, 11 + bob, dark);

        DrawLine(canvas, 2, 9 + bob, 5, 7 + bob, accent);
        DrawLine(canvas, 39, 8 + bob, 42, 6 + bob, accent);
        DrawLine(canvas, 39, 11 + bob, 42, 11 + bob, accent);

        DrawTwinkle(canvas, frame, 4, 4, 0);
        DrawTwinkle(canvas, frame, 39, 18, 3);
        DrawTwinkle(canvas, frame, 23, 2, 5);

        return canvas;
    }

    private static void FillRect(Canvas canvas, int left, double top, int width, int height, Color color)
    {
        var topY = (int)Math.Round(top);
        for (var y = topY; y < topY + height; y++)
        {
            for (var x = left; x < left + width; x++)
                SafeSetPixel(canvas, x, y, color);
        }
    }

    private static void DrawEllipse(Canvas canvas, double centerX, double centerY, double radiusX, double radiusY, Color color)
    {
        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                var nx = (x - centerX) / radiusX;
                var ny = (y - centerY) / radiusY;
                if ((nx * nx) + (ny * ny) <= 1.0)
                    SafeSetPixel(canvas, x, y, color);
            }
        }
    }

    private static void DrawTriangle(Canvas canvas, double x1, double y1, double x2, double y2, double x3, double y3, Color color)
    {
        var minX = (int)Math.Floor(Math.Min(x1, Math.Min(x2, x3)));
        var maxX = (int)Math.Ceiling(Math.Max(x1, Math.Max(x2, x3)));
        var minY = (int)Math.Floor(Math.Min(y1, Math.Min(y2, y3)));
        var maxY = (int)Math.Ceiling(Math.Max(y1, Math.Max(y2, y3)));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var area = TriangleArea(x1, y1, x2, y2, x3, y3);
                var a1 = TriangleArea(x, y, x2, y2, x3, y3);
                var a2 = TriangleArea(x1, y1, x, y, x3, y3);
                var a3 = TriangleArea(x1, y1, x2, y2, x, y);

                if (Math.Abs(area - (a1 + a2 + a3)) < 0.8)
                    SafeSetPixel(canvas, x, y, color);
            }
        }
    }

    private static double TriangleArea(double x1, double y1, double x2, double y2, double x3, double y3) =>
        Math.Abs((x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2)) / 2.0);

    private static void DrawLine(Canvas canvas, double x1, double y1, double x2, double y2, Color color)
    {
        var steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        for (var i = 0; i <= steps; i++)
        {
            var t = steps == 0 ? 0 : i / steps;
            var x = (int)Math.Round(x1 + ((x2 - x1) * t));
            var y = (int)Math.Round(y1 + ((y2 - y1) * t));
            SafeSetPixel(canvas, x, y, color);
        }
    }

    private static void DrawPixelBlock(Canvas canvas, int x, int y, Color color)
    {
        SafeSetPixel(canvas, x, y, color);
        SafeSetPixel(canvas, x + 1, y, color);
    }

    private static void DrawTwinkle(Canvas canvas, int frame, int x, int y, int phaseOffset)
    {
        var phase = (frame + phaseOffset) % 8;
        var color = phase switch
        {
            0 => Color.White,
            1 => Color.Aqua,
            2 => Color.CadetBlue_1,
            _ => Color.Grey35
        };

        SafeSetPixel(canvas, x, y, color);
        if (phase <= 2)
        {
            SafeSetPixel(canvas, x - 1, y, Color.Grey35);
            SafeSetPixel(canvas, x + 1, y, Color.Grey35);
            SafeSetPixel(canvas, x, y - 1, Color.Grey35);
            SafeSetPixel(canvas, x, y + 1, Color.Grey35);
        }
    }

    private static void SafeSetPixel(Canvas canvas, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= canvas.Width || y >= canvas.Height)
            return;

        canvas.SetPixel(x, y, color);
    }

    private static void TryClear()
    {
        try
        {
            ResetFrameCache();

            if (!Console.IsOutputRedirected)
            {
                Console.Write("\u001b[H\u001b[2J\u001b[3J");
                return;
            }

            AnsiConsole.Clear();
        }
        catch
        {
            AnsiConsole.Clear();
        }
    }

    private static void ResetFrameCache()
    {
        _lastRenderedLines = null;
        _lastRenderedWidth = 0;
        _lastRenderedHeight = 0;
    }
}
