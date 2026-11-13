using PopMark.Helpers.Terminal.Rendering;

namespace PopMark.Helpers.Terminal.Rendering;

internal readonly record struct TextSpan(string Text, CellStyle? Style = null);
