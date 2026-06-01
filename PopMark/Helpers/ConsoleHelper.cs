using PopMark.Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace PopMark.Helpers;

public static class ConsoleHelper
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private const int SwpNoZOrder = 0x0004;
    private const int SwpNoActivate = 0x0010;
    private const int MonitorDefaultToNearest = 0x00000002;
    private const int MiniWidgetWidth = 46;
    private const int MiniWidgetHeight = 9;
    private const int MiniConsoleColumns = 52;
    private const int MiniConsoleRows = 13;
    private const string Accent = "deeppink1";
    private const string Secondary = "cyan1";
    private const string SuccessColor = "springgreen1";
    private const string Muted = "grey58";
    private const string Chrome = "grey23";
    private const string PanelBorder = "deeppink1";
    private static ConsoleWindowState? _savedWindowState;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetConsoleWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(nint hWnd, out Rect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfo lpmi);

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
                        Thread.Sleep(72);
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

    public static void DrawCommandCenter(PlayerSnapshot snapshot, string notice)
    {
        TryClear();

        AnsiConsole.Write(BuildPlayerHeader(snapshot));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildNowPlayingPanel(snapshot, notice));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildQueueTable(snapshot));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHelpTable());
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    public static void DrawMiniPlayer(PlayerSnapshot snapshot, string notice)
    {
        TryClear();

        var track = snapshot.Current?.Title
            ?? snapshot.Pending.FirstOrDefault()?.Title
            ?? "Queue is empty";
        var (_, windowHeight) = GetWindowSize();
        var topPadding = Math.Max(0, windowHeight - MiniWidgetHeight - 2);
        var textWidth = MiniWidgetWidth - 12;
        if (!Console.IsOutputRedirected && topPadding > 0)
            Console.Write(new string('\n', topPadding));

        var body = new Rows(
            Align.Center(new Markup($"[bold {Accent}]PopMark[/] [dim {Secondary}]// mini[/]")),
            Align.Center(new Markup(StatusMarkup(snapshot.Status))),
            Align.Center(new Markup($"[white]{Markup.Escape(TrimForWidget(track, textWidth))}[/]")),
            Align.Center(new Markup(BuildEqualizer(snapshot.Status))),
            Align.Center(new Markup($"[{Muted}]{Markup.Escape(TrimForWidget(notice, textWidth))}[/]")),
            Align.Center(new Markup($"[{Accent}]a[/] [{Muted}]+[/] [{Secondary}]r[/] [{Muted}]play[/] [{Secondary}]n[/] [{Muted}]next[/] [{Secondary}]m[/] [{Muted}]full[/] [{Accent}]q[/]")));

        var panel = new Panel(body)
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse(PanelBorder))
            .Header($"[bold {Secondary}]ON AIR[/]", Justify.Right)
            .Padding(1, 0);

        AnsiConsole.Write(Align.Right(panel));
    }

    public static void ConfigureMiniModeWindow(bool enabled)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || Console.IsOutputRedirected)
            return;

        try
        {
            if (enabled)
            {
                _savedWindowState ??= CaptureConsoleWindowState();
                TrySetConsoleSize(MiniConsoleColumns, MiniConsoleRows);
                TryDockConsoleWindow(MiniConsoleColumns, MiniConsoleRows);
                return;
            }

            if (_savedWindowState is not null)
            {
                RestoreConsoleWindowState(_savedWindowState.Value);
                _savedWindowState = null;
            }
        }
        catch
        {
        }
    }

    public static string ReadReactiveInput(
        ref int lastWidth,
        ref int lastHeight,
        List<string> commandHistory,
        Func<PlayerSnapshot>? snapshotProvider = null,
        Func<string>? noticeProvider = null,
        Func<bool>? miniModeProvider = null)
    {
        var buffer = new StringBuilder();
        var historyIndex = commandHistory.Count;
        var browsingHistory = false;
        var draftInput = string.Empty;
        var lastRefresh = DateTimeOffset.UtcNow;
        var lastScreenSignature = BuildScreenSignature(snapshotProvider, noticeProvider, miniModeProvider);
        RenderPrompt(buffer.ToString());

        void RefreshScreen()
        {
            if (snapshotProvider is null || noticeProvider is null)
                return;

            if (miniModeProvider?.Invoke() == true)
                DrawMiniPlayer(snapshotProvider(), noticeProvider());
            else
                DrawCommandCenter(snapshotProvider(), noticeProvider());
            RenderPrompt(buffer.ToString());
        }

        void RedrawInputLine()
        {
            Console.Write("\r");
            Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - 1)));
            Console.Write("\r");
            RenderPrompt(buffer.ToString());
        }

        while (true)
        {
            var (width, height) = GetWindowSize();
            if (width > 0 && height > 0 && (width != lastWidth || height != lastHeight))
            {
                lastWidth = width;
                lastHeight = height;
                return "cls";
            }

            if (!Console.KeyAvailable)
            {
                if ((DateTimeOffset.UtcNow - lastRefresh).TotalMilliseconds >= 250)
                {
                    lastRefresh = DateTimeOffset.UtcNow;
                    var screenSignature = BuildScreenSignature(snapshotProvider, noticeProvider, miniModeProvider);
                    if (!string.Equals(screenSignature, lastScreenSignature, StringComparison.Ordinal))
                    {
                        lastScreenSignature = screenSignature;
                        RefreshScreen();
                    }
                }

                Thread.Sleep(40);
                continue;
            }

            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.L && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                return "cls";

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString().Trim();
            }

            if (key.Key == ConsoleKey.Escape)
                return string.Empty;

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
                    RedrawInputLine();
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

                RedrawInputLine();
                continue;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                    Console.Write("\b \b");
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
                Console.Write(key.KeyChar);
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

    public static void UseBarCursor()
    {
        try
        {
            if (!Console.IsOutputRedirected)
                Console.Write("\u001b[5 q");
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
                Console.Write("\u001b[0 q");
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

    private static IRenderable BuildPlayerHeader(PlayerSnapshot snapshot)
    {
        var title = new FigletText("PopMark")
            .Centered()
            .Color(Color.DeepPink1);

        var spinner = snapshot.Status switch
        {
            PlaybackStatus.Playing => SpinnerFrame("Playing"),
            PlaybackStatus.Paused => $"[{Accent}]Paused[/]",
            PlaybackStatus.Loading => SpinnerFrame("Loading"),
            _ => $"[{Muted}]Idle[/]"
        };

        return new Rows(
            title,
            Align.Center(new Markup($"{spinner} [{Muted}]queue:[/] [white]{snapshot.Pending.Count}[/]")));
    }

    private static IRenderable BuildNowPlayingPanel(PlayerSnapshot snapshot, string notice)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().Width(16))
            .AddColumn();

        grid.AddRow($"[{Muted}]State[/]", StatusMarkup(snapshot.Status));
        grid.AddRow($"[{Muted}]Track[/]", snapshot.Current is null
            ? $"[{Muted}]Drop in a link with add <url>[/]"
            : $"[bold white]{Markup.Escape(snapshot.Current.Title)}[/]");
        grid.AddRow($"[{Muted}]Motion[/]", BuildEqualizer(snapshot.Status));
        grid.AddRow($"[{Muted}]Message[/]", $"[silver]{Markup.Escape(notice)}[/]");
        grid.AddRow($"[{Muted}]Controls[/]", $"[{Accent}]add[/] [{Muted}]|[/] [{Secondary}]play/pause[/] [{Muted}]|[/] [{Secondary}]next[/] [{Muted}]|[/] [{Accent}]cls[/] [{Muted}]|[/] [{Accent}]q[/]");

        return new Panel(grid)
            .Border(BoxBorder.Double)
            .BorderStyle(Style.Parse(PanelBorder))
            .Header($"[bold {Accent}]Now Playing[/]")
            .Expand();
    }

    private static IRenderable BuildQueueTable(PlayerSnapshot snapshot)
    {
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.DeepPink1)
            .Expand()
            .AddColumn(new TableColumn($"[bold {Accent}]#[/]").Width(5))
            .AddColumn(new TableColumn($"[bold {Secondary}]Track[/]"))
            .AddColumn(new TableColumn($"[bold {Accent}]Source[/]"));

        if (snapshot.Current is not null)
        {
            table.AddRow($"[bold {SuccessColor}]now[/]", Markup.Escape(snapshot.Current.Title), $"[{SuccessColor}]playing[/]");
        }

        var index = 1;
        foreach (var track in snapshot.Pending.Take(12))
        {
            table.AddRow(index.ToString(), Markup.Escape(track.Title), Markup.Escape(track.DisplaySource));
            index++;
        }

        if (snapshot.Current is null && snapshot.Pending.Count == 0)
            table.AddRow($"[{Muted}]-[/]", $"[{Muted}]Queue is empty[/]", $"[{Muted}]add <url>[/]");

        if (snapshot.Pending.Count > 12)
            table.AddRow($"[{Muted}]...[/]", $"[{Muted}]{snapshot.Pending.Count - 12} more track(s)[/]", $"[{Muted}]hidden[/]");

        return table;
    }

    private static IRenderable BuildHelpTable()
    {
        var table = new Table()
            .NoBorder()
            .Expand()
            .AddColumn($"[bold {Accent}]Command[/]")
            .AddColumn($"[bold {Secondary}]Action[/]")
            .AddColumn($"[bold {Accent}]Alias[/]");

        table.AddRow("add <url>", "Add a YouTube video or playlist", "a");
        table.AddRow("play / pause", "Toggle playback", "r");
        table.AddRow("next", "Skip to the next queued track", "n");
        table.AddRow("cls", "Clear and redraw the command center", "clear / Ctrl+L");
        table.AddRow("mini", "Toggle compact player view", "m");
        table.AddRow("quit", "Stop playback and exit", "q");

        return table;
    }

    private static string StatusMarkup(PlaybackStatus status) =>
        status switch
        {
            PlaybackStatus.Playing => $"{SpinnerFrame("Playing")}",
            PlaybackStatus.Paused => $"[{Accent}]Paused[/]",
            PlaybackStatus.Loading => $"{SpinnerFrame("Loading")}",
            PlaybackStatus.Detached => $"[{Secondary}]Detached[/]",
            _ => $"[{Muted}]Stopped[/]"
        };

    private static string SpinnerFrame(string label)
    {
        var frames = new[] { "o", "O", "@", "O" };
        var frame = frames[(Environment.TickCount64 / 160) % frames.Length];
        return $"[{SuccessColor}]{frame}[/] [white]{label}[/]";
    }

    private static string BuildEqualizer(PlaybackStatus status)
    {
        if (status == PlaybackStatus.Paused)
            return $"[{Accent}]▁ ▁ ▁ ▁ ▁ ▁ ▁ ▁[/]";

        if (status is not (PlaybackStatus.Playing or PlaybackStatus.Loading))
            return $"[{Chrome}]▁ ▁ ▁ ▁ ▁ ▁ ▁ ▁[/]";

        var bars = new[] { "▁", "▂", "▃", "▄", "▅", "▆", "▇" };
        var offset = (int)((Environment.TickCount64 / 120) % bars.Length);
        var output = Enumerable.Range(0, 12)
            .Select(i => bars[(i + offset + (i % 3)) % bars.Length]);

        return $"[{SuccessColor}]{string.Join(' ', output)}[/]";
    }

    private static void RenderPrompt(string input)
    {
        AnsiConsole.Markup($"[bold {Accent}]popmark[/][{Muted}] >[/] ");
        Console.Write(input);
    }

    private static string? BuildScreenSignature(
        Func<PlayerSnapshot>? snapshotProvider,
        Func<string>? noticeProvider,
        Func<bool>? miniModeProvider)
    {
        if (snapshotProvider is null || noticeProvider is null)
            return null;

        var snapshot = snapshotProvider();
        var builder = new StringBuilder()
            .Append(miniModeProvider?.Invoke() == true ? "mini" : "full")
            .Append('|')
            .Append(snapshot.Status)
            .Append('|')
            .Append(noticeProvider())
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
        const int width = 52;
        const int height = 18;
        var canvas = new Canvas(width, height);
        var bob = Math.Sin(frame * 0.18) * 0.55;
        var body = Color.DeepPink1;
        var label = Color.Grey93;
        var accent = frame % 18 < 9 ? Color.SpringGreen1 : Color.Cyan1;
        var dark = Color.Grey23;

        FillRect(canvas, 7, 4 + bob, 38, 11, body);
        FillRect(canvas, 10, 6 + bob, 32, 4, label);
        FillRect(canvas, 16, 12 + bob, 23, 2, dark);

        DrawEllipse(canvas, 19, 10 + bob, 4.0, 2.4, dark);
        DrawEllipse(canvas, 34, 10 + bob, 4.0, 2.4, dark);
        DrawEllipse(canvas, 19, 10 + bob, 1.6, 0.9, accent);
        DrawEllipse(canvas, 34, 10 + bob, 1.6, 0.9, accent);

        var spin = frame % 6;
        DrawLine(canvas, 19, 10 + bob, 19 + Math.Cos(spin) * 3, 10 + bob + Math.Sin(spin) * 1.5, label);
        DrawLine(canvas, 34, 10 + bob, 34 - Math.Cos(spin) * 3, 10 + bob - Math.Sin(spin) * 1.5, label);

        DrawPixelBlock(canvas, 24, (int)Math.Round(8 + bob), dark);
        DrawPixelBlock(canvas, 29, (int)Math.Round(8 + bob), dark);
        DrawLine(canvas, 26, 9 + bob, 28, 9 + bob, dark);

        DrawLine(canvas, 3, 7 + bob, 6, 5 + bob, accent);
        DrawLine(canvas, 46, 6 + bob, 50, 4 + bob, accent);
        DrawLine(canvas, 46, 9 + bob, 50, 9 + bob, accent);

        DrawTwinkle(canvas, frame, 4, 3, 0);
        DrawTwinkle(canvas, frame, 47, 14, 3);
        DrawTwinkle(canvas, frame, 27, 2, 5);

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

    [SupportedOSPlatform("windows")]
    private static ConsoleWindowState? CaptureConsoleWindowState()
    {
        var handle = GetConsoleWindow();
        if (handle == 0 || !GetWindowRect(handle, out var rect))
            return null;

        return new ConsoleWindowState(
            handle,
            rect,
            Console.WindowWidth,
            Console.WindowHeight,
            Console.BufferWidth,
            Console.BufferHeight);
    }

    [SupportedOSPlatform("windows")]
    private static void RestoreConsoleWindowState(ConsoleWindowState state)
    {
        TrySetConsoleSize(state.WindowColumns, state.WindowRows, state.BufferColumns, state.BufferRows);
        var width = Math.Max(1, state.WindowRect.Right - state.WindowRect.Left);
        var height = Math.Max(1, state.WindowRect.Bottom - state.WindowRect.Top);
        _ = SetWindowPos(
            state.Handle,
            0,
            state.WindowRect.Left,
            state.WindowRect.Top,
            width,
            height,
            SwpNoZOrder | SwpNoActivate);
    }

    [SupportedOSPlatform("windows")]
    private static void TrySetConsoleSize(int columns, int rows, int? bufferColumns = null, int? bufferRows = null)
    {
        try
        {
            columns = Math.Clamp(columns, 20, Console.LargestWindowWidth);
            rows = Math.Clamp(rows, 8, Console.LargestWindowHeight);
            Console.SetBufferSize(Math.Max(bufferColumns ?? columns, columns), Math.Max(bufferRows ?? rows, rows));
            Console.SetWindowSize(columns, rows);
        }
        catch
        {
        }
    }

    [SupportedOSPlatform("windows")]
    private static void TryDockConsoleWindow(int columns, int rows)
    {
        var handle = GetConsoleWindow();
        if (handle == 0 || !GetWindowRect(handle, out var currentRect))
            return;

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref monitorInfo))
            return;

        var currentWidth = Math.Max(1, currentRect.Right - currentRect.Left);
        var currentHeight = Math.Max(1, currentRect.Bottom - currentRect.Top);
        var currentColumns = Math.Max(1, Console.WindowWidth);
        var currentRows = Math.Max(1, Console.WindowHeight);
        var width = Math.Max(360, (int)Math.Round(currentWidth * (columns / (double)currentColumns)));
        var height = Math.Max(190, (int)Math.Round(currentHeight * (rows / (double)currentRows)));
        var workArea = monitorInfo.WorkArea;
        var margin = 18;
        var x = workArea.Right - width - margin;
        var y = workArea.Bottom - height - margin;

        _ = SetWindowPos(handle, 0, x, y, width, height, SwpNoZOrder | SwpNoActivate);
    }

    private static void TryClear()
    {
        try
        {
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

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Rect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    private readonly record struct ConsoleWindowState(
        nint Handle,
        Rect WindowRect,
        int WindowColumns,
        int WindowRows,
        int BufferColumns,
        int BufferRows);
}
