using PopMark.Models;

namespace PopMark.Helpers.Terminal;

internal sealed record RenderContext(
    int Width,
    int Height,
    PlayerSnapshot Snapshot,
    string Notice,
    string Input,
    bool ShowHelp,
    bool ShowControls,
    bool MiniMode,
    int AnimationFrame,
    int QueueScrollOffset);
