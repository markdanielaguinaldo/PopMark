using Spectre.Console;

namespace PopMark.Helpers.Terminal.Rendering;

/// <summary>
/// A terminal cell style. Backgrounds are inherited from the cell already in the
/// buffer when a background is not specified, which lets components use simple
/// foreground-only styles inside panels and rows.
/// </summary>
internal readonly record struct CellStyle(
    Color Foreground,
    Color? Background = null,
    bool Bold = false,
    bool Dim = false,
    bool Italic = false,
    bool Underline = false)
{
    public static readonly CellStyle Default = new(Color.Default);

    public static CellStyle FromColor(string color) => new(ParseColor(color));

    public static CellStyle FromHex(string color) => new(Color.FromHex(color));

    public static CellStyle FromBackground(string color) => new(Color.Default, ParseColor(color));

    public static Color ParseColor(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return Color.Default;

        if (trimmed[0] == '#')
            return Color.FromHex(trimmed);

        return Style.Parse(trimmed).Foreground;
    }

    public CellStyle WithForeground(string color) => this with { Foreground = ParseColor(color) };

    public CellStyle WithBackground(string color) => this with { Background = ParseColor(color) };

    public CellStyle WithBold(bool value = true) => this with { Bold = value };

    public CellStyle WithDim(bool value = true) => this with { Dim = value };

    public CellStyle WithItalic(bool value = true) => this with { Italic = value };

    public CellStyle WithUnderline(bool value = true) => this with { Underline = value };
}
