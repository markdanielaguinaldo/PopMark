using PopMark.Helpers.Terminal.Rendering;

namespace PopMark.Helpers.Terminal.Components;

/// <summary>
/// Base type for every UI component. Components measure themselves against the
/// available space and draw into a screen buffer; they never write to the
/// terminal or touch playback services directly.
/// </summary>
internal abstract class UiComponent
{
    public bool Visible { get; set; } = true;

    public int? PreferredWidth { get; set; }

    public int? PreferredHeight { get; set; }

    public int MinWidth { get; set; }

    public int MinHeight { get; set; }

    public virtual UiSize Measure(UiSize available)
    {
        var width = PreferredWidth ?? available.Width;
        var height = PreferredHeight ?? available.Height;
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, height))));
    }

    public abstract void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context);
}
