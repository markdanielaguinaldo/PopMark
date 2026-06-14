using PopMark.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace PopMark.Helpers.Terminal;

internal static class SplashScreen
{
    public static IRenderable Render(int frame, string version, int terminalWidth, int terminalHeight)
    {
        var width = Math.Max(1, terminalWidth > 0 ? terminalWidth : 80);
        var height = Math.Max(1, terminalHeight > 0 ? terminalHeight : 24);
        var content = SplashCanvas.Render(frame, version, Math.Max(1, width - 2), Math.Max(1, height - 2));

        return new Panel(content)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(TerminalStyles.Accent),
            Padding = new Padding(0, 0),
            Width = width,
            Height = height
        };
    }
}

internal static class SplashCanvas
{
    private const string Dim = "grey23";

    public static IRenderable Render(int frame, string version, int width, int height)
    {
        var cells = CreateCells(width, height);
        AddAmbientFrame(cells, frame, version);
        AddHero(cells, frame, version);

        return new Rows(
            Enumerable.Range(0, height)
                .Select(row => new Markup(BuildMarkupRow(cells, row)))
                .ToArray());
    }

    private static SplashCell[,] CreateCells(int width, int height)
    {
        var cells = new SplashCell[height, width];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
                cells[row, column] = new SplashCell(' ', Dim);
        }

