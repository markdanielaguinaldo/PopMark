using PopMark.Helpers.Terminal.Rendering;

namespace PopMark.Helpers.Terminal.Components;

/// <summary>A bordered, padded container with an optional title.</summary>
internal sealed class PanelComponent : UiComponent
{
    public UiComponent? Child { get; set; }

    public UiBorderStyle Border { get; set; } = UiBorderStyle.Rounded;

    public CellStyle BorderStyle { get; set; } = UiTheme.Default.Border;

    public CellStyle Background { get; set; } = UiTheme.Default.Surface;

    public Thickness Padding { get; set; } = Thickness.Horizontal(1);

    public string? Title { get; set; }

    public CellStyle? TitleStyle { get; set; }

    public override UiSize Measure(UiSize available)
    {
        var chrome = Chrome();
        var innerAvailable = new UiSize(
            Math.Max(0, available.Width - chrome.Left - chrome.Right),
            Math.Max(0, available.Height - chrome.Top - chrome.Bottom));
        var childSize = Child?.Measure(innerAvailable) ?? UiSize.Empty;
        var width = PreferredWidth ?? childSize.Width + chrome.Left + chrome.Right;
        var height = PreferredHeight ?? childSize.Height + chrome.Top + chrome.Bottom;

        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, height))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        screen.Fill(bounds, Background);

        if (Border != UiBorderStyle.None)
        {
            screen.DrawBox(
                bounds,
                Border,
                BorderStyle,
                Title,
                TitleStyle ?? context.Theme.Accent);
        }

        var inner = bounds.Inset(Chrome());
        if (!inner.IsEmpty)
            Child?.Render(screen, inner, context);
    }

    private Thickness Chrome()
    {
        var border = Border == UiBorderStyle.None ? 0 : 1;
        return new Thickness(
            border + Padding.Left,
            border + Padding.Top,
            border + Padding.Right,
            border + Padding.Bottom);
    }
}
