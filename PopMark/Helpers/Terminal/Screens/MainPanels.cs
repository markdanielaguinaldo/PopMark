using PopMark.Helpers.Terminal.Components;
using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;

namespace PopMark.Helpers.Terminal;

/// <summary>Top identity/status bar.</summary>
internal sealed class HeaderComponent : UiComponent
{
    public override UiSize Measure(UiSize available) =>
        new(Math.Max(0, available.Width), Math.Min(1, Math.Max(0, available.Height)));

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        screen.Fill(bounds, context.Theme.SurfaceMuted);

        var snapshot = context.Snapshot;
        var statusKind = Status(context.Snapshot);
        var statusLabel = StatusIndicatorComponent.LabelFor(statusKind);
        var left = new List<TextSpan>
        {
            new("PopMark", context.Theme.Accent.WithBold()),
            new("  "),
            new(statusLabel, context.Theme.ForStatus(statusKind))
        };

        var queueCount = snapshot.Previous.Count + snapshot.Pending.Count + (snapshot.Current is null ? 0 : 1);
        var middle = new List<TextSpan>
        {
            new("Queue", context.Theme.Faint),
            new(" "),
            new(queueCount.ToString(), context.Theme.Text)
        };

        var elapsed = PlaybackProgressComponent.FormatDuration(snapshot.Elapsed);
        var right = new List<TextSpan>
        {
            new("Elapsed", context.Theme.Faint),
            new(" "),
            new(elapsed, context.Theme.Muted)
        };

        var leftWidth = Width(left);
        var middleWidth = Width(middle);
        var rightWidth = Width(right);
        var middleStart = Math.Max(bounds.X + leftWidth + 2, bounds.X + (bounds.Width - middleWidth) / 2);
        var rightStart = bounds.Right - rightWidth;
        var useMiddle = middleStart + middleWidth + 2 <= rightStart;
        var maxLeft = Math.Max(0, (useMiddle ? middleStart : rightStart) - bounds.X - 1);

        if (leftWidth <= maxLeft)
        {
            screen.WriteSpans(bounds.X, bounds.Y, left, context.Theme.Text, maxLeft);
        }
        else
        {
            var fallback = TerminalText.TrimForWidget($"PopMark {statusLabel}", maxLeft);
            screen.WriteText(bounds.X, bounds.Y, fallback, context.Theme.Accent, maxLeft);
        }

        if (useMiddle)
            screen.WriteSpans(middleStart, bounds.Y, middle, context.Theme.Text, rightStart - middleStart);

        if (rightStart >= bounds.X)
            screen.WriteSpans(rightStart, bounds.Y, right, context.Theme.Text, bounds.Right - rightStart);
    }

    internal static UiStatusKind Status(PlayerSnapshot snapshot) =>
        snapshot.Status == PlaybackStatus.Stopped && snapshot.Current is null
            ? UiStatusKind.Ready
            : StatusIndicatorComponent.FromPlayback(snapshot.Status);

    private static int Width(IEnumerable<TextSpan> spans) =>
        spans.Sum(span => TerminalText.VisibleLength(span.Text));
}

/// <summary>Now-playing card with metadata, status and the owl artwork.</summary>
internal sealed class NowPlayingComponent : UiComponent
{
    public override UiSize Measure(UiSize available) => available;

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var snapshot = context.Snapshot;
        var track = snapshot.Current ?? snapshot.Pending.FirstOrDefault();
        var contentHeight = Math.Max(0, bounds.Height - 2);
        var contentWidth = Math.Max(0, bounds.Width - 4);
        var statusKind = HeaderComponent.Status(snapshot);

        var textColumn = new VerticalStackComponent();
        textColumn.Add(new TextComponent
        {
            Text = track?.Title ?? "Nothing loaded",
            Style = context.Theme.Strong
        }, 1);

        if (contentHeight >= 2)
        {
            textColumn.Add(new TextComponent
            {
                Text = track?.DisplaySource ?? "add <url or search>",
                Style = context.Theme.Muted
            }, 1);
        }

        if (contentHeight >= 3)
        {
            var statusRow = new HorizontalStackComponent { Spacing = 1 };
            statusRow.Add(new StatusIndicatorComponent
            {
                Kind = statusKind,
                Label = StatusIndicatorComponent.LabelFor(statusKind)
            }, 12);
            statusRow.AddFlex(new TextComponent
            {
                Text = context.Notice,
                Style = context.Theme.Muted
            }, 1);
            textColumn.Add(statusRow, 1);
        }

        if (contentHeight >= 4)
        {
            textColumn.Add(new TextComponent
            {
                Text = $"Elapsed {PlaybackProgressComponent.FormatDuration(snapshot.Elapsed)}  ·  Duration {PlaybackProgressComponent.FormatDuration(track?.Duration)}",
                Style = context.Theme.Faint
            }, 1);
        }

        var content = new HorizontalStackComponent { Spacing = 2 };
        content.AddFlex(textColumn, 1);

        if (contentWidth >= 48 && contentHeight >= 4)
        {
            content.Add(new OwlComponent
            {
                Status = snapshot.Status,
                Frame = context.AnimationFrame
            }, 20);
        }

        new PanelComponent
        {
            Title = "Now Playing",
            Child = content,
            Border = UiBorderStyle.Rounded,
            BorderStyle = context.Theme.BorderAccent,
            Background = context.Theme.Surface,
            Padding = Thickness.Horizontal(1),
            TitleStyle = context.Theme.Accent
        }.Render(screen, bounds, context);
    }
}