        return cells;
    }

    private static void AddAmbientFrame(SplashCell[,] cells, int frame, string version)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        var pulse = frame % 4 < 2 ? "grey35" : TerminalStyles.Secondary;

        Write(cells, 1, 3, MicroSignal(frame), TerminalStyles.Accent);
        WriteRight(cells, 1, 3, $"v{version}", "grey70");
        WriteRight(cells, 2, 3, $"YT Music {SignalTrace(frame, 10)}", TerminalStyles.Secondary);

        if (height >= 18)
        {
            Write(cells, height - 3, 3, PlaybackHint(frame), "grey70");
            Write(cells, height - 2, 3, TransportTrail(frame), TerminalStyles.Secondary);
            AddBottomRightDots(cells, frame);
        }

        AddMiddleDecorations(cells, frame);
        AddParticles(cells, frame, pulse);
    }

    private static void AddMiddleDecorations(SplashCell[,] cells, int frame)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        if (width < 56 || height < 16)
            return;

        AddLeftTower(cells, frame, Math.Max(4, height / 2 - 4), 4);
        AddRightPulse(cells, frame, Math.Max(4, height / 2 - 2), Math.Max(0, width - 13));
    }

    private static void AddLeftTower(SplashCell[,] cells, int frame, int startRow, int column)
    {
        int[][] frames =
        [
            [2, 4, 3, 5, 2],
            [3, 4, 3, 4, 2],
            [3, 5, 4, 4, 3],
            [2, 4, 4, 5, 3]
        ];

        var levels = frames[frame % frames.Length];
        var towerHeight = 6;
        for (var row = 0; row < towerHeight; row++)
        {
            var line = new char[levels.Length];
            for (var bar = 0; bar < levels.Length; bar++)
            {
                line[bar] = towerHeight - row <= levels[bar] ? '▌' : '·';
            }

            Write(cells, startRow + row, column, new string(line), row >= 2 ? TerminalStyles.Accent : TerminalStyles.Secondary);
        }
    }

    private static void AddRightPulse(SplashCell[,] cells, int frame, int startRow, int column)
    {
        var art = (frame % 4) switch
        {
            0 => new[] { "  .-.  ", " ( o ) ", "  '-'  " },
            1 => new[] { " .---. ", "(  o  )", " '---' " },
            2 => new[] { " .---. ", "(  O  )", " '---' " },
            _ => new[] { "  .-.  ", " ( o ) ", "  '-'  " }
        };

        for (var row = 0; row < art.Length; row++)
            Write(cells, startRow + row, column, art[row], row == 1 && frame == 2 ? TerminalStyles.Accent : "grey70");
    }

    private static void AddBottomRightDots(SplashCell[,] cells, int frame)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        if (width < 48)
            return;

        var dots = (frame % 4) switch
        {
            0 => " ·   · ",
            1 => "  · ·  ",
            2 => "   ·   ",
            _ => "  · ·  "
        };

        WriteRight(cells, height - 2, 5, dots, TerminalStyles.Accent);
    }

    private static void AddParticles(SplashCell[,] cells, int frame, string pulse)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        if (width < 48 || height < 16)
            return;

        var particles = new (int X, int Y, char[] Marks)[]
        {
            (20, 25, ['.', '·', ' ']),
            (78, 24, ['*', '.', ' ']),
            (13, 60, ['·', ' ', '.']),
            (84, 64, ['.', '*', ' ']),
            (32, 77, ['·', '.', ' ']),
            (68, 78, ['*', ' ', '.'])
        };

        foreach (var (x, y, marks) in particles)
        {
            var row = Math.Clamp(height * y / 100, 3, Math.Max(3, height - 4));
            var column = Math.Clamp(width * x / 100, 1, Math.Max(1, width - 2));
            var mark = marks[(frame / 2 + row + column) % marks.Length];
            if (mark != ' ')
                Put(cells, row, column, mark, pulse);
        }
    }

    private static void AddHero(SplashCell[,] cells, int frame, string version)
    {
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
        var art = AudioBeaconLogo.Lines(frame, width);
        var artWidth = art.Max(line => line.Length);
        var compact = height < 18;
        var groupHeight = art.Count + (compact ? 2 : 4);
        var top = Math.Max(1, (height - groupHeight) / 2);
        var column = Math.Max(0, (width - artWidth) / 2);

        for (var index = 0; index < art.Count && top + index < height; index++)
        {
            var line = art[index].PadRight(artWidth);
            WriteTape(cells, top + index, column, line, frame, index);
        }

        var titleRow = Math.Min(height - 1, top + art.Count + 1);
        var subtitleRow = Math.Min(height - 1, titleRow + 1);
        var readyRow = Math.Min(height - 1, subtitleRow + (compact ? 1 : 2));
        var pulse = frame % 4 < 2 ? "white" : TerminalStyles.Accent;

        WriteCentered(cells, titleRow, "PopMark", TerminalStyles.Accent);
        if (!compact)
            WriteCentered(cells, subtitleRow, $"Terminal Music Player v{version}", "grey70");
        WriteCentered(cells, readyRow, "Ready • Press any key", pulse);
    }

    private static void WriteTape(SplashCell[,] cells, int row, int column, string text, int frame, int tapeRow)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            var style = AudioBeaconLogo.StyleFor(frame, tapeRow, i, character, text);
            Put(cells, row, column + i, character, style);
        }
    }

    private static string MicroSignal(int frame)
    {
        string[] frames =
        [
            "⣀⣄⣤⣶⣤⣄⣀⣄⣀",
            "⣀⣄⣶⣿⣶⣄⣀⣄⣀",
            "⣀⣄⣤⣶⣤⣄⣀⣄⣀"
        ];

        return frames[(frame / 3) % frames.Length];
    }

    private static string SignalTrace(int frame, int width)
    {
        const string source = "▁▂▃▅▇▅▃▂";
        return string.Concat(Enumerable.Range(0, width).Select(index => source[(frame + index * 2) % source.Length]));
    }

    private static string PlaybackHint(int frame) =>
        frame % 8 < 4
            ? "▶ SPACE   ▌▌ P/P   ■ Q"
            : "▷ SPACE   ▌▌ P/P   ■ Q";

    private static string TransportTrail(int frame)
    {
        const string source = "───●──────";
        var offset = frame % source.Length;
        return source[offset..] + source[..offset];
    }

    private static void WriteCentered(SplashCell[,] cells, int row, string text, string style) =>
        Write(cells, row, Math.Max(0, (cells.GetLength(1) - TerminalText.VisibleLength(text)) / 2), text, style);

    private static void WriteRight(SplashCell[,] cells, int row, int margin, string text, string style) =>
        Write(cells, row, Math.Max(0, cells.GetLength(1) - margin - TerminalText.VisibleLength(text)), text, style);

    private static void Write(SplashCell[,] cells, int row, int column, string text, string style)
    {
        for (var index = 0; index < text.Length; index++)
            Put(cells, row, column + index, text[index], style);
    }

    private static void Put(SplashCell[,] cells, int row, int column, char character, string style)
    {
        if (row < 0 ||
            row >= cells.GetLength(0) ||
            column < 0 ||
            column >= cells.GetLength(1))
        {
            return;
        }

        cells[row, column] = new SplashCell(character, style);
    }

    private static string BuildMarkupRow(SplashCell[,] cells, int row)
    {
        var output = new System.Text.StringBuilder();
        var width = cells.GetLength(1);
        var currentStyle = cells[row, 0].Style;
        var segment = new System.Text.StringBuilder();

        for (var column = 0; column < width; column++)
        {
            var cell = cells[row, column];
            if (cell.Style != currentStyle)
            {
                AppendSegment(output, currentStyle, segment.ToString());
                segment.Clear();
                currentStyle = cell.Style;
            }

            segment.Append(cell.Character);
        }

        AppendSegment(output, currentStyle, segment.ToString());
        return output.ToString();
    }

    private static void AppendSegment(System.Text.StringBuilder output, string style, string text)
    {
        output.Append('[');
        output.Append(style);
        output.Append(']');
        output.Append(Markup.Escape(text));
        output.Append("[/]");
    }

    private sealed record SplashCell(char Character, string Style);
}

