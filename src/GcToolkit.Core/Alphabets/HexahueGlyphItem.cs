namespace GcToolkit.Core.Alphabets;

/// <summary>
/// A rendered glyph in the output sequence: the <see cref="Glyph"/> to draw and the plain
/// <see cref="Character"/> it stands for, with an <see cref="AutomationName"/> for screen readers.
/// </summary>
public sealed record HexahueGlyphItem(HexahueGlyph Glyph, char Character)
{
    /// <summary>Screen-reader label: the decoded character (a friendly word for the space block).</summary>
    public string AutomationName => Character == ' ' ? "Space" : Character.ToString();
}
