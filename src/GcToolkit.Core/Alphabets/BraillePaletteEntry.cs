namespace GcToolkit.Core.Alphabets;

/// <summary>
/// One clickable cell of the braille reference chart (the geocachingtoolbox.com "alphabet"). Clicking it
/// "types" a character: <see cref="Text"/> is appended when converting text → braille, and the braille
/// <see cref="Cell"/> is appended when converting braille → text, so the chart drives input in either
/// direction.
/// </summary>
/// <param name="Id">Stable identifier (e.g. <c>"a"</c>, <c>"Capital"</c>, <c>"QuoteOpen"</c>).</param>
/// <param name="Label">The literal caption shown under the cell (e.g. <c>"a / 1"</c>, <c>"."</c>). For the
/// three word-captioned indicators this is an English fallback; prefer <see cref="LabelKey"/>.</param>
/// <param name="LabelKey">Localization key for the caption, or <see langword="null"/> when
/// <see cref="Label"/> is a language-neutral literal.</param>
/// <param name="Dots">Dot flags for the 2×3 grid rendering.</param>
/// <param name="Text">Plain-text character to type in the text → braille direction (empty for the
/// capital and number indicators, which have no plain-text form).</param>
/// <param name="Cell">Braille character(s) to type in the braille → text direction.</param>
/// <param name="DotNumbers">Dash-joined raised-dot numbers (e.g. <c>"1-3-4"</c>) for the tooltip.</param>
public sealed partial record BraillePaletteEntry(
    string Id,
    string Label,
    string? LabelKey,
    BrailleDots Dots,
    string Text,
    string Cell,
    string DotNumbers);
