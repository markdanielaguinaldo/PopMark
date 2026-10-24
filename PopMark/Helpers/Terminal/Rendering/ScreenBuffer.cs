using System.Text;

namespace PopMark.Helpers.Terminal.Rendering;

/// <summary>One cell in a <see cref="ScreenBuffer"/>.</summary>
internal readonly record struct ScreenCell(Rune Rune, CellStyle Style, bool IsContinuation = false)
{
    public bool IsWide => !IsContinuation && TerminalText.CodePointWidth(Rune.Value) == 2;

    public static ScreenCell Blank(CellStyle style) => new(new Rune(' '), style);
}

/// <summary>Border glyphs used when drawing a panel.</summary>
internal readonly record struct UiBorderGlyphs(
    char TopLeft,
    char TopRight,
    char BottomLeft,
    char BottomRight,
    char Horizontal,
    char Vertical)
{
    public static readonly UiBorderGlyphs Single = new('┌', '┐', '└', '┘', '─', '│');
    public static readonly UiBorderGlyphs Rounded = new('╭', '╮', '╰', '╯', '─', '│');
}

internal enum UiBorderStyle
{
    None,
    Single,
    Rounded
}

/// <summary>
/// In-memory terminal screen. Components draw into this grid; the renderer then
/// diffs it against the previous frame and writes only the changed cells.
/// </summary>
internal sealed class ScreenBuffer
{
    private readonly ScreenCell[] _cells;

    public ScreenBuffer(int width, int height, CellStyle? background = null)
    {
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
        _cells = new ScreenCell[Width * Height];
        Clear(background ?? CellStyle.Default);
    }

    public int Width { get; }

    public int Height { get; }

    public ScreenCell this[int x, int y] => Get(x, y);

    public void Clear(CellStyle style)
    {
        var blank = ScreenCell.Blank(style);
        Array.Fill(_cells, blank);
    }

    public ScreenCell Get(int x, int y) =>
        x < 0 || x >= Width || y < 0 || y >= Height
            ? ScreenCell.Blank(CellStyle.Default)
            : _cells[Index(x, y)];

    public void Set(int x, int y, Rune rune, CellStyle style)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;

        var width = TerminalText.CodePointWidth(rune.Value);
        if (width <= 0)
            return;

        if (width == 2 && x == Width - 1)
        {
            rune = new Rune(' ');
            width = 1;
        }

        var existing = Get(x, y);
        var effective = style.Background is null
            ? style with { Background = existing.Style.Background }
            : style;

        ClearOccupied(x, y);
        _cells[Index(x, y)] = new ScreenCell(rune, effective);

