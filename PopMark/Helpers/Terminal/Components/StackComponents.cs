using PopMark.Helpers.Terminal.Rendering;

namespace PopMark.Helpers.Terminal.Components;

/// <summary>Vertical flex layout with fixed and weighted children.</summary>
internal sealed class VerticalStackComponent : UiComponent
{
    private readonly List<StackEntry> _entries = [];

    public int Spacing { get; set; }

    public VerticalStackComponent Add(UiComponent child, int? height = null)
    {
        _entries.Add(new StackEntry(child, height, 0));
        return this;
    }

    public VerticalStackComponent AddFlex(UiComponent child, int weight = 1)
    {
        _entries.Add(new StackEntry(child, null, Math.Max(1, weight)));
        return this;
    }

    public override UiSize Measure(UiSize available)
    {
        var width = PreferredWidth ?? available.Width;
        var visible = _entries.Where(entry => entry.Component.Visible).ToList();
        var fixedHeight = 0;
        var flexHeight = 0;

        foreach (var entry in visible)
        {
            if (entry.Weight > 0)
            {
                flexHeight = Math.Max(flexHeight, entry.Component.Measure(new UiSize(width, available.Height)).Height);
                continue;
            }

            var measured = entry.FixedHeight ?? entry.Component.Measure(new UiSize(width, available.Height)).Height;
            fixedHeight += Math.Max(0, measured);
        }

        var spacing = Math.Max(0, visible.Count - 1) * Math.Max(0, Spacing);
        var height = PreferredHeight ?? fixedHeight + flexHeight + spacing;
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, height))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        var visible = _entries.Where(entry => entry.Component.Visible).ToList();
        if (visible.Count == 0 || bounds.IsEmpty)
            return;

        var spacing = Math.Max(0, Spacing);
        var totalSpacing = Math.Max(0, visible.Count - 1) * spacing;
        var fixedTotal = visible
            .Where(entry => entry.Weight <= 0)
            .Sum(entry => Math.Max(0, entry.FixedHeight ?? entry.Component.Measure(bounds.Size).Height));
        var flexWeight = visible.Where(entry => entry.Weight > 0).Sum(entry => entry.Weight);
        var flexRemaining = Math.Max(0, bounds.Height - totalSpacing - fixedTotal);
        var flexHeights = new int[visible.Count];
        var remainingHeight = flexRemaining;
        var remainingWeight = flexWeight;

        for (var index = 0; index < visible.Count; index++)
        {
            var entry = visible[index];
            if (entry.Weight <= 0 || remainingWeight <= 0)
                continue;

            var height = remainingHeight * entry.Weight / remainingWeight;
            flexHeights[index] = height;
            remainingHeight -= height;
            remainingWeight -= entry.Weight;
        }

        var y = bounds.Y;
        for (var index = 0; index < visible.Count; index++)
        {
            var entry = visible[index];
            var height = entry.Weight > 0
                ? flexHeights[index]
                : Math.Max(0, entry.FixedHeight ?? entry.Component.Measure(bounds.Size).Height);

            if (height > 0 && y < bounds.Bottom)
            {
                var clippedHeight = Math.Min(height, bounds.Bottom - y);
                entry.Component.Render(screen, new UiRect(bounds.X, y, bounds.Width, clippedHeight), context);
            }

            y += height;
            if (index < visible.Count - 1)
                y = Math.Min(bounds.Bottom, y + spacing);
        }
    }

    private readonly record struct StackEntry(UiComponent Component, int? FixedHeight, int Weight);
}

/// <summary>Horizontal flex layout with fixed and weighted children.</summary>
internal sealed class HorizontalStackComponent : UiComponent
{
    private readonly List<StackEntry> _entries = [];

    public int Spacing { get; set; }

    public HorizontalStackComponent Add(UiComponent child, int? width = null)
    {
        _entries.Add(new StackEntry(child, width, 0));
        return this;
    }

    public HorizontalStackComponent AddFlex(UiComponent child, int weight = 1)
    {
        _entries.Add(new StackEntry(child, null, Math.Max(1, weight)));
        return this;
    }

