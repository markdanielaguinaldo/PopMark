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

        var title = new FigletText("PopMark")
            .Centered()
            .Color(Color.Cornsilk1);

        AnsiConsole.Write(title);
        AnsiConsole.Write(BuildStatusPanel(snapshot, notice));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildQueueTable(snapshot));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildHelpTable());
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    public static string ReadReactiveInput(ref int lastWidth, ref int lastHeight, List<string> commandHistory)
    {
        var buffer = new StringBuilder();
        var historyIndex = commandHistory.Count;
        var browsingHistory = false;
        var draftInput = string.Empty;
        RenderPrompt(buffer.ToString());

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
                Thread.Sleep(40);
                continue;
            }

            var key = Console.ReadKey(intercept: true);

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

    private static IRenderable BuildStatusPanel(PlayerSnapshot snapshot, string notice)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().Width(12))
            .AddColumn();

        grid.AddRow("[grey]Status[/]", StatusMarkup(snapshot.Status));
        grid.AddRow("[grey]Current[/]", snapshot.Current is null
            ? "[grey]Nothing playing[/]"
            : $"[white]{Markup.Escape(snapshot.Current.Title)}[/]");
        grid.AddRow("[grey]Pending[/]", $"[white]{snapshot.Pending.Count} track(s)[/]");
        grid.AddRow("[grey]Notice[/]", $"[silver]{Markup.Escape(notice)}[/]");

        return new Panel(grid)
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.CadetBlue_1))
            .Header("[bold pink1]Command Center[/]")
            .Expand();
    }

    private static IRenderable BuildQueueTable(PlayerSnapshot snapshot)
    {
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.Grey50)
            .Expand()
            .AddColumn(new TableColumn("[bold pink1]#[/]").Width(5))
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
            .AddColumn("[bold pink1]Command[/]")
            .AddColumn("[bold]Action[/]")
            .AddColumn("[bold pink1]Alias[/]");

        table.AddRow("add <url>", "Load a YouTube video or playlist with yt-dlp", "a");
        table.AddRow("pause / resume", "Pause or resume mpv playback", "p / r");
        table.AddRow("toggle", "Toggle pause state", "t");
        table.AddRow("next", "Skip to the next queued track", "n");
        table.AddRow("stop", "Stop playback and clear the queue", "s");
        table.AddRow("detach", "Exit the TUI and leave mpv playing", "d");
        table.AddRow("quit", "Stop playback and exit", "q");

        return table;
    }

    private static string StatusMarkup(PlaybackStatus status) =>
        status switch
        {
            PlaybackStatus.Playing => "[green]Playing[/]",
            PlaybackStatus.Paused => "[yellow]Paused[/]",
            PlaybackStatus.Loading => "[cyan]Loading[/]",
            PlaybackStatus.Detached => "[blue]Detached[/]",
            _ => "[grey]Stopped[/]"
        };

    private static void RenderPrompt(string input)
    {
        AnsiConsole.Markup("[bold pink1]popmark[/][grey] >[/] ");
        Console.Write(input);
    }

    private static IRenderable CreateSplashFrame(int frame)
    {
        var footer = new Rows(
            Align.Center(new Markup("[bold pink1]PopMark[/] [silver]terminal music queue[/]")),
            Align.Center(new Markup("[bold springgreen1]Press any key to continue[/]")));

        return new Rows(
            new Text(" "),
            new Text(" "),
            Align.Center(CreateCatCanvas(frame)),
            new Text(" "),
            footer);
    }

    private static Canvas CreateCatCanvas(int frame)
    {
        const int width = 64;
        const int height = 24;
        var canvas = new Canvas(width, height);
        var bob = Math.Sin(frame * 0.22) * 1.1;
        var breathe = Math.Sin(frame * 0.15) * 0.7;
        var bodyColor = frame % 28 < 14 ? Color.Cornsilk1 : Color.Grey70;
        var accent = frame % 20 < 10 ? Color.Turquoise2 : Color.CadetBlue_1;
        var blush = Color.CadetBlue_1;

        DrawEllipse(canvas, 32, 14 + bob, 14.5 + breathe, 7.0, bodyColor);
        DrawEllipse(canvas, 32, 7 + bob, 10.5, 5.7, bodyColor);

        DrawTriangle(canvas, 23, 5 + bob, 27, 1 + bob, 29, 6 + bob, bodyColor);
        DrawTriangle(canvas, 35, 6 + bob, 38, 1 + bob, 42, 5 + bob, bodyColor);
        DrawTriangle(canvas, 25, 5 + bob, 27, 3 + bob, 28, 6 + bob, accent);
        DrawTriangle(canvas, 37, 6 + bob, 38, 3 + bob, 40, 5 + bob, accent);

        DrawEllipse(canvas, 16, 14 + bob, 6.5, 2.5, bodyColor);
        DrawEllipse(canvas, 13, 12 + bob, 3.0, 1.8, accent);
        DrawEllipse(canvas, 27, 20 + bob, 3.3, 1.5, accent);
        DrawEllipse(canvas, 37, 20 + bob, 3.3, 1.5, accent);

        DrawPixelBlock(canvas, 28, (int)Math.Round(7 + bob), Color.Grey35);
        DrawPixelBlock(canvas, 36, (int)Math.Round(7 + bob), Color.Grey35);
        DrawPixelBlock(canvas, 32, (int)Math.Round(9 + bob), Color.Grey35);
        DrawPixelBlock(canvas, 29, (int)Math.Round(11 + bob), blush);
        DrawPixelBlock(canvas, 38, (int)Math.Round(11 + bob), blush);

        DrawLine(canvas, 20, 10 + bob, 27, 10 + bob, accent);
        DrawLine(canvas, 20, 12 + bob, 27, 11 + bob, accent);
        DrawLine(canvas, 44, 10 + bob, 37, 10 + bob, accent);
        DrawLine(canvas, 44, 12 + bob, 37, 11 + bob, accent);

        DrawTwinkle(canvas, frame, 9, 4, 0);
        DrawTwinkle(canvas, frame, 52, 5, 3);
        DrawTwinkle(canvas, frame, 10, 19, 5);
        DrawTwinkle(canvas, frame, 54, 18, 1);

        return canvas;
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
