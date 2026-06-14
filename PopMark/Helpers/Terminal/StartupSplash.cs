using Spectre.Console;
using Spectre.Console.Rendering;
using System.Reflection;

namespace PopMark.Helpers.Terminal;

internal static class StartupSplash
{
    private const int SplashFrameMilliseconds = 150;

    public static void Show()
    {
        TerminalHost.Clear();
        AnsiConsole.Cursor.Hide();

        try
        {
            AnsiConsole.Live(CreateSplashFrame(0))
                .Overflow(VerticalOverflow.Crop)
                .Start(ctx =>
                {
                    while (!Console.KeyAvailable)
                    {
                        var frame = (int)(Environment.TickCount64 / SplashFrameMilliseconds);
                        ctx.UpdateTarget(CreateSplashFrame(frame));
                        Thread.Sleep(40);
                    }

                    _ = Console.ReadKey(intercept: true);
                });
        }
        finally
        {
            AnsiConsole.Cursor.Show();
            TerminalHost.Clear();
        }
    }

    private static IRenderable CreateSplashFrame(int frame)
    {
        var (width, height) = TerminalHost.GetWindowSize();
        return SplashScreen.Render(
            frame,
            AppVersion(),
            width > 0 ? width : 80,
            height > 0 ? height : 24);
    }

    private static string AppVersion() =>
        typeof(StartupSplash).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ??
        typeof(StartupSplash).Assembly.GetName().Version?.ToString() ??
        "unknown";
}