/// <summary>Queue card. The list inside owns scrolling, rows and row hitboxes.</summary>
internal sealed class QueuePanelComponent : UiComponent
{
    public override UiSize Measure(UiSize available) => available;

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var snapshot = context.Snapshot;
        var total = snapshot.Previous.Count + snapshot.Pending.Count + (snapshot.Current is null ? 0 : 1);
        var visibleRows = Math.Max(1, bounds.Height - 2);
        var maxOffset = Math.Max(0, total - visibleRows);
        var offset = Math.Clamp(context.App.QueueScrollOffset, 0, maxOffset);
        var title = total == 0
            ? "Queue"
            : total > visibleRows
                ? $"Queue {offset + 1}-{Math.Min(offset + visibleRows, total)} / {total}"
                : $"Queue {total}";

        new PanelComponent
        {
            Title = title,
            Child = new QueueListComponent
            {
                ScrollOffset = context.App.QueueScrollOffset,
                SelectedTrackIndex = snapshot.Current is null ? null : snapshot.Previous.Count
            },
            Border = UiBorderStyle.Rounded,
            BorderStyle = context.Theme.Border,
            Background = context.Theme.Surface,
            Padding = Thickness.Horizontal(1),
            TitleStyle = context.Theme.Accent
        }.Render(screen, bounds, context);
    }
}

/// <summary>Playback card with the waveform and progress/volume row.</summary>
internal sealed class PlaybackPanelComponent : UiComponent
{
    public override UiSize Measure(UiSize available) => available;

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var innerHeight = Math.Max(0, bounds.Height - 2);
        var content = new VerticalStackComponent();

        if (innerHeight >= 2)
        {
            content.Add(new WaveformComponent
            {
                Status = context.Snapshot.Status,
                Frame = context.AnimationFrame
            }, 1);
        }

        if (innerHeight >= 1)
            content.Add(new PlaybackProgressComponent(), 1);

        new PanelComponent
        {
            Title = "Playback",
            Child = content,
            Border = UiBorderStyle.Rounded,
            BorderStyle = context.Theme.Border,
            Background = context.Theme.Surface,
            Padding = Thickness.Horizontal(1),
            TitleStyle = context.Theme.Accent
        }.Render(screen, bounds, context);
    }
}

/// <summary>Keyboard/mouse hint bar.</summary>
internal sealed class FooterComponent : UiComponent
{
    public override UiSize Measure(UiSize available) =>
        new(Math.Max(0, available.Width), Math.Min(1, Math.Max(0, available.Height)));

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        screen.Fill(bounds, context.Theme.Page);

        var text = context.App.ShowControls
            ? "TAB splash/playlist  ·  SPACE play/pause  ·  [/] prev/next  ·  ←/→ seek"
            : context.App.ShowHelp
                ? "add <url/search>  ·  goto <#|title>  ·  shuffle  ·  tools  ·  q quit"
                : "TAB splash/playlist  ·  SPACE play/pause  ·  [/] prev/next  ·  help commands  ·  q quit";

        var spans = new List<TextSpan>();
        var parts = text.Split("  ·  ");
        for (var index = 0; index < parts.Length; index++)
        {
            if (index > 0)
                spans.Add(new TextSpan("  ·  ", context.Theme.Faint));

            var part = parts[index];
            var space = part.IndexOf(' ');
            if (space > 0)
            {
                spans.Add(new TextSpan(part[..space], context.Theme.Accent));
                spans.Add(new TextSpan(part[space..], context.Theme.Muted));
            }
            else
            {
                spans.Add(new TextSpan(part, context.Theme.Accent));
            }
        }

        screen.WriteSpans(bounds.X, bounds.Y, spans, context.Theme.Muted, bounds.Width);
    }
}

/// <summary>Compact centered player used by the existing mini-player entry point.</summary>
internal sealed class MiniPlayerScreenComponent : UiComponent
{
    public override UiSize Measure(UiSize available) => available;

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        screen.Fill(bounds, context.Theme.Page);

        var snapshot = context.Snapshot;
        var track = snapshot.Current ?? snapshot.Pending.FirstOrDefault();
        var statusKind = HeaderComponent.Status(snapshot);
        var header = new List<TextSpan>
        {
            new("PopMark", context.Theme.Accent.WithBold()),
            new("  "),
            new(StatusIndicatorComponent.LabelFor(statusKind), context.Theme.ForStatus(statusKind))
        };

        var content = new VerticalStackComponent();
        content.Add(new StyledTextComponent { Spans = header, BaseStyle = context.Theme.Text }, 1);
        content.Add(new TextComponent
        {
            Text = track?.Title ?? "Queue is empty",
            Style = context.Theme.Strong
        }, 1);
        content.Add(new TextComponent
        {
            Text = context.Notice,
            Style = context.Theme.Muted
        }, 1);

        var width = Math.Max(1, Math.Min(bounds.Width - 4, 72));
        var panel = new PanelComponent
        {
            Child = content,
            Border = UiBorderStyle.Rounded,
            BorderStyle = context.Theme.BorderAccent,
            Background = context.Theme.Surface,
            Padding = Thickness.Horizontal(1),
            PreferredWidth = width,
            PreferredHeight = 5
        };

        new AlignComponent(panel)
        {
            Horizontal = UiHorizontalAlignment.Center,
            Vertical = UiVerticalAlignment.Middle
        }.Render(screen, bounds, context);
    }
}
