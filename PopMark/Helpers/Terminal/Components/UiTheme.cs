using PopMark.Helpers.Terminal.Rendering;

namespace PopMark.Helpers.Terminal.Components;

/// <summary>Semantic status values shared by status indicators and the header.</summary>
internal enum UiStatusKind
{
    Ready,
    Playing,
    Paused,
    Loading,
    Error,
    Completed,
    Detached,
    Stopped
}

/// <summary>
/// Central palette for the component UI. Components only choose semantic styles;
/// the palette keeps the visual identity consistent.
/// </summary>
internal sealed class UiTheme
{
    public static UiTheme Default { get; } = new();

    public CellStyle Page { get; } = CellStyle.FromBackground("#0b0f14");
    public CellStyle Surface { get; } = CellStyle.FromBackground("#111820");
    public CellStyle SurfaceRaised { get; } = CellStyle.FromBackground("#151d26");
    public CellStyle SurfaceMuted { get; } = CellStyle.FromBackground("#0e141b");

    public CellStyle Text { get; } = CellStyle.FromColor("#e6edf3");
    public CellStyle Strong { get; } = CellStyle.FromColor("#ffffff").WithBold();
    public CellStyle Muted { get; } = CellStyle.FromColor("#8b98a5");
    public CellStyle Faint { get; } = CellStyle.FromColor("#5b6673");

    public CellStyle Accent { get; } = CellStyle.FromColor("#22d3ee");
    public CellStyle AccentSoft { get; } = CellStyle.FromColor("#67e8f9");
    public CellStyle Secondary { get; } = CellStyle.FromColor("#8b5cf6");
    public CellStyle Boost { get; } = CellStyle.FromColor("#c084fc");
    public CellStyle Success { get; } = CellStyle.FromColor("#34d399");
    public CellStyle Warning { get; } = CellStyle.FromColor("#fbbf24");
    public CellStyle Danger { get; } = CellStyle.FromColor("#f87171");
    public CellStyle Info { get; } = CellStyle.FromColor("#60a5fa");

    public CellStyle Border { get; } = CellStyle.FromColor("#263241");
    public CellStyle BorderAccent { get; } = CellStyle.FromColor("#22d3ee");
    public CellStyle BorderMuted { get; } = CellStyle.FromColor("#1c2632");

    public CellStyle Selection { get; } = CellStyle.FromBackground("#163042");
    public CellStyle CurrentRow { get; } = CellStyle.FromBackground("#0f2b33");

    public CellStyle SplashBackground { get; } = CellStyle.FromBackground("#090d12");
    public CellStyle SplashDim { get; } = CellStyle.FromColor("grey23");
    public CellStyle SplashMuted { get; } = CellStyle.FromColor("grey70");
    public CellStyle SplashFaint { get; } = CellStyle.FromColor("grey35");
    public CellStyle SplashWhite { get; } = CellStyle.FromColor("white");
    public CellStyle SplashPulseDim { get; } = CellStyle.FromColor("grey35");
    public CellStyle SplashPulseBright { get; } = CellStyle.FromColor("#3b82f6");

    public CellStyle ForStatus(UiStatusKind kind) =>
        kind switch
        {
            UiStatusKind.Ready => Success,
            UiStatusKind.Playing => Accent,
            UiStatusKind.Paused => Warning,
            UiStatusKind.Loading => Secondary,
            UiStatusKind.Error => Danger,
            UiStatusKind.Completed => Success,
            UiStatusKind.Detached => Info,
            _ => Muted
        };

    public CellStyle SplashStyle(string name) =>
        name switch
        {
            "grey23" => SplashDim,
            "grey35" => SplashFaint,
            "grey70" => SplashMuted,
            "white" => SplashWhite,
            "#00d4ff" => Accent,
            "#3b82f6" => SplashPulseBright,
            "#c084fc" => Boost,
            _ => CellStyle.FromColor(name)
        };
}
