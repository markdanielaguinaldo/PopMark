using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;

namespace PopMark.Helpers.Terminal.Components;

/// <summary>Determinate or indeterminate progress bar with an optional label.</summary>
internal sealed class ProgressBarComponent : UiComponent
{
    public double Value { get; set; }

    public double Maximum { get; set; } = 1;

    public bool Indeterminate { get; set; }

    public bool Completed { get; set; }

    public string? Label { get; set; }

    public bool ShowPercentage { get; set; }

    public int? BarWidth { get; set; }

    public string FilledGlyph { get; set; } = "█";

    public string EmptyGlyph { get; set; } = "░";

    public CellStyle FilledStyle { get; set; } = UiTheme.Default.Accent;

    public CellStyle EmptyStyle { get; set; } = UiTheme.Default.Faint;

    public CellStyle LabelStyle { get; set; } = UiTheme.Default.Muted;

    public CellStyle PercentageStyle { get; set; } = UiTheme.Default.Muted;

    public bool RegistersProgressHitbox { get; set; }

    public override UiSize Measure(UiSize available)
    {
        var labelWidth = Label is null ? 0 : TerminalText.VisibleLength(Label) + 1;
        var percentageWidth = ShowPercentage ? 5 : 0;
        var barWidth = BarWidth ?? Math.Max(1, available.Width - labelWidth - percentageWidth);
        var width = labelWidth + barWidth + percentageWidth;
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, PreferredWidth ?? width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, PreferredHeight ?? 1))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var labelWidth = Label is null ? 0 : TerminalText.VisibleLength(Label) + 1;
        var percentageWidth = ShowPercentage && bounds.Width >= 4 ? 5 : 0;
        var barWidth = BarWidth ?? Math.Max(0, bounds.Width - labelWidth - percentageWidth);
        barWidth = Math.Min(barWidth, Math.Max(0, bounds.Width - labelWidth - percentageWidth));

        var x = bounds.X;
        if (Label is not null && labelWidth > 0)
        {
            x += screen.WriteText(x, bounds.Y, Label, LabelStyle, labelWidth);
            if (x < bounds.Right)
                x += screen.WriteText(x, bounds.Y, " ", LabelStyle, bounds.Right - x);
        }

        barWidth = Math.Min(barWidth, Math.Max(0, bounds.Right - x - percentageWidth));
        if (barWidth > 0)
        {
            if (RegistersProgressHitbox)
                context.Hits.Add(HitKind.Progress, new UiRect(x, bounds.Y, barWidth, 1));

            DrawBar(screen, x, bounds.Y, barWidth, context);
        }

        if (ShowPercentage && bounds.Width >= 4)
        {
            var percentage = Math.Clamp((int)Math.Round(Ratio() * 100), 0, 100);
            var text = $"{percentage,3}%";
            screen.WriteText(bounds.Right - 4, bounds.Y, text, PercentageStyle, 4);
        }
    }

    private void DrawBar(ScreenBuffer screen, int x, int y, int width, UiRenderContext context)
    {
        var filled = Completed ? width : Math.Clamp((int)Math.Round(Ratio() * width), 0, width);
        var filledStyle = Completed ? context.Theme.Success : FilledStyle;

        if (Indeterminate && !Completed)
        {
            var segment = Math.Max(2, width / 4);
            var cycle = width + segment;
            var offset = (int)(Math.Max(0, context.AnimationTime.TotalMilliseconds) / 90) % Math.Max(1, cycle);

            for (var index = 0; index < width; index++)
            {
                var inside = index >= offset && index < offset + segment;
                screen.Set(x + index, y, new System.Text.Rune(inside ? FilledGlyph[0] : EmptyGlyph[0]),
                    inside ? filledStyle : EmptyStyle);
            }

            return;
        }

        for (var index = 0; index < width; index++)
        {
            var inside = index < filled;
            screen.Set(x + index, y, new System.Text.Rune(inside ? FilledGlyph[0] : EmptyGlyph[0]),
                inside ? filledStyle : EmptyStyle);
        }
    }

    private double Ratio()
    {
        if (Maximum <= 0)
            return 0;

        return Math.Clamp(Value / Maximum, 0, 1);
    }
}

/// <summary>Interactive volume track used in the playback panel and splash view.</summary>
internal sealed class VolumeSliderComponent : UiComponent
{
    public int Value { get; set; } = 100;

    public int Maximum { get; set; } = 130;

    public int TrackWidth { get; set; } = 9;

    public string Label { get; set; } = "VOL";

    public bool ShowPercentage { get; set; } = true;

    public CellStyle LabelStyle { get; set; } = UiTheme.Default.Muted;

    public CellStyle EmptyStyle { get; set; } = UiTheme.Default.Faint;

