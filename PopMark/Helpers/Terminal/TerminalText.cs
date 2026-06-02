using System.Text;

namespace PopMark.Helpers.Terminal;

internal static class TerminalText
{
    public static string PadAnsiAware(string value, int width)
    {
        var visible = VisibleLength(value);
        return visible >= width ? value : $"{value}{new string(' ', width - visible)}";
    }

    public static string TrimAnsiAware(string value, int width)
    {
        if (width <= 0)
            return string.Empty;

        var visible = 0;
        var output = new StringBuilder();
        for (var i = 0; i < value.Length;)
        {
            if (value[i] == '\u001b')
            {
                output.Append(value[i]);
                while (i + 1 < value.Length)
                {
                    i++;
                    output.Append(value[i]);
                    if (char.IsLetter(value[i]))
                        break;
                }

                i++;
                continue;
            }

            var codePoint = char.ConvertToUtf32(value, i);
            var charLength = char.IsSurrogatePair(value, i) ? 2 : 1;
            var charWidth = CellWidth(codePoint);
            if (visible + charWidth > width)
                break;

            output.Append(value, i, charLength);
            visible += charWidth;
            i += charLength;
        }

        if (VisibleLength(value) > width)
            output.Append(TerminalStyles.Reset);

        return output.ToString();
    }

    public static int VisibleLength(string value)
    {
        var visible = 0;
        for (var i = 0; i < value.Length;)
        {
            if (value[i] == '\u001b')
            {
                while (i + 1 < value.Length)
                {
                    i++;
                    if (char.IsLetter(value[i]))
                        break;
                }

                i++;
                continue;
            }

            var codePoint = char.ConvertToUtf32(value, i);
            visible += CellWidth(codePoint);
            i += char.IsSurrogatePair(value, i) ? 2 : 1;
        }

        return visible;
    }

    public static string TrimForWidget(string value, int maxLength)
    {
        if (VisibleLength(value) <= maxLength)
            return value;

        return maxLength <= 3
            ? TrimPlainTextForWidth(value, maxLength)
            : $"{TrimPlainTextForWidth(value, maxLength - 3)}...";
    }

    public static string TailForWidth(string value, int width)
    {
        if (width <= 0)
            return string.Empty;

        if (VisibleLength(value) <= width)
            return value;

        var visible = 0;
        var start = value.Length;
        for (var i = value.Length - 1; i >= 0;)
        {
            var charStart = i;
            if (char.IsLowSurrogate(value[i]) && i > 0 && char.IsHighSurrogate(value[i - 1]))
                charStart = i - 1;

            var codePoint = char.ConvertToUtf32(value, charStart);
            var charWidth = CellWidth(codePoint);
            if (visible + charWidth > width)
                break;

            visible += charWidth;
            start = charStart;
            i = charStart - 1;
        }

        return value[start..];
    }

    private static int CellWidth(int codePoint)
    {
        if (codePoint == 0 ||
            codePoint < 32 ||
            codePoint is >= 0x7F and < 0xA0 ||
            codePoint is >= 0x300 and <= 0x36F ||
            codePoint is >= 0x1AB0 and <= 0x1AFF ||
            codePoint is >= 0x1DC0 and <= 0x1DFF ||
            codePoint is >= 0x20D0 and <= 0x20FF ||
            codePoint is >= 0xFE00 and <= 0xFE0F)
        {
            return 0;
        }

        return IsWideCodePoint(codePoint) ? 2 : 1;
    }

    private static bool IsWideCodePoint(int codePoint) =>
        codePoint is >= 0x1100 and <= 0x115F ||
        codePoint is >= 0x2329 and <= 0x232A ||
        codePoint is >= 0x2E80 and <= 0xA4CF and not 0x303F ||
        codePoint is >= 0xAC00 and <= 0xD7A3 ||
        codePoint is >= 0xF900 and <= 0xFAFF ||
        codePoint is >= 0xFE10 and <= 0xFE19 ||
        codePoint is >= 0xFE30 and <= 0xFE6F ||
        codePoint is >= 0xFF00 and <= 0xFF60 ||
        codePoint is >= 0xFFE0 and <= 0xFFE6 ||
        codePoint is >= 0x1F300 and <= 0x1FAFF ||
        codePoint is >= 0x20000 and <= 0x3FFFD;

    private static string TrimPlainTextForWidth(string value, int width)
    {
        if (width <= 0)
            return string.Empty;

        var visible = 0;
        var output = new StringBuilder();
        for (var i = 0; i < value.Length;)
        {
            var codePoint = char.ConvertToUtf32(value, i);
            var charLength = char.IsSurrogatePair(value, i) ? 2 : 1;
            var charWidth = CellWidth(codePoint);
            if (visible + charWidth > width)
                break;

            output.Append(value, i, charLength);
            visible += charWidth;
            i += charLength;
        }

        return output.ToString();
    }
}
