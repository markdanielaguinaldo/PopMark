using PopMark.Helpers.Terminal.Rendering;

namespace PopMark.Helpers.Terminal.Rendering;

/// <summary>A portion of text with an optional style override.</summary>
internal readonly record struct TextSpan(string Text, CellStyle? Style = null);