internal static class AppLayout
{
    public static IRenderable Render(RenderContext context)
    {
        if (context.MiniMode)
            return MiniPlayerPanel.Render(context);

        var height = Math.Max(18, context.Height);
        var width = LayoutMetrics.ContentWidth(context.Width);
        const int headerHeight = 1;
        const int nowPlayingHeight = 6;
        const int playbackHeight = 4;
        const int helpHeight = 1;
        var queueHeight = Math.Max(4, height - headerHeight - nowPlayingHeight - playbackHeight - helpHeight);
        var visibleQueueRows = Math.Max(1, queueHeight - 2);

        return Align.Center(new Rows(
            HeaderLine.Render(context, width),
            NowPlayingPanel.Render(context, nowPlayingHeight, width),
            QueuePanel.Render(context.Snapshot, context.QueueScrollOffset, visibleQueueRows, queueHeight, width),
            PlaybackPanel.Render(context, playbackHeight, width),
            HelpLine.Render(context.ShowHelp, context.ShowControls, width)), VerticalAlignment.Top);
    }
}

internal static class HeaderLine
{
    public static IRenderable Render(RenderContext context, int width)
    {
        var snapshot = context.Snapshot;
        var left = new HeaderSegment(
            $"PopMark {StatusText(snapshot.Status)}",
            $"[bold {TerminalStyles.Accent}]PopMark[/] [grey70]{StatusText(snapshot.Status)}[/]");
        var middle = new HeaderSegment(
            $"Queue {QueueCount(snapshot)}",
            $"[grey35]Queue[/] [white]{QueueCount(snapshot)}[/]");
        var right = new HeaderSegment(
            $"Elapsed {FormatDuration(snapshot.Elapsed)}",
            $"[grey35]Elapsed[/] [white]{FormatDuration(snapshot.Elapsed)}[/]");

        return new Markup(BuildHeaderMarkup(width, left, middle, right));
    }

    private static string StatusText(PlaybackStatus status) =>
        status switch
        {
            PlaybackStatus.Playing => "playing",
            PlaybackStatus.Loading => "loading",
            PlaybackStatus.Paused => "paused",
            PlaybackStatus.Detached => "detached",
            _ => "stopped"
        };

    private static int QueueCount(PlayerSnapshot snapshot) =>
        snapshot.Previous.Count + snapshot.Pending.Count + (snapshot.Current is null ? 0 : 1);

    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
            return "--:--";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }

    private static string BuildHeaderMarkup(int width, HeaderSegment left, HeaderSegment middle, HeaderSegment right)
    {
        if (width <= 0)
            return string.Empty;

        var rightStart = Math.Max(0, width - right.Width);
        var centerStart = Math.Max(left.Width + 2, (width - middle.Width) / 2);
        var useMiddle = centerStart + middle.Width + 2 <= rightStart;
        var maxLeftWidth = Math.Max(0, (useMiddle ? centerStart : rightStart) - 1);
        var safeLeft = left.Width <= maxLeftWidth
            ? left
            : HeaderSegment.StyledPlain(TerminalText.TrimForWidget(left.Plain, maxLeftWidth), TerminalStyles.Accent);

        var output = new System.Text.StringBuilder();
        var cursor = 0;
        AppendSegment(output, safeLeft, 0, ref cursor);

        if (useMiddle)
            AppendSegment(output, middle, centerStart, ref cursor);

        if (rightStart >= cursor)
            AppendSegment(output, right, rightStart, ref cursor);

        if (cursor < width)
            output.Append(' ', width - cursor);

        return output.ToString();
    }

    private static void AppendSegment(System.Text.StringBuilder output, HeaderSegment segment, int start, ref int cursor)
    {
        if (start > cursor)
        {
            output.Append(' ', start - cursor);
            cursor = start;
        }

        output.Append(segment.Markup);
        cursor += segment.Width;
    }

    private sealed record HeaderSegment(string Plain, string Markup)
    {
        public int Width => TerminalText.VisibleLength(Plain);

        public static HeaderSegment StyledPlain(string value, string style) =>
            new(value, $"[{style}]{Spectre.Console.Markup.Escape(value)}[/]");
    }
}

internal static class NowPlayingPanel
{
    private const int OwlWidth = 20;

