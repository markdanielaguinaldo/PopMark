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
    private const int MiniWidgetWidth = 46;
    private const int MiniWidgetHeight = 9;
    private const string Accent = "deepskyblue1";
    private const string Secondary = "lightskyblue1";
    private const string SuccessColor = "cyan1";
    private const string Muted = "grey70";
    private const string Chrome = "grey35";
    private const string PanelBorder = "steelblue1";

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

    public static void DrawCommandCenter(PlayerSnapshot snapshot, string notice, bool showHelp = false)
    {
        TryClear();

        AnsiConsole.Write(BuildPlayerHeader(snapshot));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildNowPlayingPanel(snapshot, notice));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildQueueTable(snapshot));
        AnsiConsole.WriteLine();
        if (showHelp)
            AnsiConsole.Write(BuildHelpTable());
        else
            AnsiConsole.Write(Align.Center(new Markup($"[{Muted}]Type [{Accent}]help[/] to see commands.[/]")));
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
        var textWidth = MiniWidgetWidth - 10;
        if (!Console.IsOutputRedirected && topPadding > 0)
            Console.Write(new string('\n', topPadding));

        var body = new Rows(
            Align.Center(new Markup($"[bold {Accent}]PopMark[/] [dim {Secondary}]// compact[/]")),
            Align.Center(new Markup(MiniStatusMarkup(snapshot.Status))),
            Align.Center(new Markup($"[white]{Markup.Escape(TrimForWidget(track, textWidth))}[/]")),
            Align.Center(new Markup($"[{Muted}]{Markup.Escape(TrimForWidget(notice, textWidth))}[/]")),
            Align.Center(new Markup($"[{Muted}]Type [{Accent}]help[/] for commands[/]")));

        var panel = new Panel(body)
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse(PanelBorder))
            .Header($"[bold {Secondary}]Compact[/]", Justify.Right)
            .Padding(1, 0);

        AnsiConsole.Write(Align.Right(panel));
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
        var buffer = new StringBuilder();
        var historyIndex = commandHistory.Count;
        var browsingHistory = false;
        var draftInput = string.Empty;
        var lastRefresh = DateTimeOffset.UtcNow;
        var lastScreenSignature = BuildScreenSignature(snapshotProvider, noticeProvider, miniModeProvider, helpModeProvider);
        RenderPrompt(buffer.ToString());

        void RefreshScreen()
        {
            if (snapshotProvider is null || noticeProvider is null)
                return;

            if (miniModeProvider?.Invoke() == true)
                DrawMiniPlayer(snapshotProvider(), noticeProvider());
            else
                DrawCommandCenter(snapshotProvider(), noticeProvider(), helpModeProvider?.Invoke() == true);
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
                    var screenSignature = BuildScreenSignature(snapshotProvider, noticeProvider, miniModeProvider, helpModeProvider);
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
        var grid = new Grid()
            .Expand()
            .AddColumn()
            .AddColumn(new GridColumn().RightAligned());

        grid.AddRow(
            $"[bold {Accent}]PopMark[/] [{Muted}]command center[/]",
            $"{StatusMarkup(snapshot.Status)} [{Muted}]queue:[/] [white]{snapshot.Pending.Count}[/]");

        return new Panel(grid)
            .Border(BoxBorder.None)
            .Padding(0, 0, 0, 0);
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
        grid.AddRow($"[{Muted}]Activity[/]", ActivityMarkup(snapshot.Status));
        grid.AddRow($"[{Muted}]Message[/]", $"[silver]{Markup.Escape(notice)}[/]");
        grid.AddRow($"[{Muted}]Hint[/]", $"Type [{Accent}]help[/] to see commands.");

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
            .BorderColor(Color.DeepSkyBlue1)
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
            PlaybackStatus.Playing => $"[{SuccessColor}]Playing[/]",
            PlaybackStatus.Paused => $"[{Accent}]Paused[/]",
            PlaybackStatus.Loading => $"[{Secondary}]Loading[/]",
            PlaybackStatus.Detached => $"[{Secondary}]Detached[/]",
            _ => $"[{Muted}]Stopped[/]"
        };

    private static string MiniStatusMarkup(PlaybackStatus status) =>
        status switch
        {
            PlaybackStatus.Playing => $"[{SuccessColor}]Playing[/]",
            PlaybackStatus.Loading => $"[{Secondary}]Loading[/]",
            PlaybackStatus.Paused => $"[{Accent}]Paused[/]",
            PlaybackStatus.Detached => $"[{Secondary}]Detached[/]",
            _ => $"[{Muted}]Stopped[/]"
        };

    private static string ActivityMarkup(PlaybackStatus status) =>
        status switch
        {
            PlaybackStatus.Playing => $"[{SuccessColor}]Active[/]",
            PlaybackStatus.Loading => $"[{Secondary}]Loading metadata[/]",
            PlaybackStatus.Paused => $"[{Accent}]Paused[/]",
            PlaybackStatus.Detached => $"[{Secondary}]Detached to mpv[/]",
            _ => $"[{Chrome}]Idle[/]"
        };

    private static void RenderPrompt(string input)
    {
        AnsiConsole.Markup($"[bold {Accent}]popmark[/][{Muted}] >[/] ");
        Console.Write(input);
    }

    private static string? BuildScreenSignature(
        Func<PlayerSnapshot>? snapshotProvider,
        Func<string>? noticeProvider,
        Func<bool>? miniModeProvider,
        Func<bool>? helpModeProvider)
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
            if (!Console.IsOutputRedirected)
            {
                Console.Write("\u001b[H\u001b[J");
                return;
            }

            AnsiConsole.Clear();
        }
        catch
        {
            AnsiConsole.Clear();
        }
    }

}