    public override UiSize Measure(UiSize available)
    {
        var height = PreferredHeight ?? available.Height;
        var visible = _entries.Where(entry => entry.Component.Visible).ToList();
        var fixedWidth = 0;
        var flexWidth = 0;

        foreach (var entry in visible)
        {
            if (entry.Weight > 0)
            {
                flexWidth = Math.Max(flexWidth, entry.Component.Measure(new UiSize(available.Width, height)).Width);
                continue;
            }

            var measured = entry.FixedWidth ?? entry.Component.Measure(new UiSize(available.Width, height)).Width;
            fixedWidth += Math.Max(0, measured);
        }

        var spacing = Math.Max(0, visible.Count - 1) * Math.Max(0, Spacing);
        var width = PreferredWidth ?? fixedWidth + flexWidth + spacing;
        return new UiSize(
            Math.Max(0, Math.Min(available.Width, Math.Max(MinWidth, width))),
            Math.Max(0, Math.Min(available.Height, Math.Max(MinHeight, height))));
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        var visible = _entries.Where(entry => entry.Component.Visible).ToList();
        if (visible.Count == 0 || bounds.IsEmpty)
            return;

        var spacing = Math.Max(0, Spacing);
        var totalSpacing = Math.Max(0, visible.Count - 1) * spacing;
        var fixedTotal = visible
            .Where(entry => entry.Weight <= 0)
            .Sum(entry => Math.Max(0, entry.FixedWidth ?? entry.Component.Measure(bounds.Size).Width));
        var flexWeight = visible.Where(entry => entry.Weight > 0).Sum(entry => entry.Weight);
        var flexRemaining = Math.Max(0, bounds.Width - totalSpacing - fixedTotal);
        var flexWidths = new int[visible.Count];
        var remainingWidth = flexRemaining;
        var remainingWeight = flexWeight;

        for (var index = 0; index < visible.Count; index++)
        {
            var entry = visible[index];
            if (entry.Weight <= 0 || remainingWeight <= 0)
                continue;

            var width = remainingWidth * entry.Weight / remainingWeight;
            flexWidths[index] = width;
            remainingWidth -= width;
            remainingWeight -= entry.Weight;
        }

        var x = bounds.X;
        for (var index = 0; index < visible.Count; index++)
        {
            var entry = visible[index];
            var width = entry.Weight > 0
                ? flexWidths[index]
                : Math.Max(0, entry.FixedWidth ?? entry.Component.Measure(bounds.Size).Width);

            if (width > 0 && x < bounds.Right)
            {
                var clippedWidth = Math.Min(width, bounds.Right - x);
                entry.Component.Render(screen, new UiRect(x, bounds.Y, clippedWidth, bounds.Height), context);
            }

            x += width;
            if (index < visible.Count - 1)
                x = Math.Min(bounds.Right, x + spacing);
        }
    }

    private readonly record struct StackEntry(UiComponent Component, int? FixedWidth, int Weight);
}

/// <summary>Places a child inside a slot using horizontal/vertical alignment.</summary>
internal sealed class AlignComponent : UiComponent
{
    public AlignComponent(UiComponent child)
    {
        Child = child;
    }

    public UiComponent Child { get; }

    public UiHorizontalAlignment Horizontal { get; set; } = UiHorizontalAlignment.Left;

    public UiVerticalAlignment Vertical { get; set; } = UiVerticalAlignment.Top;

    public override UiSize Measure(UiSize available)
    {
        var desired = Child.Measure(available);
        if (Horizontal == UiHorizontalAlignment.Stretch)
            desired = desired with { Width = available.Width };
        if (Vertical == UiVerticalAlignment.Stretch)
            desired = desired with { Height = available.Height };

        return desired.Clamp(available);
    }

    public override void Render(ScreenBuffer screen, UiRect bounds, UiRenderContext context)
    {
        if (bounds.IsEmpty)
            return;

        var desired = Child.Measure(bounds.Size);
        var width = Horizontal == UiHorizontalAlignment.Stretch
            ? bounds.Width
            : Math.Min(bounds.Width, Math.Max(Child.MinWidth, desired.Width));
        var height = Vertical == UiVerticalAlignment.Stretch
            ? bounds.Height
            : Math.Min(bounds.Height, Math.Max(Child.MinHeight, desired.Height));

        var x = Horizontal switch
        {
            UiHorizontalAlignment.Center => bounds.X + (bounds.Width - width) / 2,
            UiHorizontalAlignment.Right => bounds.Right - width,
            _ => bounds.X
        };

        var y = Vertical switch
        {
            UiVerticalAlignment.Middle => bounds.Y + (bounds.Height - height) / 2,
            UiVerticalAlignment.Bottom => bounds.Bottom - height,
            _ => bounds.Y
        };

        Child.Render(screen, new UiRect(x, y, width, height), context);
    }
}
