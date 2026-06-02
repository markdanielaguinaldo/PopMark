namespace PopMark.Helpers.Terminal;

internal static class TerminalStyles
{
    public const string Accent = "#00d4ff";
    public const string SuccessColor = "#00d4ff";
    public const string Muted = "grey70";
    public const string Reset = "\u001b[0m";
    public const string Bold = "\u001b[1m";
    public const string AnsiAccent = "\u001b[38;2;0;212;255m";
    public const string AnsiSecondary = "\u001b[38;2;139;92;246m";
    public const string AnsiMuted = "\u001b[38;2;150;150;150m";
    public const string AnsiChrome = "\u001b[38;2;82;82;82m";
    public const string AnsiWhite = "\u001b[38;2;245;245;245m";

    public static readonly string[] VisualizerBars = ["▁", "▂", "▃", "▄", "▅", "▆", "▇"];
}