    public static IRenderable Render(RenderContext context, int height, int width)
    {
        var snapshot = context.Snapshot;
        var track = snapshot.Current ?? snapshot.Pending.FirstOrDefault();
        var contentWidth = Math.Max(1, width - 4);
        var owl = OwlLines(OwlExpressionForFrame(context.AnimationFrame));
        var owlWidth = owl.Max(line => line.Width);
        var showOwl = contentWidth >= owlWidth + 28;
        var textWidth = showOwl ? Math.Max(1, contentWidth - owlWidth - 2) : contentWidth;
        var title = Markup.Escape(FitPlain(track?.Title ?? "Nothing loaded", textWidth));
        var source = Markup.Escape(FitPlain(track?.DisplaySource ?? "add <url>", textWidth));
        var statusLabel = StatusLabel(snapshot.Status);
        var noticeWidth = Math.Max(0, textWidth - statusLabel.Length - 1);
        var notice = Markup.Escape(FitPlain(context.Notice, noticeWidth));
        var elapsed = Markup.Escape(FitPlain($"Elapsed {FormatDuration(snapshot.Elapsed)} | Duration {FormatDuration(track?.Duration)}", textWidth));
        var rows = new Rows(
            new Markup(WithOwl($"[bold white]{title}[/]", owl, 0, showOwl)),
            new Markup(WithOwl($"[grey70]{source}[/]", owl, 1, showOwl)),
            new Markup(WithOwl($"{StatusPill(snapshot.Status)} [grey70]{notice}[/]", owl, 2, showOwl)),
            new Markup(WithOwl($"[grey70]{elapsed}[/]", owl, 3, showOwl)));

        return new Panel(rows)
        {
            Header = new PanelHeader("[deepskyblue1] Now Playing [/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(TerminalStyles.Accent),
            Padding = new Padding(1, 0),
            Height = height,
            Width = width
        };
    }

    private static string StatusPill(PlaybackStatus status)
    {
        var style = status switch
        {
            PlaybackStatus.Playing => TerminalStyles.Accent,
            PlaybackStatus.Loading => TerminalStyles.Secondary,
            PlaybackStatus.Paused => TerminalStyles.Accent,
            PlaybackStatus.Detached => TerminalStyles.Secondary,
            _ => "grey70"
        };

        return $"[{style}]{StatusLabel(status)}[/]";
    }

    private static string StatusLabel(PlaybackStatus status) =>
        status switch
        {
            PlaybackStatus.Playing => "PLAYING",
            PlaybackStatus.Loading => "LOADING",
            PlaybackStatus.Paused => "PAUSED",
            PlaybackStatus.Detached => "DETACHED",
            _ => "STOPPED"
        };

    private static string FitPlain(string value, int width) =>
        TerminalText.PadPlain(TerminalText.TrimForWidget(value, width), width);

    private static string WithOwl(string textMarkup, IReadOnlyList<OwlLine> owl, int row, bool showOwl)
    {
        if (!showOwl)
            return textMarkup;

        return $"{textMarkup}  {owl[row].Markup}";
    }

    private static IReadOnlyList<OwlLine> OwlLines(OwlExpression expression)
    {
        var (headPlain, headMarkup, facePlain, faceMarkup) = expression switch
        {
            OwlExpression.Sleep => (
                "           ^...^",
                "[grey70]           ^...^[/]",
                "          / z,z \\",
                "[grey70]          / [/][#00d4ff]z,z[/][grey70] \\[/]"),
            OwlExpression.Blink => (
                "           ^...^",
                "[grey70]           ^...^[/]",
                "          / -,- \\",
                "[grey70]          / [/][#00d4ff]-,-[/][grey70] \\[/]"),
            _ => (
                "           ^...^",
                "[grey70]           ^...^[/]",
                "          / o,o \\",
                "[grey70]          / [/][#00d4ff]o,o[/][grey70] \\[/]")
        };

        return
        [
            Owl(headPlain, headMarkup),
            Owl(facePlain, faceMarkup),
            Owl(BodyPlain(), BodyMarkup()),
            Owl(FeetPlain(), FeetMarkup())
        ];
    }

    private static string BodyPlain() =>
        "          |):::(|";

    private static string BodyMarkup() =>
        "[grey70]          |[/][#3b82f6])[/][grey70]:::[/][#3b82f6]([/][grey70]|[/]";

    private static string FeetPlain() =>
        "        ----w-w----";

    private static string FeetMarkup() =>
        "[grey70]        ----[/][#3b82f6]w-w[/][grey70]----[/]";

    private static OwlLine Owl(string plain, string markup)
    {
        var padding = Math.Max(0, OwlWidth - TerminalText.VisibleLength(plain));
        return OwlLine.Styled($"{plain}{new string(' ', padding)}", $"{markup}{new string(' ', padding)}");
    }

    private static OwlExpression OwlExpressionForFrame(int frame)
    {
        var cycle = frame % 120;
        if (cycle is >= 88 and <= 112)
            return OwlExpression.Sleep;

        if (cycle is 21 or 22 or 45 or 46 or 69 or 70)
            return OwlExpression.Blink;

        return OwlExpression.Center;
    }

    private enum OwlExpression
    {
        Center,
        Blink,
        Sleep
    }

    private sealed record OwlLine(string Plain, string Markup)
    {
        public int Width => TerminalText.VisibleLength(Plain);

        public static OwlLine Styled(string plain, string markup) =>
            new(plain, markup);
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
            return "--:--";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }
}

internal static class QueuePanel
{
    public static IRenderable Render(PlayerSnapshot snapshot, int scrollOffset, int visibleRows, int panelHeight, int width)
    {
        var rows = BuildRows(snapshot);
        var maxOffset = Math.Max(0, rows.Count - visibleRows);
        var offset = Math.Clamp(scrollOffset, 0, maxOffset);
        var rowsToShow = rows.Count == 0
            ? [QueueRow.Message("Queue is empty")]
            : rows.Skip(offset).Take(visibleRows).ToList();
        var hiddenAfter = rows.Count == 0 ? 0 : Math.Max(0, rows.Count - offset - rowsToShow.Count);
        if (hiddenAfter > 0 && rowsToShow.Count > 0)
        {
            rowsToShow.RemoveAt(rowsToShow.Count - 1);
            rowsToShow.Add(QueueRow.More(hiddenAfter + 1));
        }

        while (rowsToShow.Count < visibleRows)
            rowsToShow.Add(QueueRow.Empty());

        var contentWidth = Math.Max(1, width - 4);
        var numberWidth = Math.Clamp(rows.Count.ToString().Length + 1, 3, 6);
        const int markerWidth = 2;
        var titleWidth = Math.Max(8, contentWidth - numberWidth - markerWidth - 1);
        var table = new Table
        {
            Border = TableBorder.None,
            ShowHeaders = false,
            Expand = true,
            Width = contentWidth
        };
        table.AddColumn(new TableColumn(string.Empty) { Width = numberWidth, Padding = new Padding(0, 0), NoWrap = true });
        table.AddColumn(new TableColumn(string.Empty) { Width = markerWidth, Padding = new Padding(0, 0), NoWrap = true });
        table.AddColumn(new TableColumn(string.Empty) { Width = titleWidth, Padding = new Padding(0, 0), NoWrap = true });

        foreach (var row in rowsToShow)
        {
            table.AddRow(
                new Markup(row.NumberMarkup),
                new Markup(row.MarkerMarkup),
                new Markup(row.TitleMarkup(titleWidth)));
        }

        var end = rows.Count == 0 ? 0 : Math.Min(offset + visibleRows, rows.Count);
        var header = rows.Count > visibleRows && visibleRows > 0
            ? $"[deepskyblue1] Queue {offset + 1}-{end} / {rows.Count} [/]"
            : $"[deepskyblue1] Queue {rows.Count} [/]";

        return new Panel(table)
        {
            Header = new PanelHeader(header, Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey35"),
            Padding = new Padding(1, 0),
            Height = panelHeight,
            Width = width
        };
    }

    private static IReadOnlyList<QueueRow> BuildRows(PlayerSnapshot snapshot)
    {
        var rows = new List<QueueRow>();
        var index = 1;

        foreach (var track in snapshot.Previous)
            rows.Add(QueueRow.Track(index++, "[grey35]✓[/]", "[grey70]", track.Title));

        if (snapshot.Current is not null)
            rows.Add(QueueRow.Track(index++, $"[{TerminalStyles.Accent}]▶[/]", "[bold white]", snapshot.Current.Title));

        foreach (var track in snapshot.Pending)
            rows.Add(QueueRow.Track(index++, "[grey70]›[/]", "[white]", track.Title));

        return rows;
    }

    private sealed record QueueRow(int DisplayIndex, string MarkerMarkup, string Style, string Title, bool IsMore = false)
    {
        public static QueueRow Track(int displayIndex, string markerMarkup, string style, string title) =>
            new(displayIndex, markerMarkup, style, title);

        public static QueueRow More(int count) =>
            new(0, "[grey35]+[/]", "[grey70]", $"{count} more", IsMore: true);

        public static QueueRow Message(string title) =>
            new(0, "[grey35]•[/]", "[grey70]", title, IsMore: true);

        public static QueueRow Empty() =>
            new(0, string.Empty, "[grey35]", string.Empty, IsMore: true);

        public string NumberMarkup => IsMore ? string.Empty : $"[grey35]{DisplayIndex,2}.[/]";

        public string TitleMarkup(int width) =>
            $"{Style}{Markup.Escape(TerminalText.PadPlain(TerminalText.TrimForWidget(Title, width), width))}[/]";
    }
}

internal static class PlaybackPanel
{
    public static IRenderable Render(RenderContext context, int height, int width)
    {
        var snapshot = context.Snapshot;
        var track = snapshot.Current ?? snapshot.Pending.FirstOrDefault();
        var contentWidth = Math.Max(1, width - 4);
        var timeText = $"{FormatDuration(snapshot.Elapsed)} / {FormatDuration(track?.Duration)}";
        var rows = new Rows(
            new Markup(Visualizer(snapshot.Status, context.AnimationFrame, contentWidth)),
            new Markup(ProgressMeterRow(ProgressRatio(snapshot), timeText, snapshot.VolumePercent, contentWidth)));

        return new Panel(rows)
        {
            Header = new PanelHeader("[deepskyblue1] Playback [/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("grey35"),
            Padding = new Padding(1, 0),
            Height = height,
            Width = width
        };
    }

    private static string ProgressBar(double ratio, int width)
    {
        var filled = Math.Clamp((int)Math.Round(width * Math.Clamp(ratio, 0, 1)), 0, width);
        return $"[{TerminalStyles.Accent}]{new string('█', filled)}[/][grey23]{new string('░', width - filled)}[/]";
    }

    private static string Visualizer(PlaybackStatus status, int frame, int width)
    {
        if (width <= 0)
            return string.Empty;

        var style = status is PlaybackStatus.Playing or PlaybackStatus.Loading ? TerminalStyles.Accent : "grey35";
        var content = BrailleWave(status, frame, width);

        return $"[{style}]{content}[/]";
    }

    public static int ProgressHitboxWidth(PlayerSnapshot snapshot, int panelWidth)
    {
        var track = snapshot.Current ?? snapshot.Pending.FirstOrDefault();
        var contentWidth = Math.Max(1, panelWidth - 4);
        var timeText = $"{FormatDuration(snapshot.Elapsed)} / {FormatDuration(track?.Duration)}";
        return ProgressMeterBarWidth(timeText, snapshot.VolumePercent, contentWidth);
    }

    private static string ProgressMeterRow(double ratio, string timeText, int volumePercent, int width)
    {
        if (width <= 0)
            return string.Empty;

        var sizing = CalculateMeterSizing(timeText, volumePercent, width);
        if (sizing.RightWidth > width)
            return $"[grey70]{Markup.Escape(TerminalText.TrimForWidget(sizing.RightPlain, width))}[/]";

        var progressWidth = ProgressMeterBarWidth(sizing, width);
        if (progressWidth <= 0)
            return MeterRightMarkup(sizing.TimeText, sizing.Volume, sizing.VolumeBarWidth);

        return $"{ProgressBar(ratio, progressWidth)} {MeterRightMarkup(sizing.TimeText, sizing.Volume, sizing.VolumeBarWidth)}";
    }

    private static int ProgressMeterBarWidth(string timeText, int volumePercent, int width) =>
        ProgressMeterBarWidth(CalculateMeterSizing(timeText, volumePercent, width), width);

    private static int ProgressMeterBarWidth(MeterSizing sizing, int width) =>
        Math.Max(0, width - sizing.RightWidth - (sizing.RightWidth > 0 ? 1 : 0));

    private static MeterSizing CalculateMeterSizing(string timeText, int volumePercent, int width)
    {
        var volume = Math.Clamp(volumePercent, 0, 200);
        var timeWidth = width >= 56 ? 12 : Math.Clamp(width / 4, 8, 12);
        var fittedTime = TerminalText.TrimForWidget(timeText, timeWidth);
        var volumeBarWidth = 10;
        var rightPlain = BuildMeterRightPlain(fittedTime, volume, volumeBarWidth);
        var minimumProgressWidth = width >= 48 ? 12 : Math.Min(8, Math.Max(0, width / 3));

        while (TerminalText.VisibleLength(rightPlain) + minimumProgressWidth + 1 > width && volumeBarWidth > 4)
        {
            volumeBarWidth--;
            rightPlain = BuildMeterRightPlain(fittedTime, volume, volumeBarWidth);
        }

        while (TerminalText.VisibleLength(rightPlain) > width && volumeBarWidth > 0)
        {
            volumeBarWidth--;
            rightPlain = BuildMeterRightPlain(fittedTime, volume, volumeBarWidth);
        }

        return new MeterSizing(fittedTime, volume, volumeBarWidth, rightPlain);
    }

    private static string BuildMeterRightPlain(string timeText, int volumePercent, int volumeBarWidth) =>
        $"{timeText} {VolumeBarPlain(volumePercent, volumeBarWidth)} {volumePercent,3}%";

    private static string MeterRightMarkup(string timeText, int volumePercent, int volumeBarWidth) =>
        $"[grey70]{Markup.Escape(timeText)}[/] {VolumeMeter(volumePercent, volumeBarWidth)} [{VolumeStyle(volumePercent)}]{volumePercent,3}%[/]";

    private static string VolumeBarPlain(int volumePercent, int width)
    {
        var filled = VolumeBarFilled(volumePercent, width);
        return $"{new string('█', filled)}{new string('░', width - filled)}";
    }

    private static string VolumeMeter(int volumePercent, int width)
    {
        var filled = VolumeBarFilled(volumePercent, width);
        var style = VolumeStyle(volumePercent);
        return $"[{style}]{new string('█', filled)}[/][grey23]{new string('░', width - filled)}[/]";
    }

    private static int VolumeBarFilled(int volumePercent, int width)
    {
        var normalizedVolume = Math.Clamp(volumePercent, 0, 100);
        return Math.Clamp((int)Math.Round(normalizedVolume / 100d * width), 0, width);
    }

    private static string VolumeStyle(int volumePercent) =>
        volumePercent switch
        {
            > 150 => "#ff4dff",
            > 100 => TerminalStyles.Boost,
            _ => TerminalStyles.Accent
        };

    private sealed record MeterSizing(string TimeText, int Volume, int VolumeBarWidth, string RightPlain)
    {
        public int RightWidth => TerminalText.VisibleLength(RightPlain);
    }

    private static string BrailleWave(PlaybackStatus status, int frame, int width)
    {
        string[] glyphs = ["⣀", "⣄", "⣆", "⣇", "⣧", "⣷", "⣿"];
        if (status is not (PlaybackStatus.Playing or PlaybackStatus.Loading))
            return new string('⣀', width);

        return string.Concat(Enumerable.Range(0, width).Select(index =>
        {
            var current = WaveLevel(frame, index);
            var previous = WaveLevel(frame - 1, index);
            var smoothed = (int)Math.Round(previous + (current - previous) * 0.55);
            return glyphs[Math.Clamp(smoothed, 0, glyphs.Length - 1)];
        }));
    }

    private static int WaveLevel(int frame, int index)
    {
        var phase = (frame + index * 2) % 14;
        return phase <= 7 ? phase - 1 : 13 - phase;
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
            return "--:--";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }

    private static double ProgressRatio(PlayerSnapshot snapshot)
    {
        if (snapshot.Current?.Duration is not { TotalSeconds: > 0 } duration ||
            snapshot.Elapsed is not { } elapsed)
            return 0;

        return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0, 1);
    }
}

internal static class HelpLine
{
    public static IRenderable Render(bool showHelp, bool showControls, int width)
    {
        string text;
        if (showControls)
        {
            text = "SPACE play/pause | Left/Right seek | Up/Down scroll queue | click song play";
            return new Markup(StyledHelp(text, width, true));
        }

        if (showHelp)
        {
            text = "add <url/search> | goto <#|title> focus | shuffle randomize | q quit";
            return new Markup(StyledHelp(text, width, false));
        }

        text = "SPACE play/pause | Up/Down queue | ENTER/click play | help commands | q quit";
        return new Markup(StyledHelp(text, width, true));
    }