        if (width == 2 && x + 1 < Width)
        {
            ClearOccupied(x + 1, y);
            _cells[Index(x + 1, y)] = new ScreenCell(new Rune(' '), effective, IsContinuation: true);
        }
    }

    public void SetCell(int x, int y, ScreenCell cell)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;

        _cells[Index(x, y)] = cell;
    }

    public int WriteText(int x, int y, string text, CellStyle style, int maxWidth = int.MaxValue)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0 || y < 0 || y >= Height)
            return 0;

        var written = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var width = TerminalText.CodePointWidth(rune.Value);
            if (width <= 0)
                continue;

            if (written + width > maxWidth)
                break;

            Set(x + written, y, rune, style);
            written += width;
        }

        return written;
    }

    public int WriteSpans(int x, int y, IReadOnlyList<TextSpan> spans, CellStyle baseStyle, int maxWidth)
    {
        if (maxWidth <= 0 || y < 0 || y >= Height)
            return 0;

        var written = 0;
        foreach (var span in spans)
        {
            var style = span.Style ?? baseStyle;
            foreach (var rune in span.Text.EnumerateRunes())
            {
                var width = TerminalText.CodePointWidth(rune.Value);
                if (width <= 0)
                    continue;

                if (written + width > maxWidth)
                    return written;

                Set(x + written, y, rune, style);
                written += width;
            }
        }

        return written;
    }

    public void Fill(UiRect rect, CellStyle style) =>
        Fill(rect, new Rune(' '), style);

    public void Fill(UiRect rect, Rune rune, CellStyle style)
    {
        var clipped = rect.Intersect(new UiRect(0, 0, Width, Height));
        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            for (var x = clipped.X; x < clipped.Right; x++)
                Set(x, y, rune, style);
        }
    }

    public void DrawBox(
        UiRect rect,
        UiBorderStyle borderStyle,
        CellStyle borderCellStyle,
        string? title = null,
        CellStyle? titleStyle = null)
    {
        if (borderStyle == UiBorderStyle.None || rect.Width <= 0 || rect.Height <= 0)
            return;

        var glyphs = borderStyle == UiBorderStyle.Rounded ? UiBorderGlyphs.Rounded : UiBorderGlyphs.Single;
        var clipped = rect.Intersect(new UiRect(0, 0, Width, Height));
        if (clipped.IsEmpty)
            return;

        if (rect.Height == 1)
        {
            for (var x = rect.X; x < rect.Right; x++)
                Set(x, rect.Y, new Rune(glyphs.Horizontal), borderCellStyle);
            return;
        }

        if (rect.Width == 1)
        {
            for (var y = rect.Y; y < rect.Bottom; y++)
                Set(rect.X, y, new Rune(glyphs.Vertical), borderCellStyle);
            return;
        }

        Set(rect.X, rect.Y, new Rune(glyphs.TopLeft), borderCellStyle);
        Set(rect.Right - 1, rect.Y, new Rune(glyphs.TopRight), borderCellStyle);
        Set(rect.X, rect.Bottom - 1, new Rune(glyphs.BottomLeft), borderCellStyle);
        Set(rect.Right - 1, rect.Bottom - 1, new Rune(glyphs.BottomRight), borderCellStyle);

        for (var x = rect.X + 1; x < rect.Right - 1; x++)
        {
            Set(x, rect.Y, new Rune(glyphs.Horizontal), borderCellStyle);
            Set(x, rect.Bottom - 1, new Rune(glyphs.Horizontal), borderCellStyle);
        }

        for (var y = rect.Y + 1; y < rect.Bottom - 1; y++)
        {
            Set(rect.X, y, new Rune(glyphs.Vertical), borderCellStyle);
            Set(rect.Right - 1, y, new Rune(glyphs.Vertical), borderCellStyle);
        }

        if (!string.IsNullOrWhiteSpace(title) && rect.Width > 4)
        {
            var fitted = TerminalText.TrimForWidget(title, Math.Max(1, rect.Width - 4));
            var titleText = $" {fitted} ";
            WriteText(rect.X + 1, rect.Y, titleText, titleStyle ?? borderCellStyle, Math.Max(0, rect.Width - 2));
        }
    }

    public ScreenBuffer ExtractRow(int y)
    {
        var row = new ScreenBuffer(Width, 1);
        if (y < 0 || y >= Height)
            return row;

        for (var x = 0; x < Width; x++)
            row.SetCell(x, 0, Get(x, y));

        return row;
    }

    public void ApplyRow(ScreenBuffer row, int y)
    {
        if (row.Height <= 0 || y < 0 || y >= Height)
            return;

        var width = Math.Min(Width, row.Width);
        for (var x = 0; x < width; x++)
            SetCell(x, y, row.Get(x, 0));
    }

    public IReadOnlyList<string> ToLines(bool trimEnd = true)
    {
        var lines = new List<string>(Height);
        var builder = new StringBuilder(Width);

        for (var y = 0; y < Height; y++)
        {
            builder.Clear();
            for (var x = 0; x < Width; x++)
            {
                var cell = Get(x, y);
                if (!cell.IsContinuation)
                    builder.Append(cell.Rune.ToString());
            }

            var line = builder.ToString();
            lines.Add(trimEnd ? line.TrimEnd() : line);
        }

        return lines;
    }

    public string ToPlainText() => string.Join(Environment.NewLine, ToLines());

    private void ClearOccupied(int x, int y)
    {
        var cell = Get(x, y);
        if (cell.IsContinuation)
        {
            var lead = Get(x - 1, y);
            _cells[Index(x - 1, y)] = ScreenCell.Blank(lead.Style);
            return;
        }

        if (cell.IsWide && x + 1 < Width)
        {
            var continuation = Get(x + 1, y);
            _cells[Index(x + 1, y)] = ScreenCell.Blank(continuation.Style);
        }
    }

    private int Index(int x, int y) => y * Width + x;
}
