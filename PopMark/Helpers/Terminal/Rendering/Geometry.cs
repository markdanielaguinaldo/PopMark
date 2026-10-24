namespace PopMark.Helpers.Terminal.Rendering;

/// <summary>A width/height pair used by the layout system.</summary>
internal readonly record struct UiSize(int Width, int Height)
{
    public static readonly UiSize Empty = new(0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public UiSize Clamp(UiSize maximum) =>
        new(Math.Clamp(Width, 0, Math.Max(0, maximum.Width)),
            Math.Clamp(Height, 0, Math.Max(0, maximum.Height)));
}

/// <summary>A zero-based rectangle in screen cells.</summary>
internal readonly record struct UiRect(int X, int Y, int Width, int Height)
{
    public static readonly UiRect Empty = new(0, 0, 0, 0);

    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int Area => Math.Max(0, Width) * Math.Max(0, Height);
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public UiSize Size => new(Width, Height);

    public bool Contains(int x, int y) =>
        x >= X && x < Right && y >= Y && y < Bottom;

    public UiRect Inset(Thickness thickness) => Inset(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom);

    public UiRect Inset(int left, int top, int right, int bottom)
    {
        var width = Math.Max(0, Width - Math.Max(0, left) - Math.Max(0, right));
        var height = Math.Max(0, Height - Math.Max(0, top) - Math.Max(0, bottom));
        return new UiRect(X + Math.Max(0, left), Y + Math.Max(0, top), width, height);
    }

    public UiRect Intersect(UiRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top
            ? Empty
            : new UiRect(left, top, right - left, bottom - top);
    }
}

/// <summary>Cell or panel padding.</summary>
internal readonly record struct Thickness(int Left, int Top, int Right, int Bottom)
{
    public static readonly Thickness Zero = new(0, 0, 0, 0);

    public static Thickness All(int value) => new(value, value, value, value);
    public static Thickness Horizontal(int value) => new(value, 0, value, 0);
    public static Thickness Vertical(int value) => new(0, value, 0, value);
    public static Thickness operator +(Thickness left, Thickness right) =>
        new(left.Left + right.Left, left.Top + right.Top, left.Right + right.Right, left.Bottom + right.Bottom);
}

/// <summary>Horizontal placement inside a layout slot.</summary>
internal enum UiHorizontalAlignment
{
    Left,
    Center,
    Right,
    Stretch
}

/// <summary>Vertical placement inside a layout slot.</summary>
internal enum UiVerticalAlignment
{
    Top,
    Middle,
    Bottom,
    Stretch
}

/// <summary>Horizontal placement of text inside its own bounds.</summary>
internal enum UiTextAlignment
{
    Left,
    Center,
    Right
}
