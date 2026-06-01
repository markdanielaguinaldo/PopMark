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

    public static string ReadReactiveInput(
        ref int lastWidth,
        ref int lastHeight,
        List<string> commandHistory,
        Func<PlayerSnapshot>? snapshotProvider = null,
        Func<string>? noticeProvider = null)
    {
        var buffer = new StringBuilder();
        var historyIndex = commandHistory.Count;
        var browsingHistory = false;
        var draftInput = string.Empty;
        var lastRefresh = DateTimeOffset.UtcNow;
        RenderPrompt(buffer.ToString());

        void RefreshScreen()
        {
            if (snapshotProvider is null || noticeProvider is null)
                return;

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
                if ((DateTimeOffset.UtcNow - lastRefresh).TotalMilliseconds >= 180)
                {
                    lastRefresh = DateTimeOffset.UtcNow;
                    RefreshScreen();
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
            .Color(Color.MediumPurple1);

        var spinner = snapshot.Status switch
        {
            PlaybackStatus.Playing => SpinnerFrame("Playing"),
            PlaybackStatus.Paused => "[yellow]Paused[/]",
            PlaybackStatus.Loading => SpinnerFrame("Loading"),
            _ => "[grey]Idle[/]"
        };

        return new Rows(
            title,
            Align.Center(new Markup($"{spinner} [grey]queue:[/] [white]{snapshot.Pending.Count}[/]")));
    }

    private static IRenderable BuildNowPlayingPanel(PlayerSnapshot snapshot, string notice)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().Width(16))
            .AddColumn();

        grid.AddRow("[grey]State[/]", StatusMarkup(snapshot.Status));
        grid.AddRow("[grey]Track[/]", snapshot.Current is null
            ? "[grey]Drop in a link with add <url>[/]"
            : $"[bold white]{Markup.Escape(snapshot.Current.Title)}[/]");
        grid.AddRow("[grey]Motion[/]", BuildEqualizer(snapshot.Status));
        grid.AddRow("[grey]Message[/]", $"[silver]{Markup.Escape(notice)}[/]");
        grid.AddRow("[grey]Controls[/]", "[mediumorchid1]add[/] [grey]|[/] [mediumorchid1]play/pause[/] [grey]|[/] [mediumorchid1]next[/] [grey]|[/] [mediumorchid1]cls[/] [grey]|[/] [mediumorchid1]q[/]");

        return new Panel(grid)
            .Border(BoxBorder.Double)
            .BorderStyle(new Style(Color.MediumPurple1))
            .Header("[bold mediumorchid1]Now Playing[/]")
            .Expand();
    }

    private static IRenderable BuildQueueTable(PlayerSnapshot snapshot)
    {
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.Grey50)
            .Expand()
            .AddColumn(new TableColumn("[bold mediumorchid1]#[/]").Width(5))
            .AddColumn(new TableColumn("[bold]Track[/]"))
            .AddColumn(new TableColumn("[bold]Source[/]"));

        if (snapshot.Current is not null)
        {
            table.AddRow("[bold green]now[/]", Markup.Escape(snapshot.Current.Title), "[green]playing[/]");
        }

        var index = 1;
        foreach (var track in snapshot.Pending.Take(12))
        {
            table.AddRow(index.ToString(), Markup.Escape(track.Title), Markup.Escape(track.DisplaySource));
            index++;
        }

        if (snapshot.Current is null && snapshot.Pending.Count == 0)
            table.AddRow("[grey]-[/]", "[grey]Queue is empty[/]", "[grey]add <url>[/]");

        if (snapshot.Pending.Count > 12)
            table.AddRow("[grey]...[/]", $"[grey]{snapshot.Pending.Count - 12} more track(s)[/]", "[grey]hidden[/]");

        return table;
    }

    private static IRenderable BuildHelpTable()
    {
        var table = new Table()
            .NoBorder()
            .Expand()
            .AddColumn("[bold mediumorchid1]Command[/]")
            .AddColumn("[bold]Action[/]")
            .AddColumn("[bold mediumorchid1]Alias[/]");

        table.AddRow("add <url>", "Add a YouTube video or playlist", "a");
        table.AddRow("play / pause", "Toggle playback", "r");
        table.AddRow("next", "Skip to the next queued track", "n");
        table.AddRow("cls", "Clear and redraw the command center", "clear / Ctrl+L");
        table.AddRow("quit", "Stop playback and exit", "q");

        return table;
    }

    private static string StatusMarkup(PlaybackStatus status) =>
        status switch
        {
            PlaybackStatus.Playing => $"{SpinnerFrame("Playing")}",
            PlaybackStatus.Paused => "[yellow]Paused[/]",
            PlaybackStatus.Loading => $"{SpinnerFrame("Loading")}",
            PlaybackStatus.Detached => "[blue]Detached[/]",
            _ => "[grey]Stopped[/]"
        };

    private static string SpinnerFrame(string label)
    {
        var frames = new[] { "o", "O", "@", "O" };
        var frame = frames[(Environment.TickCount64 / 160) % frames.Length];
        return $"[springgreen1]{frame}[/] [white]{label}[/]";
    }

    private static string BuildEqualizer(PlaybackStatus status)
    {
        if (status == PlaybackStatus.Paused)
            return "[yellow]▁ ▁ ▁ ▁ ▁ ▁ ▁ ▁[/]";

        if (status is not (PlaybackStatus.Playing or PlaybackStatus.Loading))
            return "[grey]▁ ▁ ▁ ▁ ▁ ▁ ▁ ▁[/]";

        var bars = new[] { "▁", "▂", "▃", "▄", "▅", "▆", "▇" };
        var offset = (int)((Environment.TickCount64 / 120) % bars.Length);
        var output = Enumerable.Range(0, 12)
            .Select(i => bars[(i + offset + (i % 3)) % bars.Length]);

        return $"[springgreen1]{string.Join(' ', output)}[/]";
    }

    private static void RenderPrompt(string input)
    {
        AnsiConsole.Markup("[bold mediumorchid1]popmark[/][grey] >[/] ");
        Console.Write(input);
    }

    private static IRenderable CreateSplashFrame(int frame)
    {
        var footer = new Rows(
            Align.Center(new Markup("[bold mediumorchid1]PopMark[/] [silver]terminal music queue[/]")),
            Align.Center(new Markup("[bold springgreen1]Press any key to continue[/]")));

        return new Rows(
            new Text(" "),
            new Text(" "),
            Align.Center(CreateCassetteCanvas(frame)),
            new Text(" "),
            footer);
    }

    private static Canvas CreateCassetteCanvas(int frame)
    {
        const int width = 70;
        const int height = 24;
        var canvas = new Canvas(width, height);
        var bob = Math.Sin(frame * 0.18) * 0.8;
        var body = Color.MediumPurple1;
        var label = Color.Cornsilk1;
        var accent = frame % 18 < 9 ? Color.SpringGreen1 : Color.Turquoise2;
        var dark = Color.Grey23;

        FillRect(canvas, 10, 5 + bob, 49, 15, body);
        FillRect(canvas, 13, 8 + bob, 43, 5, label);
        FillRect(canvas, 20, 16 + bob, 29, 3, dark);

        DrawEllipse(canvas, 23, 14 + bob, 5.3, 3.3, dark);
        DrawEllipse(canvas, 46, 14 + bob, 5.3, 3.3, dark);
        DrawEllipse(canvas, 23, 14 + bob, 2.1, 1.2, accent);
        DrawEllipse(canvas, 46, 14 + bob, 2.1, 1.2, accent);

        var spin = frame % 6;
        DrawLine(canvas, 23, 14 + bob, 23 + Math.Cos(spin) * 4, 14 + bob + Math.Sin(spin) * 2, label);
        DrawLine(canvas, 46, 14 + bob, 46 - Math.Cos(spin) * 4, 14 + bob - Math.Sin(spin) * 2, label);

        DrawPixelBlock(canvas, 30, (int)Math.Round(10 + bob), dark);
        DrawPixelBlock(canvas, 39, (int)Math.Round(10 + bob), dark);
        DrawLine(canvas, 33, 12 + bob, 36, 12 + bob, dark);

        DrawLine(canvas, 5, 9 + bob, 9, 7 + bob, accent);
        DrawLine(canvas, 61, 8 + bob, 66, 5 + bob, accent);
        DrawLine(canvas, 61, 11 + bob, 67, 11 + bob, accent);

        DrawTwinkle(canvas, frame, 7, 4, 0);
        DrawTwinkle(canvas, frame, 63, 18, 3);
        DrawTwinkle(canvas, frame, 35, 2, 5);

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
                Console.Write("\u001b[H\u001b[2J\u001b[3J");

            AnsiConsole.Clear();
        }
        catch
        {
            AnsiConsole.Clear();
        }
    }
}
