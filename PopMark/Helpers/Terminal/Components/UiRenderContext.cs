using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;

namespace PopMark.Helpers.Terminal.Components;

/// <summary>
/// Per-frame data passed to components. The application snapshot and input buffer
/// are supplied by the existing render context; hitboxes and the caret are
/// collected while drawing.
/// </summary>
internal sealed class UiRenderContext
{
    public UiRenderContext(RenderContext app, UiTheme theme, HitRegistry hits, TimeSpan animationTime)
    {
        App = app;
        Theme = theme;
        Hits = hits;
        AnimationTime = animationTime;
    }

    public RenderContext App { get; }

    public UiTheme Theme { get; }

    public HitRegistry Hits { get; }

    public TimeSpan AnimationTime { get; }

    public UiCaret? Caret { get; set; }

    public PlayerSnapshot Snapshot => App.Snapshot;

    public string Notice => App.Notice;

    public string Input => App.Input;

    public int AnimationFrame => App.AnimationFrame;
}

/// <summary>Zero-based caret position in the screen buffer.</summary>
internal readonly record struct UiCaret(int X, int Y);
