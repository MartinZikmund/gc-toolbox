namespace GcToolkit.Core.Alphabets;

/// <summary>
/// A single braille cell of a translation result, carrying the source <see cref="Cell"/> character, its
/// <see cref="Dots"/> for the dot-grid rendering, and a human-readable <see cref="DotNumbers"/> string
/// (e.g. <c>"1-3-4"</c>) for tooltips.
/// </summary>
public sealed record BrailleGlyph(char Cell, BrailleDots Dots, string DotNumbers);
