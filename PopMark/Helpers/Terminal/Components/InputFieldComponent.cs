using PopMark.Helpers.Terminal.Rendering;

namespace PopMark.Helpers.Terminal.Components;

/// <summary>
/// Command input line. The command parser still lives in the application; this
/// component only draws the buffer, its visible window, and the caret.
/// </summary>
internal sealed class InputFieldComponent : UiComponent
{
    private const int PromptWidth = 10;

    public string Text { get; set; } = string.Empty;

    public int CaretIndex { get; set; }

    public bool IsFocused { get; set; } = true;

    public string Prompt { get; set; } = "popmark";

    public string Separator { get; set; } = " > ";

    public override UiSize Measure(UiSize available)
    {
        var width = PromptWidth + TerminalText.VisibleLength(Text) + 1;
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, PreferredWidth ?? width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, PreferredHeight ?? 1))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        screen.Fill(bounds, context.Theme.SurfaceMuted);

        var x = bounds.X;
        var promptStyle = IsFocused ? context.Theme.Accent.WithBold() : context.Theme.Muted;
        x += screen.WriteText(x, bounds.Y, Prompt, promptStyle, bounds.Right - x);
        x += screen.WriteText(x, bounds.Y, Separator, context.Theme.Muted, bounds.Right - x);

        var textWidth = Math.Max(0, bounds.Right - x);
        var caret = Math.Clamp(CaretIndex, 0, Text.Length);
        var (visible, caretOffset) = VisibleWindow(Text, caret, textWidth);
        screen.WriteText(x, bounds.Y, visible, context.Theme.Text, textWidth);

        if (IsFocused)
            context.Caret = new UiCaret(Math.Clamp(x + caretOffset, bounds.X, bounds.Right - 1), bounds.Y);
    }

    internal static (string Text, int CaretOffset) VisibleWindow(string text, int caret, int width)
    {
        if (width <= 0 || string.IsNullOrEmpty(text))
            return (string.Empty, 0);

        caret = Math.Clamp(caret, 0, text.Length);
        if (text.Length <= width)
            return (text, caret);

        var start = caret > width ? caret - width + 1 : 0;
        start = Math.Clamp(start, 0, text.Length - width);
        var visible = text.Substring(start, Math.Min(width, text.Length - start));
        return (visible, Math.Clamp(caret - start, 0, visible.Length));
    }
}
