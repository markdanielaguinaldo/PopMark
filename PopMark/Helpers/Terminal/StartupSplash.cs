using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;

namespace PopMark.Helpers.Terminal;

/// <summary>
/// Full-screen startup splash. It uses the same component pipeline as the main
/// screen, but owns its animation clock because the application loop has not
/// started yet.
/// </summary>
internal static class StartupSplash
{
    private const int SplashFrameMilliseconds = 150;

    public static void Show()
    {
        if (Console.IsOutputRedirected)
            return;

        TerminalHost.Clear();
        TerminalHost.HideCursor();

        ScreenBuffer? previous = null;

        try
        {
            while (!Console.KeyAvailable)
            {
                var (width, height) = TerminalHost.GetWindowSize();
                width = width > 0 ? width : 80;
                height = height > 0 ? height : 24;

                var frame = (int)(Environment.TickCount64 / SplashFrameMilliseconds);
                var context = new RenderContext(
                    width,
                    height,
                    EmptySnapshot(),
                    string.Empty,
                    string.Empty,
                    ShowHelp: false,
                    ShowControls: false,
                    MiniMode: false,
                    AnimationFrame: frame,
                    QueueScrollOffset: 0);

                var buffer = new ScreenBuffer(width, height);
                var uiContext = new UiRenderContext(
                    context,
                    UiTheme.Default,
                    new HitRegistry(),
                    TimeSpan.FromMilliseconds(frame * SplashFrameMilliseconds));

                new SplashViewComponent(context, playbackMode: false)
                    .Render(buffer, new UiRect(0, 0, width, height), uiContext);

                Console.Write(ScreenComposer.Compose(
                    previous,
                    buffer,
                    forceFullPaint: false,
                    useColor: true));

                previous = buffer;
                Thread.Sleep(40);
            }

            _ = Console.ReadKey(intercept: true);
        }
        finally
        {
            TerminalHost.ShowCursor();
            TerminalHost.Clear();
        }
    }

    private static PlayerSnapshot EmptySnapshot() =>
        new(
            PlaybackStatus.Stopped,
            null,
            Array.Empty<Track>(),
            Array.Empty<Track>(),
            null,
            100);
}
