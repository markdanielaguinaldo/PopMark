namespace PopMark.Helpers.Terminal.Rendering;

internal enum HitKind
{
    QueueRow,
    Progress,
    Volume
}

/// <summary>A zero-based clickable region produced during rendering.</summary>
internal readonly record struct HitRegion(HitKind Kind, UiRect Bounds, int Index = -1)
{
    public bool Contains(int x, int y) => Bounds.Contains(x, y);
}

/// <summary>
/// Collects hitboxes while a component tree renders. The renderer swaps this
/// registry after each frame so mouse coordinates always match the layout that
/// is currently on screen.
/// </summary>
internal sealed class HitRegistry
{
    private readonly List<HitRegion> _regions = [];

    public IReadOnlyList<HitRegion> Regions => _regions;

    public void Add(HitRegion region)
    {
        if (!region.Bounds.IsEmpty)
            _regions.Add(region);
    }

    public void Add(HitKind kind, UiRect bounds, int index = -1) =>
        Add(new HitRegion(kind, bounds, index));

    public HitRegion? Find(HitKind kind, int x, int y)
    {
        foreach (var region in _regions)
        {
            if (region.Kind == kind && region.Contains(x, y))
                return region;
        }

        return null;
    }

    public HitRegion? FindAt(int x, int y)
    {
        foreach (var region in _regions)
        {
            if (region.Contains(x, y))
                return region;
        }

        return null;
    }

    public int? FindQueueRow(int x, int y)
    {
        var region = Find(HitKind.QueueRow, x, y);
        return region is { Index: >= 0 } ? region.Value.Index : null;
    }
}
