using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;

namespace PopMark.Helpers.Terminal.Components;

/// <summary>Braille waveform used by the playback panel.</summary>
internal sealed class WaveformComponent : UiComponent
{
    private static readonly string[] Glyphs = ["⣀", "⣄", "⣆", "⣇", "⣧", "⣷", "⣿"];

    public PlaybackStatus Status { get; set; } = PlaybackStatus.Stopped;

    public int Frame { get; set; }

    public override UiSize Measure(UiSize available) => new(Math.Max(0, available.Width), 1);

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var active = Status is PlaybackStatus.Playing or PlaybackStatus.Loading;
        var style = active ? context.Theme.Accent : context.Theme.Faint;
        var frame = Frame != 0 ? Frame : context.AnimationFrame;

        if (!active)
        {
            screen.WriteText(bounds.X, bounds.Y, new string('⣀', bounds.Width), style, bounds.Width);
            return;
        }

        for (var index = 0; index < bounds.Width; index++)
        {
            var current = WaveLevel(frame, index);
            var previous = WaveLevel(frame - 1, index);
            var smoothed = (int)Math.Round(previous + (current - previous) * 0.55);
            var glyph = Glyphs[Math.Clamp(smoothed, 0, Glyphs.Length - 1)];
            screen.WriteText(bounds.X + index, bounds.Y, glyph, style, 1);
        }
    }

    private static int WaveLevel(int frame, int index)
    {
        var phase = (frame + index * 2) % 14;
        return phase <= 7 ? phase - 1 : 13 - phase;
    }
}

/// <summary>The small PopMark owl, with the existing blink/sleep animation.</summary>
internal sealed class OwlComponent : UiComponent
{
    public PlaybackStatus Status { get; set; } = PlaybackStatus.Stopped;

    public int Frame { get; set; }

    public override UiSize Measure(UiSize available) =>
        new(Math.Min(20, Math.Max(0, available.Width)), Math.Min(4, Math.Max(0, available.Height)));

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var expression = ExpressionFor(Frame != 0 ? Frame : context.AnimationFrame, Status);
        var eyes = expression switch
        {
            OwlExpression.Sleep => "z,z",
            OwlExpression.Blink => "-,-",
            _ => "o,o"
        };

        var head = "           ^...^";
        var face = $"          / {eyes} \\";
        var body = "          |):::(|";
        var feet = "        ----w-w----";

        WriteLine(screen, bounds, 0, head, context.Theme.Muted);
        WriteLine(screen, bounds, 1, face, context.Theme.Muted, context.Theme.Accent, 14, 3);
        WriteLine(screen, bounds, 2, body + " ", context.Theme.Muted, context.Theme.Secondary, 11, 2);
        WriteLine(screen, bounds, 3, feet, context.Theme.Muted, context.Theme.Secondary, 12, 3);
    }

    private static void WriteLine(
        ScreenBuffer screen,
        UiRect bounds,
        int row,
        string text,
        CellStyle baseStyle,
        CellStyle? highlightStyle = null,
        int highlightStart = -1,
        int highlightLength = 0)
    {
        if (bounds.Y + row >= bounds.Bottom)
            return;

        var width = Math.Min(text.Length, bounds.Width);
        for (var index = 0; index < width; index++)
        {
            var style = highlightStyle is { } highlight &&
                        highlightStart >= 0 &&
                        index >= highlightStart &&
                        index < highlightStart + highlightLength
                ? highlight
                : baseStyle;
            screen.Set(bounds.X + index, bounds.Y + row, new System.Text.Rune(text[index]), style);
        }
    }

    private static OwlExpression ExpressionFor(int frame, PlaybackStatus status)
    {
        if (status != PlaybackStatus.Playing)
            return OwlExpression.Blink;

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
}