    private static string StyledHelp(string text, int width, bool mutedFirst)
    {
        var display = TerminalText.PadPlain(TerminalText.TrimForWidget(text, width), width);
        var escaped = Markup.Escape(display);
        return mutedFirst
            ? $"[grey70]{escaped}[/]"
            : $"[{TerminalStyles.Accent}]{escaped}[/]";
    }
}

internal static class InputPrompt
{
    private const int PrefixWidth = 10;

    public static IRenderable Render(string input, int width)
        => new Markup(PromptMarkup(input, width));

    public static IRenderable RenderLine(int terminalWidth, string input)
    {
        var width = LayoutMetrics.ContentWidth(terminalWidth);
        var leftMargin = LayoutMetrics.LeftMargin(terminalWidth, width);
        return new Markup($"{new string(' ', leftMargin)}{PromptMarkup(input, width)}");
    }

    public static int CursorColumn(int terminalWidth, string input)
    {
        var width = LayoutMetrics.ContentWidth(terminalWidth);
        var leftMargin = LayoutMetrics.LeftMargin(terminalWidth, width);
        var displayInput = DisplayInput(input, width);
        return Math.Clamp(leftMargin + PrefixWidth + TerminalText.VisibleLength(displayInput) + 1, 1, Math.Max(1, terminalWidth));
    }

