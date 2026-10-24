using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;

namespace PopMark.Helpers.Terminal.Components;

/// <summary>A single line of styled text.</summary>
internal sealed class TextComponent : UiComponent
{
    public string Text { get; set; } = string.Empty;

    public CellStyle Style { get; set; } = UiTheme.Default.Text;

    public UiTextAlignment Alignment { get; set; } = UiTextAlignment.Left;

    public bool AddEllipsis { get; set; } = true;

    public override UiSize Measure(UiSize available)
    {
        var width = TerminalText.VisibleLength(Text);
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, PreferredWidth ?? width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, PreferredHeight ?? 1))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var text = AddEllipsis
            ? TerminalText.TrimForWidget(Text, bounds.Width)
            : Text;
        var width = Math.Min(bounds.Width, TerminalText.VisibleLength(text));
        var x = Align(bounds, width);
        screen.WriteText(x, bounds.Y, text, Style, bounds.Width - (x - bounds.X));
    }

    private int Align(UiRect bounds, int width) =>
        Alignment switch
        {
            UiTextAlignment.Center => bounds.X + (bounds.Width - width) / 2,
            UiTextAlignment.Right => bounds.Right - width,
            _ => bounds.X
        };
}

/// <summary>A line made of multiple styled spans.</summary>
internal sealed class StyledTextComponent : UiComponent
{
    public IReadOnlyList<TextSpan> Spans { get; set; } = Array.Empty<TextSpan>();

    public CellStyle BaseStyle { get; set; } = UiTheme.Default.Text;

    public UiTextAlignment Alignment { get; set; } = UiTextAlignment.Left;

    public override UiSize Measure(UiSize available)
    {
        var width = Spans.Sum(span => TerminalText.VisibleLength(span.Text));
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, PreferredWidth ?? width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, PreferredHeight ?? 1))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty || Spans.Count == 0)
            return;

        var total = Spans.Sum(span => TerminalText.VisibleLength(span.Text));
        var visible = Math.Min(total, bounds.Width);
        var x = Alignment switch
        {
            UiTextAlignment.Center => bounds.X + (bounds.Width - visible) / 2,
            UiTextAlignment.Right => bounds.Right - visible,
            _ => bounds.X
        };

        screen.WriteSpans(x, bounds.Y, Spans, BaseStyle, bounds.Width - (x - bounds.X));
    }
}

/// <summary>
/// In-place animated spinner. The frame is derived from the shared animation
/// clock, so there is no background loop per spinner and stopping is immediate.
/// </summary>
internal sealed class SpinnerComponent : UiComponent
{
    public static readonly string[] DefaultFrames =
    [
        "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"
    ];

    public IReadOnlyList<string> Frames { get; set; } = DefaultFrames;

    public int IntervalMilliseconds { get; set; } = 80;

    public string Text { get; set; } = string.Empty;

    public CellStyle Style { get; set; } = UiTheme.Default.Accent;

    public CellStyle TextStyle { get; set; } = UiTheme.Default.Muted;

    public bool IsRunning { get; set; } = true;

    /// <summary>Deterministic frame override used by tests.</summary>
    public int? FrameOverride { get; set; }

    public int FrameAt(TimeSpan elapsed)
    {
        if (Frames.Count == 0)
            return 0;

        var interval = Math.Max(1, IntervalMilliseconds);
        var frame = (long)(Math.Max(0, elapsed.TotalMilliseconds) / interval);
        return (int)(frame % Frames.Count);
    }

    /// <summary>Stops the animation. The component keeps rendering its first frame.</summary>
    public void Stop() => IsRunning = false;

    /// <summary>Cancellation-compatible alias for <see cref="Stop"/>.</summary>
    public void Cancel() => Stop();

