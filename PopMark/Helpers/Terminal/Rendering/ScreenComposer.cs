using Spectre.Console;
using System.Text;

namespace PopMark.Helpers.Terminal.Rendering;

/// <summary>
/// Converts screen buffers into ANSI output. Full paints are used for the first
/// frame and after a resize; every other frame is a cell-level diff.
/// </summary>
internal static class ScreenComposer
{
    public static string Compose(
        ScreenBuffer? previous,
        ScreenBuffer current,
        bool forceFullPaint,
        bool useColor,
        int? cursorRow = null,
        int? cursorColumn = null,
        bool showCursor = false)
    {
        var output = new StringBuilder(Math.Max(64, current.Width * current.Height / 2));
        output.Append("\u001b[?25l\u001b[?7l");

        var fullPaint = forceFullPaint ||
                        previous is null ||
                        previous.Width != current.Width ||
                        previous.Height != current.Height;

        if (fullPaint)
            output.Append("\u001b[H\u001b[2J");

        var styleState = new StyleState();
        for (var y = 0; y < current.Height; y++)
        {
            if (fullPaint)
            {
                MoveTo(output, y + 1, 1);
                WriteRange(output, current, y, 0, current.Width, useColor, styleState);
                output.Append("\u001b[K");
                continue;
            }

            foreach (var (start, end) in FindChangedRanges(previous!, current, y))
            {
                MoveTo(output, y + 1, start + 1);
                WriteRange(output, current, y, start, end, useColor, styleState);
            }
        }

        if (useColor)
            output.Append("\u001b[0m");

        if (showCursor && cursorRow is { } row && cursorColumn is { } column)
            MoveTo(output, row + 1, column + 1, showCursor: true);

        output.Append("\u001b[?7h");
        return output.ToString();
    }

    public static string ComposeRow(
        ScreenBuffer? previousRow,
        ScreenBuffer currentRow,
        int terminalRow,
        bool useColor,
        int? cursorColumn = null,
        bool showCursor = false)
    {
        var output = new StringBuilder(Math.Max(64, currentRow.Width + 32));
        output.Append("\u001b[?25l\u001b[?7l");
        var styleState = new StyleState();

        if (previousRow is null || previousRow.Width != currentRow.Width)
        {
            MoveTo(output, terminalRow, 1);
            WriteRange(output, currentRow, 0, 0, currentRow.Width, useColor, styleState);
            output.Append("\u001b[K");
        }
        else
        {
            foreach (var (start, end) in FindChangedRanges(previousRow, currentRow, 0))
            {
                MoveTo(output, terminalRow, start + 1);
                WriteRange(output, currentRow, 0, start, end, useColor, styleState);
            }
        }

        if (useColor)
            output.Append("\u001b[0m");

        if (showCursor && cursorColumn is { } column)
            MoveTo(output, terminalRow, column + 1, showCursor: true);

        output.Append("\u001b[?7h");
        return output.ToString();
    }

    private static IEnumerable<(int Start, int End)> FindChangedRanges(
        ScreenBuffer previous,
        ScreenBuffer current,
        int row)
    {
        var width = Math.Min(previous.Width, current.Width);
        var x = 0;

        while (x < width)
        {
            while (x < width && previous.Get(x, row).Equals(current.Get(x, row)))
                x++;

            if (x >= width)
                yield break;

            var start = x;
            var lastChanged = x;

            while (x < width)
            {
                if (!previous.Get(x, row).Equals(current.Get(x, row)))
                {
                    lastChanged = x;
                    x++;
                    continue;
                }

                // Keep small unchanged gaps in the same write run to avoid a cursor
                // move for every one or two spaces between styled text.
                var lookAhead = x;
                var gap = 0;
                while (lookAhead < width &&
                       gap <= 2 &&
                       previous.Get(lookAhead, row).Equals(current.Get(lookAhead, row)))
                {
                    lookAhead++;
                    gap++;
                }

                if (lookAhead >= width || gap > 2)
                    break;

                x = lookAhead;
            }

            var end = lastChanged + 1;

            if (start > 0 && current.Get(start, row).IsContinuation)
                start--;

            if (end < width && current.Get(end - 1, row).IsWide)
                end++;

            yield return (start, end);
            x = end;
        }
    }

    private static void WriteRange(
        StringBuilder output,
        ScreenBuffer buffer,
        int row,
        int start,
        int end,
        bool useColor,
        StyleState styleState)
    {
        for (var x = start; x < end; x++)
        {
            var cell = buffer.Get(x, row);
            if (cell.IsContinuation)
                continue;

            if (useColor && (styleState.Current is null || !styleState.Current.Value.Equals(cell.Style)))
            {
                output.Append("\u001b[0m");
                AppendStyle(output, cell.Style);
                styleState.Current = cell.Style;
            }

            output.Append(cell.Rune.ToString());
        }
    }

    private static void AppendStyle(StringBuilder output, CellStyle style)
    {
        if (style.Bold)
            output.Append("\u001b[1m");
        if (style.Dim)
            output.Append("\u001b[2m");
        if (style.Italic)
            output.Append("\u001b[3m");
        if (style.Underline)
            output.Append("\u001b[4m");

        if (!style.Foreground.Equals(Color.Default))
            output.Append($"\u001b[38;2;{style.Foreground.R};{style.Foreground.G};{style.Foreground.B}m");

        if (style.Background is { } background && !background.Equals(Color.Default))
            output.Append($"\u001b[48;2;{background.R};{background.G};{background.B}m");
    }

    private static void MoveTo(StringBuilder output, int row, int column, bool showCursor = false)
    {
        output.Append('\u001b');
        output.Append('[');
        output.Append(row);
        output.Append(';');
        output.Append(column);
        output.Append('H');

        if (showCursor)
            output.Append("\u001b[?25h");
    }

    private sealed class StyleState
    {
        public CellStyle? Current { get; set; }
    }
}