    private static string DisplayInput(string input, int width) =>
        TerminalText.TailForWidth(input, Math.Max(0, width - PrefixWidth - 1));

    private static string PromptMarkup(string input, int width)
    {
        var displayInput = DisplayInput(input, width);
        var trailing = Math.Max(0, width - PrefixWidth - TerminalText.VisibleLength(displayInput));
        return $"[bold {TerminalStyles.Accent}]popmark[/][grey70] > [/][white]{Markup.Escape(displayInput)}[/]{new string(' ', trailing)}";
    }
}

internal static class MiniPlayerPanel
{
    public static IRenderable Render(RenderContext context)
    {
        var snapshot = context.Snapshot;
        var track = snapshot.Current ?? snapshot.Pending.FirstOrDefault();
        var title = Markup.Escape(TerminalText.TrimForWidget(track?.Title ?? "Queue is empty", 58));
        var rows = new Rows(
            new Markup($"[bold {TerminalStyles.Accent}]PopMark[/] [grey70]{snapshot.Status}[/]"),
            new Markup($"[white]{title}[/]"),
            new Markup($"[grey70]{Markup.Escape(context.Notice)}[/]"));

        return Align.Center(new Panel(rows)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(TerminalStyles.Accent),
            Padding = new Padding(1, 0),
            Width = 72
        }, VerticalAlignment.Middle);
    }
}

internal static class AudioBeaconLogo
{
    private static readonly string[] LargeTapeDeck =
    [
        "         _____________ _I-I__ __",
        "        /       ____  \"-|_|-.\\  \\",
        "       /  __,--'    `--._ _  \\\\  \\",
        "      /,-'      ,-\"-.      `-.\\\\  \\",
        "     /(        (  ^  )       ) \\\\  \\",
        "    /  `m.__    `-.-'   __.m'  _))  \\",
        "   / ____`\"\"mm.______,mm\"\"'   /_/-'  \\",
        "  / /___/ === `\"\"\"\"\"\"'    II  `w      \\",
        " /_____________________________________\\",
        "|_______________________________________|"
    ];

