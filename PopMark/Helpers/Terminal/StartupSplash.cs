using Spectre.Console;
using Spectre.Console.Rendering;

namespace PopMark.Helpers.Terminal;

internal static class StartupSplash
{
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
            TerminalHost.Clear();
        }
    }

    private static IRenderable CreateSplashFrame(int frame)
    {
        var pulse = frame % 12 < 6 ? "deepskyblue1" : "mediumpurple1";
        var footer = new Rows(
            Align.Center(new Markup($"[bold {TerminalStyles.Accent}]PopMark[/] [silver]terminal music queue[/]")),
            Align.Center(new Markup($"[bold {pulse}]Press any key to continue[/]")));

        return new Rows(
            new Text(" "),
            Align.Center(CreateRobotDjCanvas(frame)),
            new Text(" "),
            footer);
    }

    private static Canvas CreateRobotDjCanvas(int frame)
    {
        const int width = 92;
        const int height = 32;
        var canvas = new Canvas(width, height);
        var accent = frame % 14 < 7 ? Color.DeepSkyBlue1 : Color.Cyan1;
        var glow = frame % 10 < 5 ? Color.Cyan1 : Color.Turquoise2;
        var panel = Color.Grey93;
        var dark = Color.Grey15;
        var shadow = Color.Grey27;
        var bob = frame % 18 < 9 ? 0 : 1;

        DrawSpark(canvas, 7, 4, frame, 0, accent);
        DrawSpark(canvas, 72, 4, frame, 5, Color.Grey70);
        DrawSpark(canvas, 83, 9, frame, 8, accent);
        DrawSpark(canvas, 9, 24, frame, 11, Color.Grey70);
        DrawSpark(canvas, 78, 25, frame, 3, Color.Grey70);

        DrawFrame(canvas, 12, 6, 68, 20, accent, shadow);

        DrawMusicNote(canvas, 22, 11, accent);
        DrawMusicNote(canvas, 67, 11, accent);
        DrawEqualizer(canvas, 17, 23, frame, accent);
        DrawEqualizer(canvas, 67, 23, frame + 3, accent);
        DrawWaveFloor(canvas, 29, 26, frame, accent, shadow);

        DrawRobot(canvas, 34, 12 + bob, frame, accent, glow, panel, dark, shadow);
        DrawLoadingDots(canvas, 33, 29, frame, accent, shadow);

        return canvas;
    }

    private static void DrawFrame(Canvas canvas, int left, int top, int width, int height, Color accent, Color shadow)
    {
        FillRect(canvas, left + 1, top, width - 2, 1, accent);
        FillRect(canvas, left + 1, top + height - 1, width - 2, 1, accent);
        FillRect(canvas, left, top + 1, 1, height - 2, accent);
        FillRect(canvas, left + width - 1, top + 1, 1, height - 2, accent);

        SafeSetPixel(canvas, left + 1, top + 1, accent);
        SafeSetPixel(canvas, left + width - 2, top + 1, accent);
        SafeSetPixel(canvas, left + 1, top + height - 2, accent);
        SafeSetPixel(canvas, left + width - 2, top + height - 2, accent);
        FillRect(canvas, left + 2, top + height, width - 4, 1, shadow);
    }

    private static void DrawRobot(Canvas canvas, int left, int top, int frame, Color accent, Color glow, Color panel, Color dark, Color shadow)
    {
        FillRect(canvas, left + 15, top - 5, 2, 5, accent);
        FillRect(canvas, left + 8, top - 2, 18, 2, accent);
        FillRect(canvas, left + 6, top, 4, 8, accent);
        FillRect(canvas, left + 24, top, 4, 8, accent);
        FillRect(canvas, left + 4, top + 2, 5, 7, Color.DeepSkyBlue1);
        FillRect(canvas, left + 25, top + 2, 5, 7, Color.DeepSkyBlue1);
        FillRect(canvas, left + 5, top + 3, 2, 4, glow);
        FillRect(canvas, left + 27, top + 3, 2, 4, glow);

        FillRect(canvas, left + 9, top + 1, 16, 10, panel);
        FillRect(canvas, left + 11, top + 3, 12, 6, dark);
        FillRect(canvas, left + 13, top + 5, 2, 2, glow);
        FillRect(canvas, left + 19, top + 5, 2, 2, glow);
        FillRect(canvas, left + 10, top + 2, 14, 1, Color.White);
        FillRect(canvas, left + 10, top + 10, 14, 1, Color.Grey70);

        FillRect(canvas, left + 12, top + 11, 10, 4, dark);
        FillRect(canvas, left + 14, top + 12, 2, 2, panel);
        FillRect(canvas, left + 18, top + 12, 2, 2, panel);
        FillRect(canvas, left + 13, top + 15, 8, 1, accent);

        FillRect(canvas, left + 3, top + 17, 10, 2, shadow);
        FillRect(canvas, left + 21, top + 17, 10, 2, shadow);
        FillRect(canvas, left + 6, top + 16, 4, 2, dark);
        FillRect(canvas, left + 24, top + 16, 4, 2, dark);
        if (frame % 12 < 6)
        {
            FillRect(canvas, left + 7, top + 16, 2, 1, accent);
            FillRect(canvas, left + 25, top + 16, 2, 1, accent);
        }
    }

    private static void DrawEqualizer(Canvas canvas, int left, int bottom, int frame, Color accent)
    {
        for (var i = 0; i < 5; i++)
        {
            var height = 2 + ((frame + i * 2) % 5);
            FillRect(canvas, left + i * 3, bottom - height, 2, height, i % 2 == 0 ? accent : Color.CadetBlue);
        }
    }

    private static void DrawWaveFloor(Canvas canvas, int left, int y, int frame, Color accent, Color shadow)
    {
        for (var i = 0; i < 34; i++)
        {
            var active = (i + frame / 2) % 5 == 0;
            SafeSetPixel(canvas, left + i, y + (active ? 0 : 1), active ? accent : shadow);
        }
    }

    private static void DrawLoadingDots(Canvas canvas, int left, int y, int frame, Color accent, Color shadow)
    {
        for (var i = 0; i < 24; i++)
            SafeSetPixel(canvas, left + i, y, i < (frame % 24) ? accent : shadow);
    }

    private static void DrawMusicNote(Canvas canvas, int x, int y, Color color)
    {
        FillRect(canvas, x, y + 2, 2, 2, color);
        FillRect(canvas, x + 2, y - 1, 1, 4, color);
        FillRect(canvas, x + 3, y - 1, 2, 1, color);
        SafeSetPixel(canvas, x + 4, y, color);
    }

    private static void DrawSpark(Canvas canvas, int x, int y, int frame, int offset, Color accent)
    {
        var active = (frame + offset) % 12 < 6;
        var color = active ? accent : Color.Grey35;
        SafeSetPixel(canvas, x, y, color);
        if (!active)
            return;

        SafeSetPixel(canvas, x - 1, y, Color.Grey35);
        SafeSetPixel(canvas, x + 1, y, Color.Grey35);
        SafeSetPixel(canvas, x, y - 1, Color.Grey35);
        SafeSetPixel(canvas, x, y + 1, Color.Grey35);
    }

    private static void FillRect(Canvas canvas, int left, int top, int width, int height, Color color)
    {
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
                SafeSetPixel(canvas, x, y, color);
        }
    }

    private static void SafeSetPixel(Canvas canvas, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= canvas.Width || y >= canvas.Height)
            return;

        canvas.SetPixel(x, y, color);
    }
}
