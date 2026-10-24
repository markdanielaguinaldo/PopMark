using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;

namespace PopMark.Helpers.Terminal;

/// <summary>
/// Application-facing terminal façade. It builds the component tree, paints into
/// an in-memory <see cref="ScreenBuffer"/> and asks <see cref="ScreenComposer"/>
/// to emit only the cells that changed since the previous frame.
/// </summary>
internal static class TerminalFrameRenderer
{
    private static readonly UiTheme Theme = UiTheme.Default;

    private static ScreenBuffer? _previousFrame;
    private static HitRegistry _lastHits = new();
    private static UiCaret? _lastCaret;
    private static int _frameWidth;
    private static int _frameHeight;

    public static void DrawCommandCenter(PlayerSnapshot snapshot, string notice, bool showHelp = false, int queueScrollOffset = 0, bool showControls = false, bool showSplash = false)
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
            QueueScrollOffset: queueScrollOffset,
            SplashMode: showSplash));
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
        var normalized = context with { Width = width, Height = height };
        var buffer = new ScreenBuffer(width, height);
        var hits = new HitRegistry();
        var uiContext = new UiRenderContext(
            normalized,
            Theme,
            hits,
            TimeSpan.FromMilliseconds(Math.Max(0, normalized.AnimationFrame) * 110d));

        AppLayout.Create(normalized).Render(buffer, new UiRect(0, 0, width, height), uiContext);

        _lastHits = hits;
        _lastCaret = uiContext.Caret;

        if (Console.IsOutputRedirected)
        {
            Console.Write(buffer.ToPlainText());
            _previousFrame = null;
            return;
        }

        var fullPaint = forceFullPaint ||
                        _previousFrame is null ||
                        _frameWidth != width ||
                        _frameHeight != height;
        var showCursor = !normalized.MiniMode && !normalized.SplashMode && uiContext.Caret is not null;
        var output = ScreenComposer.Compose(
            _previousFrame,
            buffer,
            fullPaint,
            useColor: true,
            cursorRow: showCursor ? uiContext.Caret!.Value.Y : null,
            cursorColumn: showCursor ? uiContext.Caret!.Value.X : null,
            showCursor: showCursor);

        Console.Write(output);

        _previousFrame = buffer;
        _frameWidth = width;
        _frameHeight = height;
    }

    public static void RenderInputLine(int terminalWidth, int terminalHeight, string input, int caretIndex = -1)
    {
        if (Console.IsOutputRedirected)
            return;

        var width = terminalWidth > 0 ? terminalWidth : 80;
        var height = terminalHeight > 0 ? terminalHeight : 24;
        var caret = caretIndex < 0 ? input.Length : Math.Clamp(caretIndex, 0, input.Length);

        if (_previousFrame is null || _frameWidth != width || _frameHeight != height)
        {
            Render(new RenderContext(
                width,
                height,
                EmptySnapshot(),
                string.Empty,
                input,
                ShowHelp: false,
                ShowControls: false,
                MiniMode: false,
                AnimationFrame: 0,
                QueueScrollOffset: 0,
                InputCaret: caret), forceFullPaint: true);
            return;
        }

        var contentWidth = LayoutMetrics.ContentWidth(width);
        var leftMargin = LayoutMetrics.LeftMargin(width, contentWidth);
        var row = new ScreenBuffer(width, 1, Theme.Page);
        var rowContext = new UiRenderContext(
            new RenderContext(
                width,
                height,
                EmptySnapshot(),
                string.Empty,
                input,
                ShowHelp: false,
                ShowControls: false,
                MiniMode: false,
                AnimationFrame: 0,
                QueueScrollOffset: 0,
                InputCaret: caret),
            Theme,
            _lastHits,
            TimeSpan.Zero);

        new InputFieldComponent
        {
            Text = input,
            CaretIndex = caret,
            IsFocused = true
        }.Render(row, new UiRect(leftMargin, 0, contentWidth, 1), rowContext);

        var rowIndex = height - 1;
        var output = ScreenComposer.ComposeRow(
            _previousFrame.ExtractRow(rowIndex),
            row,
            terminalRow: height,
            useColor: true,
            cursorColumn: rowContext.Caret?.X,
            showCursor: rowContext.Caret is not null);

        Console.Write(output);
        _previousFrame.ApplyRow(row, rowIndex);
        _lastCaret = rowContext.Caret;
    }

    public static bool TryResolveProgressClick(int x, int y, PlayerSnapshot snapshot, out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;
        if (!ToZeroBased(x, y, out var column, out var row))
            return false;

        var hitbox = _lastHits.Find(HitKind.Progress, column, row);
        if (hitbox is not { } region ||
            snapshot.Current?.Duration is not { TotalSeconds: > 0 } duration)
        {
            return false;
        }

        var ratio = region.Bounds.Width <= 1
            ? 0
            : (double)(column - region.Bounds.X) / (region.Bounds.Width - 1);
        timestamp = TimeSpan.FromSeconds(duration.TotalSeconds * Math.Clamp(ratio, 0, 1));
        return true;
    }

    public static bool TryResolvePlaylistClick(int x, int y, PlayerSnapshot snapshot, out int trackIndex)
    {
        trackIndex = -1;
        if (!ToZeroBased(x, y, out var column, out var row))
            return false;

        var resolved = _lastHits.FindQueueRow(column, row);
        if (resolved is not { } index || index < 0 || index >= QueueCount(snapshot))
            return false;

        trackIndex = index;
        return true;
    }

    public static bool TryResolveVolumeClick(int x, int y, out int volumePercent)
    {
        volumePercent = 0;
        if (!ToZeroBased(x, y, out var column, out var row))
            return false;

        var hitbox = FindVolumeHit(column, row);
        if (hitbox is not { } region)
            return false;

        var clampedX = Math.Clamp(column, region.Bounds.X, region.Bounds.Right - 1);
        var ratio = region.Bounds.Width <= 1
            ? 0
            : (double)(clampedX - region.Bounds.X) / (region.Bounds.Width - 1);
        volumePercent = Math.Clamp((int)Math.Round(ratio * 100), 0, 100);
        return true;
    }

    public static void ResetFrameCache()
    {
        _previousFrame = null;
        _lastHits = new HitRegistry();
        _lastCaret = null;
        _frameWidth = 0;
        _frameHeight = 0;
    }

    private static HitRegion? FindVolumeHit(int x, int y)
    {
        // The volume track is forgiving: clicks slightly outside its ends still
        // clamp to 0% / 100%, matching the previous behaviour.
        foreach (var region in _lastHits.Regions)
        {
            if (region.Kind != HitKind.Volume || y != region.Bounds.Y)
                continue;

            if (x >= region.Bounds.X - 2 && x < region.Bounds.Right + 2)
                return region;
        }

        return null;
    }

    private static bool ToZeroBased(int x, int y, out int column, out int row)
    {
        column = x - 1;
        row = y - 1;
        return column >= 0 && row >= 0;
    }

    private static int QueueCount(PlayerSnapshot snapshot) =>
        snapshot.Previous.Count + snapshot.Pending.Count + (snapshot.Current is null ? 0 : 1);

    private static PlayerSnapshot EmptySnapshot() =>
        new(
            PlaybackStatus.Stopped,
            null,
            Array.Empty<Track>(),
            Array.Empty<Track>(),
            null,
            100);
}