    private static readonly string[] CompactTapeDeck =
    [
        "      __________ _I-I_",
        "     /     ___  \"|_|.\\",
        "    / __,-'   `-._ \\\\",
        "   /,'    ,-\"-.   `\\\\",
        "  /(     ( ^ )    )\\",
        " / `m.__  `-' __.m' _))\\",
        " /___ === `\"\"\"\"'  II `w \\",
        "/________________________\\",
        "|________________________|"
    ];

    private static readonly string[] NarrowTapeDeck =
    [
        "   _________",
        "  /  _I-I_  \\",
        " /  ( ^ )  \\",
        "|  === II  |",
        "|___________|"
    ];

    public static IReadOnlyList<string> Lines(int frame, int availableWidth)
    {
        var large = AnimateReel(NormalizeArt(LargeTapeDeck), frame);
        if (availableWidth >= large[0].Length)
            return large;

        var compact = AnimateReel(NormalizeArt(CompactTapeDeck), frame);
        if (availableWidth >= compact[0].Length)
            return compact;

        return AnimateReel(NormalizeArt(NarrowTapeDeck), frame);
    }

    public static string StyleFor(int frame, int row, int column, char character, string line) =>
        TapeAccentStyle(frame, row, column, character, line) ?? BaseTapeStyle(row);

