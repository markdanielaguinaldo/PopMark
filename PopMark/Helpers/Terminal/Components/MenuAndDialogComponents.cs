using PopMark.Helpers.Terminal.Rendering;

namespace PopMark.Helpers.Terminal.Components;

internal sealed record MenuItem(string Label, string? Description = null, bool Enabled = true);

/// <summary>Reusable keyboard-menu component with selection highlighting.</summary>
internal sealed class MenuComponent : UiComponent
{
    public IReadOnlyList<MenuItem> Items { get; set; } = Array.Empty<MenuItem>();

    public int SelectedIndex { get; set; }

    public int? MaxVisibleItems { get; set; }

    public bool ShowDescriptions { get; set; } = true;

    public override UiSize Measure(UiSize available)
    {
        var width = Items.Count == 0
            ? 1
            : Items.Max(item =>
                TerminalText.VisibleLength(item.Label) +
                (ShowDescriptions && !string.IsNullOrEmpty(item.Description)
                    ? 2 + TerminalText.VisibleLength(item.Description)
                    : 0));
        var height = Math.Min(Items.Count == 0 ? 1 : Items.Count, MaxVisibleItems ?? available.Height);
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, PreferredWidth ?? width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, PreferredHeight ?? height))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        if (Items.Count == 0)
        {
            screen.WriteText(bounds.X, bounds.Y, "No items", context.Theme.Muted, bounds.Width);
            return;
        }

        var visibleCount = Math.Max(1, Math.Min(bounds.Height, MaxVisibleItems ?? bounds.Height));
        var selected = Math.Clamp(SelectedIndex, 0, Items.Count - 1);
        var offset = Math.Clamp(selected - visibleCount + 1, 0, Math.Max(0, Items.Count - visibleCount));

        for (var row = 0; row < visibleCount && offset + row < Items.Count; row++)
        {
            var index = offset + row;
            var item = Items[index];
            var rowRect = new UiRect(bounds.X, bounds.Y + row, bounds.Width, 1);
            var isSelected = index == selected;

            if (isSelected)
                screen.Fill(rowRect, context.Theme.Selection);

            var style = !item.Enabled
                ? context.Theme.Faint
                : isSelected ? context.Theme.Accent.WithBold() : context.Theme.Text;
            var x = bounds.X;
            var labelWidth = Math.Max(0, bounds.Width -
                (ShowDescriptions && !string.IsNullOrEmpty(item.Description)
                    ? TerminalText.VisibleLength(item.Description) + 2
                    : 0));
            x += screen.WriteText(x, bounds.Y + row, TerminalText.TrimForWidget(item.Label, labelWidth),
                style, labelWidth);

            if (ShowDescriptions && !string.IsNullOrEmpty(item.Description) && x < bounds.Right)
            {
                x += screen.WriteText(x, bounds.Y + row, "  ", context.Theme.Faint, bounds.Right - x);
                screen.WriteText(x, bounds.Y + row, item.Description, context.Theme.Faint, bounds.Right - x);
            }
        }
    }

    public void SelectNext() => SelectedIndex = Math.Min(Items.Count - 1, SelectedIndex + 1);

    public void SelectPrevious() => SelectedIndex = Math.Max(0, SelectedIndex - 1);
}

/// <summary>
/// A centered modal dialog built from the reusable text and panel components.
/// The caller decides when to place it in an overlay.
/// </summary>
internal sealed class DialogComponent : UiComponent
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Footer { get; set; } = "Enter close · Esc cancel";

    public int DialogWidth { get; set; } = 60;

    public override UiSize Measure(UiSize available) =>
        new(Math.Min(available.Width, Math.Max(1, DialogWidth)), Math.Min(available.Height, 7));

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var width = Math.Max(12, Math.Min(bounds.Width - 2, Math.Max(12, DialogWidth)));
        var height = Math.Min(bounds.Height, 7);
        if (height < 3)
            return;

        var rect = new UiRect(
            bounds.X + Math.Max(0, (bounds.Width - width) / 2),
            bounds.Y + Math.Max(0, (bounds.Height - height) / 2),
            width,
            height);

        var content = new VerticalStackComponent();
        content.Add(new TextComponent
        {
            Text = TerminalText.TrimForWidget(Message, Math.Max(1, width - 4)),
            Style = context.Theme.Text
        }, 1);
        content.Add(new TextComponent
        {
            Text = Footer,
            Style = context.Theme.Faint
        }, 1);

        var panel = new PanelComponent
        {
            Title = Title,
            Child = content,
            Border = UiBorderStyle.Rounded,
            BorderStyle = context.Theme.BorderAccent,
            Background = context.Theme.SurfaceRaised,
            Padding = new Thickness(1, 1, 1, 1)
        };

        panel.Render(screen, rect, context);
    }
}

/// <summary>Draws a background and centers a content component on top of it.</summary>
internal sealed class OverlayComponent : UiComponent
{
    public UiComponent? Background { get; set; }

    public UiComponent? Content { get; set; }

    public CellStyle? BackdropStyle { get; set; }

    public override UiSize Measure(UiSize available) => available;

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        Background?.Render(screen, bounds, context);

        if (BackdropStyle is { } backdrop)
            screen.Fill(bounds, backdrop);

        if (Content is null)
            return;

        var desired = Content.Measure(bounds.Size);
        var width = Math.Min(bounds.Width, Math.Max(1, desired.Width));
        var height = Math.Min(bounds.Height, Math.Max(1, desired.Height));
        var rect = new UiRect(
            bounds.X + Math.Max(0, (bounds.Width - width) / 2),
            bounds.Y + Math.Max(0, (bounds.Height - height) / 2),
            width,
            height);

        Content.Render(screen, rect, context);
    }
}