    public override UiSize Measure(UiSize available)
    {
        var labelWidth = string.IsNullOrEmpty(Label) ? 0 : TerminalText.VisibleLength(Label) + 1;
        var percentageWidth = ShowPercentage ? 5 : 0;
        var width = labelWidth + TrackWidth + percentageWidth;
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, PreferredWidth ?? width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, PreferredHeight ?? 1))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var value = Math.Clamp(Value, 0, Math.Max(0, Maximum));
        var percentageWidth = ShowPercentage && bounds.Width >= 4 ? 5 : 0;
        var labelWidth = string.IsNullOrEmpty(Label) ? 0 : TerminalText.VisibleLength(Label) + 1;
        var trackWidth = Math.Min(TrackWidth, Math.Max(0, bounds.Width - labelWidth - percentageWidth));
        var x = bounds.X;

        if (labelWidth > 0)
        {
            x += screen.WriteText(x, bounds.Y, Label, LabelStyle, labelWidth);
            if (x < bounds.Right)
                x += screen.WriteText(x, bounds.Y, " ", LabelStyle, bounds.Right - x);
        }

        trackWidth = Math.Min(trackWidth, Math.Max(0, bounds.Right - x - percentageWidth));
        var activeStyle = value > 100 ? context.Theme.Boost : context.Theme.Accent;
        if (trackWidth > 0)
        {
            var knob = Math.Clamp(
                (int)Math.Round(Math.Min(value, 100) / 100d * Math.Max(0, trackWidth - 1)),
                0,
                Math.Max(0, trackWidth - 1));

            context.Hits.Add(HitKind.Volume, new UiRect(x, bounds.Y, trackWidth, 1));

            for (var index = 0; index < trackWidth; index++)
            {
                var glyph = index == knob ? '●' : '━';
                var style = index <= knob ? activeStyle : EmptyStyle;
                screen.Set(x + index, bounds.Y, new System.Text.Rune(glyph), style);
            }

            x += trackWidth;
        }

        if (percentageWidth > 0)
            screen.WriteText(bounds.Right - 4, bounds.Y, $"{value,3}%", activeStyle, 4);
    }
}

/// <summary>Playback progress with elapsed/duration and the volume track.</summary>
internal sealed class PlaybackProgressComponent : UiComponent
{
    private const int TimeWidth = 15;
    private const int VolumeWidth = 18;

    public override UiSize Measure(UiSize available) =>
        new(Math.Max(0, available.Width), Math.Max(0, Math.Min(1, available.Height)));

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var snapshot = context.Snapshot;
        var track = snapshot.Current ?? snapshot.Pending.FirstOrDefault();
        var duration = track?.Duration;
        var timeText = $"{FormatDuration(snapshot.Elapsed)} / {FormatDuration(duration)}";
        var showTime = bounds.Width >= 26;
        var showVolume = bounds.Width >= 48;

        var progressWidth = bounds.Width -
                            (showTime ? TimeWidth + 1 : 0) -
                            (showVolume ? VolumeWidth + 1 : 0);

        if (progressWidth < 8 && showVolume)
        {
            showVolume = false;
            progressWidth = bounds.Width - (showTime ? TimeWidth + 1 : 0);
        }

        if (progressWidth < 6 && showTime)
        {
            showTime = false;
            progressWidth = bounds.Width - (showVolume ? VolumeWidth + 1 : 0);
        }

        progressWidth = Math.Max(0, progressWidth);
        var ratio = ProgressRatio(snapshot);
        var indeterminate = duration is not { TotalSeconds: > 0 } &&
                            snapshot.Status is PlaybackStatus.Loading;

        if (progressWidth > 0)
        {
            var progress = new ProgressBarComponent
            {
                Value = ratio,
                Maximum = 1,
                Indeterminate = indeterminate,
                RegistersProgressHitbox = true,
                BarWidth = progressWidth,
                FilledStyle = context.Theme.Accent,
                EmptyStyle = context.Theme.Faint
            };
            progress.Render(screen, new UiRect(bounds.X, bounds.Y, progressWidth, 1), context);
        }

        var x = bounds.X + progressWidth;
        if (showTime && x < bounds.Right)
        {
            x += screen.WriteText(x, bounds.Y, " ", context.Theme.Muted, bounds.Right - x);
            screen.WriteText(x, bounds.Y, TerminalText.TrimForWidget(timeText, TimeWidth), context.Theme.Muted,
                Math.Min(TimeWidth, bounds.Right - x));
            x += TimeWidth;
        }

        if (showVolume && bounds.Right - VolumeWidth >= bounds.X)
        {
            var volume = new VolumeSliderComponent
            {
                Value = snapshot.VolumePercent,
                TrackWidth = 9,
                Label = "VOL",
                ShowPercentage = true,
                LabelStyle = context.Theme.Muted,
                EmptyStyle = context.Theme.Faint
            };
            volume.Render(screen, new UiRect(bounds.Right - VolumeWidth, bounds.Y, VolumeWidth, 1), context);
        }
    }

    internal static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
            return "--:--";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }

    internal static double ProgressRatio(PlayerSnapshot snapshot)
    {
        if (snapshot.Current?.Duration is not { TotalSeconds: > 0 } duration ||
            snapshot.Elapsed is not { } elapsed)
        {
            return 0;
        }

        return Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0, 1);
    }
}