    public override UiSize Measure(UiSize available)
    {
        var frameWidth = Frames.Count == 0 ? 0 : TerminalText.VisibleLength(Frames[0]);
        var width = frameWidth + (string.IsNullOrEmpty(Text) ? 0 : frameWidth == 0 ? 0 : 1) +
                    TerminalText.VisibleLength(Text);
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, PreferredWidth ?? width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, PreferredHeight ?? 1))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty || Frames.Count == 0)
            return;

        var frameIndex = IsRunning
            ? FrameOverride ?? FrameAt(context.AnimationTime)
            : 0;
        var frame = Frames[Math.Clamp(frameIndex, 0, Frames.Count - 1)];
        var x = bounds.X;
        var written = screen.WriteText(x, bounds.Y, frame, Style, bounds.Width);
        x += written;

        if (!string.IsNullOrEmpty(Text) && x < bounds.Right)
        {
            x += screen.WriteText(x, bounds.Y, " ", TextStyle, bounds.Right - x);
            screen.WriteText(x, bounds.Y, TerminalText.TrimForWidget(Text, bounds.Right - x), TextStyle, bounds.Right - x);
        }
    }
}

/// <summary>Consistent status pill used by the header and now-playing panel.</summary>
internal sealed class StatusIndicatorComponent : UiComponent
{
    private static readonly string[] LoadingFrames = ["◐", "◓", "◑", "◒"];

    public UiStatusKind Kind { get; set; } = UiStatusKind.Ready;

    public string? Label { get; set; }

    public bool ShowLabel { get; set; } = true;

    public bool ShowGlyph { get; set; } = true;

    public static UiStatusKind FromPlayback(PlaybackStatus status) =>
        status switch
        {
            PlaybackStatus.Loading => UiStatusKind.Loading,
            PlaybackStatus.Playing => UiStatusKind.Playing,
            PlaybackStatus.Paused => UiStatusKind.Paused,
            PlaybackStatus.Detached => UiStatusKind.Detached,
            _ => UiStatusKind.Stopped
        };

    public override UiSize Measure(UiSize available)
    {
        var label = Label ?? LabelFor(Kind);
        var width = (ShowGlyph ? 2 : 0) + (ShowLabel ? TerminalText.VisibleLength(label) : 0);
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, PreferredWidth ?? width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, PreferredHeight ?? 1))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var style = context.Theme.ForStatus(Kind);
        var x = bounds.X;

        if (ShowGlyph)
        {
            var glyph = Kind == UiStatusKind.Loading
                ? LoadingFrames[(int)(context.AnimationTime.TotalMilliseconds / 120) % LoadingFrames.Length]
                : GlyphFor(Kind);
            x += screen.WriteText(x, bounds.Y, glyph, style, bounds.Right - x);
            if (ShowLabel && x < bounds.Right)
                x += screen.WriteText(x, bounds.Y, " ", style, bounds.Right - x);
        }

        if (ShowLabel && x < bounds.Right)
        {
            var label = Label ?? LabelFor(Kind);
            screen.WriteText(x, bounds.Y, TerminalText.TrimForWidget(label, bounds.Right - x), style, bounds.Right - x);
        }
    }

    private static string GlyphFor(UiStatusKind kind) =>
        kind switch
        {
            UiStatusKind.Ready => "●",
            UiStatusKind.Playing => "●",
            UiStatusKind.Paused => "❚❚",
            UiStatusKind.Error => "●",
            UiStatusKind.Completed => "✓",
            UiStatusKind.Detached => "●",
            _ => "○"
        };

    internal static string LabelFor(UiStatusKind kind) =>
        kind switch
        {
            UiStatusKind.Ready => "READY",
            UiStatusKind.Playing => "PLAYING",
            UiStatusKind.Paused => "PAUSED",
            UiStatusKind.Loading => "LOADING",
            UiStatusKind.Error => "ERROR",
            UiStatusKind.Completed => "COMPLETED",
            UiStatusKind.Detached => "DETACHED",
            _ => "STOPPED"
        };
}
