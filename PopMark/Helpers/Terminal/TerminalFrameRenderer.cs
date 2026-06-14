using PopMark.Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;

namespace PopMark.Helpers.Terminal;

internal static class TerminalFrameRenderer
{
    private static IReadOnlyList<string>? _lastRenderedLines;
    private static int _lastRenderedWidth;
    private static int _lastRenderedHeight;
    private static int _lastRenderedTerminalHeight;
    private static ProgressHitbox? _lastProgressHitbox;
    private static IReadOnlyList<PlaylistHitbox> _lastPlaylistHitboxes = [];

    public static void DrawCommandCenter(PlayerSnapshot snapshot, string notice, bool showHelp = false, int queueScrollOffset = 0, bool showControls = false)
    {
        var (width, height) = TerminalHost.GetWindowSize();
        Render(new RenderContext(
            width,
            height,
            snapshot,
            notice,
            Input: string.Empty,
            ShowHelp: showHelp,
            ShowControls: showControls,
            MiniMode: false,
            AnimationFrame: 0,
            QueueScrollOffset: queueScrollOffset));
    }

    public static void DrawMiniPlayer(PlayerSnapshot snapshot, string notice)
    {
        var (width, height) = TerminalHost.GetWindowSize();
        Render(new RenderContext(
            width,
            height,
            snapshot,
            notice,
            Input: string.Empty,
            ShowHelp: false,
            ShowControls: false,
            MiniMode: true,
            AnimationFrame: 0,
            QueueScrollOffset: 0));
    }

    public static void Render(RenderContext context, bool forceFullPaint = false, bool renderInput = true)
    {
        var width = context.Width > 0 ? context.Width : 80;
        var height = context.Height > 0 ? context.Height : 24;
        var mainHeight = context.MiniMode ? height : Math.Max(1, height - 1);
        var normalized = context with { Width = width, Height = height };
        var mainContext = normalized with { Height = mainHeight };
        var lines = RenderToLines(AppLayout.Render(mainContext), width, mainHeight);
        UpdateHitboxes(mainContext);

        if (Console.IsOutputRedirected)
        {
            Console.Write(string.Join(Environment.NewLine, lines));
            Remember(lines, width, mainHeight, height);
            return;
        }

        var fullPaint = forceFullPaint ||
                        _lastRenderedLines is null ||
                        _lastRenderedWidth != width ||
                        _lastRenderedHeight != mainHeight ||
                        _lastRenderedTerminalHeight != height;
        var output = new StringBuilder();
        output.Append("\u001b[?25l\u001b[?7l");
        if (fullPaint)
            output.Append("\u001b[H\u001b[2J");

        for (var i = 0; i < lines.Count; i++)
        {
            var changed = fullPaint ||
                          i >= _lastRenderedLines!.Count ||
                          !string.Equals(lines[i], _lastRenderedLines[i], StringComparison.Ordinal);
            if (!changed)
                continue;

            output.Append($"\u001b[{i + 1};1H");
            output.Append(TerminalText.PadAnsiAware(lines[i], width));
            output.Append("\u001b[K");
        }

        if (renderInput && !context.MiniMode)
            AppendInputLine(output, normalized);

        if (!context.MiniMode && ResolveInputCursor(normalized) is { } cursor)
            output.Append($"\u001b[{cursor.Row};{cursor.Column}H\u001b[?25h");

        output.Append("\u001b[?7h");
        Console.Write(output.ToString());
        Remember(lines, width, mainHeight, height);
    }

    public static void RenderInputLine(int terminalWidth, int terminalHeight, string input)
    {
        if (Console.IsOutputRedirected)
            return;

        var width = terminalWidth > 0 ? terminalWidth : 80;
        var height = terminalHeight > 0 ? terminalHeight : 24;
        var context = new RenderContext(
            width,
            height,
            new PlayerSnapshot(PlaybackStatus.Stopped, null, [], [], null, 100),
            string.Empty,
            input,
            ShowHelp: false,
            ShowControls: false,
            MiniMode: false,
            AnimationFrame: 0,
            QueueScrollOffset: 0);
        var output = new StringBuilder("\u001b[?25l\u001b[?7l");
        AppendInputLine(output, context);

        if (ResolveInputCursor(context) is { } cursor)
            output.Append($"\u001b[{cursor.Row};{cursor.Column}H\u001b[?25h");

        output.Append("\u001b[?7h");
        Console.Write(output.ToString());
    }

    public static bool TryResolveProgressClick(int x, int y, PlayerSnapshot snapshot, out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;
        if (_lastProgressHitbox is not { } hitbox ||
            snapshot.Current?.Duration is not { TotalSeconds: > 0 } duration ||
            y != hitbox.Y ||
            x < hitbox.X ||
            x >= hitbox.X + hitbox.Width)
        {
            return false;
        }

        var ratio = hitbox.Width <= 1
            ? 0
            : (double)(x - hitbox.X) / (hitbox.Width - 1);
        timestamp = TimeSpan.FromSeconds(duration.TotalSeconds * Math.Clamp(ratio, 0, 1));
        return true;
    }

