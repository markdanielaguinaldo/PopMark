using Spectre.Console;
using System.Runtime.InteropServices;
using System.Text;

namespace PopMark.Helpers.Terminal;

internal static class TerminalHost
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
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

    public static void EnterInteractiveScreen()
    {
        if (Console.IsOutputRedirected || _usingAlternateScreen)
            return;

        Console.Write("\u001b[?1049h\u001b[?25l\u001b[H\u001b[2J");
        _usingAlternateScreen = true;
        TerminalFrameRenderer.ResetFrameCache();
    }

    public static void LeaveInteractiveScreen()
    {
        if (Console.IsOutputRedirected || !_usingAlternateScreen)
            return;

        TerminalFrameRenderer.ResetFrameCache();
        Console.Write("\u001b[?25h\u001b[?1049l");
        _usingAlternateScreen = false;
    }

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

    public static void Clear()
    {
        try
        {
            TerminalFrameRenderer.ResetFrameCache();

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
}
