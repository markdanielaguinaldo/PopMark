using PopMark.Helpers.Terminal.Rendering;
using PopMark.Models;

namespace PopMark.Helpers.Terminal.Components;

internal enum QueueItemKind
{
    Previous,
    Current,
    Pending,
    Message,
    More,
    Empty
}

/// <summary>One row in the queue list.</summary>
internal sealed class QueueItemComponent : UiComponent
{
    public int DisplayIndex { get; set; }

    public string Title { get; set; } = string.Empty;

    public QueueItemKind Kind { get; set; } = QueueItemKind.Pending;

    public int TrackIndex { get; set; } = -1;

    public bool Selected { get; set; }

    public int NumberWidth { get; set; } = 3;

    public override UiSize Measure(UiSize available) => new(Math.Max(0, available.Width), 1);

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        if (Selected)
            screen.Fill(bounds, context.Theme.Selection);
        else if (Kind == QueueItemKind.Current)
            screen.Fill(bounds, context.Theme.CurrentRow);

        var numberWidth = Math.Clamp(NumberWidth, 3, 6);
        var markerWidth = 2;
        var x = bounds.X;

        if (Kind != QueueItemKind.Empty && Kind != QueueItemKind.More && DisplayIndex > 0)
        {
            var numberStyle = Kind == QueueItemKind.Current ? context.Theme.Accent : context.Theme.Faint;
            x += screen.WriteText(x, bounds.Y, $"{DisplayIndex,2}.", numberStyle, numberWidth);
        }
        else
        {
            x += Math.Min(numberWidth, bounds.Right - x);
        }

        var marker = Kind switch
        {
            QueueItemKind.Previous => "✓",
            QueueItemKind.Current => "▶",
            QueueItemKind.Pending => "›",
            QueueItemKind.Message => "•",
            QueueItemKind.More => "+",
            _ => " "
        };

        var markerStyle = Kind switch
        {
            QueueItemKind.Previous => context.Theme.Success,
            QueueItemKind.Current => context.Theme.Accent,
            QueueItemKind.Pending => context.Theme.Muted,
            QueueItemKind.Message => context.Theme.Faint,
            QueueItemKind.More => context.Theme.Faint,
            _ => context.Theme.Faint
        };

        x += screen.WriteText(x, bounds.Y, marker, markerStyle, Math.Min(markerWidth, bounds.Right - x));
        x += screen.WriteText(x, bounds.Y, " ", markerStyle, Math.Max(0, bounds.Right - x));

        var titleWidth = Math.Max(0, bounds.Right - x);
        if (titleWidth > 0)
        {
            var titleStyle = Kind switch
            {
                QueueItemKind.Current => context.Theme.Strong,
                QueueItemKind.Previous => context.Theme.Muted,
                QueueItemKind.Pending => context.Theme.Text,
                QueueItemKind.Message => context.Theme.Muted.WithItalic(),
                QueueItemKind.More => context.Theme.Faint.WithItalic(),
                _ => context.Theme.Faint
            };
            var title = Kind == QueueItemKind.Empty
                ? string.Empty
                : TerminalText.TrimForWidget(Title, titleWidth);
            screen.WriteText(x, bounds.Y, title, titleStyle, titleWidth);
        }

        if (TrackIndex >= 0 && Kind is QueueItemKind.Previous or QueueItemKind.Current or QueueItemKind.Pending)
            context.Hits.Add(HitKind.QueueRow, bounds, TrackIndex);
    }
}

/// <summary>
/// Scrolling queue view. It composes queue item components and leaves the
/// scroll offset and playback state in the application layer.
/// </summary>
internal sealed class QueueListComponent : UiComponent
{
    public int ScrollOffset { get; set; }

    public int? SelectedTrackIndex { get; set; }

    public override UiSize Measure(UiSize available) =>
        new(Math.Max(0, available.Width), Math.Max(0, available.Height));

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var rows = BuildRows(context.Snapshot);
        var offset = Math.Clamp(ScrollOffset, 0, Math.Max(0, rows.Count - bounds.Height));
        var visibleRows = rows.Count == 0
            ? new List<QueueRow> { new(-1, 0, QueueItemKind.Message, "Queue is empty") }
            : rows.Skip(offset).Take(bounds.Height).ToList();

        var hiddenAfter = rows.Count == 0 ? 0 : Math.Max(0, rows.Count - offset - visibleRows.Count);
        if (hiddenAfter > 0 && visibleRows.Count > 0)
        {
            visibleRows.RemoveAt(visibleRows.Count - 1);
            visibleRows.Add(new QueueRow(-1, 0, QueueItemKind.More, $"{hiddenAfter + 1} more"));
        }

        while (visibleRows.Count < bounds.Height)
            visibleRows.Add(new QueueRow(-1, 0, QueueItemKind.Empty, string.Empty));

        var numberWidth = Math.Clamp(rows.Count.ToString().Length + 1, 3, 6);
        for (var row = 0; row < visibleRows.Count && row < bounds.Height; row++)
        {
            var model = visibleRows[row];
            var item = new QueueItemComponent
            {
                DisplayIndex = model.DisplayIndex,
                Title = model.Title,
                Kind = model.Kind,
                TrackIndex = model.TrackIndex,
                Selected = SelectedTrackIndex is { } selected && selected == model.TrackIndex,
                NumberWidth = numberWidth
            };

            item.Render(screen, new UiRect(bounds.X, bounds.Y + row, bounds.Width, 1), context);
        }
    }

    internal static IReadOnlyList<QueueRow> BuildRows(PlayerSnapshot snapshot)
    {
        var rows = new List<QueueRow>();
        var index = 0;

        foreach (var track in snapshot.Previous)
        {
            rows.Add(new QueueRow(index, index + 1, QueueItemKind.Previous, track.Title));
            index++;
        }

        if (snapshot.Current is not null)
        {
            rows.Add(new QueueRow(index, index + 1, QueueItemKind.Current, snapshot.Current.Title));
            index++;
        }

        foreach (var track in snapshot.Pending)
        {
            rows.Add(new QueueRow(index, index + 1, QueueItemKind.Pending, track.Title));
            index++;
        }

        return rows;
    }

    internal sealed record QueueRow(int TrackIndex, int DisplayIndex, QueueItemKind Kind, string Title);
}