    public static bool TryResolvePlaylistClick(int x, int y, PlayerSnapshot snapshot, out int trackIndex)
    {
        trackIndex = -1;
        var totalTracks = QueueCount(snapshot);
        var hitbox = _lastPlaylistHitboxes.FirstOrDefault(candidate =>
            y == candidate.Y &&
            x >= candidate.X &&
            x < candidate.X + candidate.Width);

        if (hitbox is null || hitbox.TrackIndex < 0 || hitbox.TrackIndex >= totalTracks)
            return false;

        trackIndex = hitbox.TrackIndex;
        return true;
    }

    public static void ResetFrameCache()
    {
        _lastRenderedLines = null;
        _lastRenderedWidth = 0;
        _lastRenderedHeight = 0;
        _lastRenderedTerminalHeight = 0;
        _lastProgressHitbox = null;
        _lastPlaylistHitboxes = [];
    }

    private static void AppendInputLine(StringBuilder output, RenderContext context)
    {
        var width = context.Width > 0 ? context.Width : 80;
        var height = context.Height > 0 ? context.Height : 24;
        var line = RenderToLines(InputPrompt.RenderLine(width, context.Input), width, 1)[0];
        output.Append($"\u001b[{height};1H");
        output.Append(TerminalText.PadAnsiAware(line, width));
        output.Append("\u001b[K");
    }

    private static IReadOnlyList<string> RenderToLines(IRenderable renderable, int width, int height)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Interactive = InteractionSupport.No,
            Out = new FixedSizeAnsiOutput(writer, width, height)
        });

        console.Write(renderable);
        var normalized = writer.ToString().Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        while (lines.Count < height)
            lines.Add(string.Empty);

        if (lines.Count > height)
            lines.RemoveRange(height, lines.Count - height);

        for (var i = 0; i < lines.Count; i++)
            lines[i] = TerminalText.TrimAnsiAware(lines[i], width);

        return lines;
    }

    private static void UpdateHitboxes(RenderContext context)
    {
        if (context.MiniMode)
        {
            _lastProgressHitbox = null;
            _lastPlaylistHitboxes = [];
            return;
        }

        const int headerHeight = 1;
        const int nowPlayingHeight = 6;
        const int playbackHeight = 4;
        const int helpHeight = 1;
        var queuePanelHeight = Math.Max(4, Math.Max(17, context.Height) - headerHeight - nowPlayingHeight - playbackHeight - helpHeight);
        var visibleRows = Math.Max(1, queuePanelHeight - 2);
        var totalTracks = QueueCount(context.Snapshot);
        var offset = Math.Clamp(context.QueueScrollOffset, 0, Math.Max(0, totalTracks - visibleRows));
        var layoutWidth = LayoutMetrics.ContentWidth(context.Width);
        var leftMargin = LayoutMetrics.LeftMargin(context.Width, layoutWidth);
        var queueX = leftMargin + 3;
        var queueY = headerHeight + nowPlayingHeight + 2;
        var queueWidth = Math.Max(1, layoutWidth - 6);
        var renderedTrackRows = totalTracks - offset > visibleRows
            ? Math.Max(0, visibleRows - 1)
            : Math.Min(visibleRows, Math.Max(0, totalTracks - offset));

        _lastPlaylistHitboxes = Enumerable.Range(offset, renderedTrackRows)
            .Select((trackIndex, rowOffset) => new PlaylistHitbox(
                trackIndex,
                queueX,
                queueY + rowOffset,
                queueWidth))
            .ToList();

        _lastProgressHitbox = context.Snapshot.Current?.Duration is { TotalSeconds: > 0 }
            ? new ProgressHitbox(
                leftMargin + 3,
                headerHeight + nowPlayingHeight + queuePanelHeight + 3,
                Math.Max(1, PlaybackPanel.ProgressHitboxWidth(context.Snapshot, layoutWidth)))
            : null;
    }

    private static int QueueCount(PlayerSnapshot snapshot) =>
        snapshot.Previous.Count + snapshot.Pending.Count + (snapshot.Current is null ? 0 : 1);

    private static void Remember(IReadOnlyList<string> lines, int width, int height, int terminalHeight)
    {
        _lastRenderedLines = lines;
        _lastRenderedWidth = width;
        _lastRenderedHeight = height;
        _lastRenderedTerminalHeight = terminalHeight;
    }

    private static CursorPosition? ResolveInputCursor(RenderContext context)
    {
        if (context.MiniMode)
            return null;

        return new CursorPosition(
            InputPrompt.CursorColumn(context.Width, context.Input),
            Math.Clamp(context.Height, 1, Math.Max(1, context.Height)));
    }

    private sealed class FixedSizeAnsiOutput(TextWriter writer, int width, int height) : IAnsiConsoleOutput
    {
        public TextWriter Writer { get; } = writer;

        public bool IsTerminal => true;

        public int Width { get; } = Math.Max(1, width);

        public int Height { get; } = Math.Max(1, height);

        public void SetEncoding(Encoding encoding)
        {
        }
    }

    private sealed record PlaylistHitbox(int TrackIndex, int X, int Y, int Width);

    private sealed record ProgressHitbox(int X, int Y, int Width);

    private sealed record CursorPosition(int Column, int Row);
}