    private static string BaseTapeStyle(int row) =>
        row switch
        {
            < 3 => TerminalStyles.Secondary,
            < 8 => TerminalStyles.Accent,
            _ => "grey35"
        };

    private static string? TapeAccentStyle(int frame, int row, int column, char character, string line)
    {
        if (character is '◐' or '◓' or '◑' or '◒')
            return "white";

        if (character is '=' or 'I' or '|')
            return frame % 2 == 0 ? TerminalStyles.Accent : TerminalStyles.Secondary;

        if (IsBaseLineScan(column, character, line, frame))
            return "white";

        if (character is '_' or '/' or '\\')
            return (row + column) % 5 == 0 ? TerminalStyles.Secondary : null;

        return null;
    }

    private static bool IsBaseLineScan(int column, char character, string line, int frame)
    {
        if (character != '_' ||
            !line.StartsWith('|') ||
            line.LastIndexOf('|') <= 0)
        {
            return false;
        }

        var start = line.IndexOf('_');
        var end = line.LastIndexOf('_');
        if (start < 0 || end < start)
            return false;

        const int segmentWidth = 4;
        var availableWidth = end - start + 1;
        var maxPosition = Math.Max(0, availableWidth - segmentWidth);
        var progressPosition = maxPosition == 0 ? 0 : frame % (maxPosition + 1);
        var scanStart = start + progressPosition;
        var scanEnd = Math.Min(end, scanStart + segmentWidth - 1);
        return column >= scanStart && column <= scanEnd;
    }

    private static string[] AnimateReel(IReadOnlyList<string> art, int frame)
    {
        var reel = (frame % 4) switch
        {
            0 => '◐',
            1 => '◓',
            2 => '◑',
            _ => '◒'
        };

        return art.Select(line => line.Replace('^', reel)).ToArray();
    }

    private static string[] NormalizeArt(IReadOnlyList<string> art)
    {
        var width = art.Max(line => line.Length);
        return art.Select(line => line.PadRight(width)).ToArray();
    }
}

internal static class LayoutMetrics
{
    private const int PreferredMinimumWidth = 80;

    public static int ContentWidth(int terminalWidth)
    {
        var width = terminalWidth > 0 ? terminalWidth : PreferredMinimumWidth;
        return Math.Max(1, width - 4);
    }

    public static int LeftMargin(int terminalWidth, int contentWidth) =>
        Math.Max(0, ((terminalWidth > 0 ? terminalWidth : contentWidth) - contentWidth) / 2);
}
