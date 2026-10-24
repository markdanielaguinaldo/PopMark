using PopMark.Helpers.Terminal;
using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;

namespace PopMark.Tests;

internal static class TestUi
{
    public static Track Track(string title, string url = "https://example.test/video", int durationSeconds = 180) =>
        new(title, url, "YouTube", TimeSpan.FromSeconds(durationSeconds));

    public static PlayerSnapshot Snapshot(
        PlaybackStatus status = PlaybackStatus.Playing,
        Track? current = null,
        IReadOnlyList<Track>? pending = null,
        IReadOnlyList<Track>? previous = null,
        TimeSpan? elapsed = null,
        int volume = 100) =>
        new(
            status,
            current,
            pending ?? Array.Empty<Track>(),
            previous ?? Array.Empty<Track>(),
            elapsed,
            volume);

    public static RenderContext Context(
        PlayerSnapshot snapshot,
        int width = 100,
        int height = 30,
        string input = "",
        int caret = 0,
        bool mini = false,
        bool splash = false,
        bool help = false,
        bool controls = false,
        int scroll = 0,
        int frame = 0) =>
        new(
            width,
            height,
            snapshot,
            "Ready",
            input,
            help,
            controls,
            mini,
            frame,
            scroll,
            splash,
            caret);

    public static (ScreenBuffer Screen, UiRenderContext UiContext) Render(
        UiComponent component,
        RenderContext context,
        int? width = null,
        int? height = null)
    {
        var w = width ?? context.Width;
        var h = height ?? context.Height;
        var screen = new ScreenBuffer(w, h);
        var hits = new HitRegistry();
        var uiContext = new UiRenderContext(
            context,
            UiTheme.Default,
            hits,
            TimeSpan.FromMilliseconds(context.AnimationFrame * 110d));
        component.Render(screen, new UiRect(0, 0, w, h), uiContext);
        return (screen, uiContext);
    }

    public static IReadOnlyList<string> Lines(ScreenBuffer buffer) => buffer.ToLines();
}
