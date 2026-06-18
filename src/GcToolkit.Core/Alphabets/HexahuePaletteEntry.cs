namespace GcToolkit.Core.Alphabets;

/// <summary>
/// One clickable cell of the Hexahue reference chart: the plain <see cref="Character"/> it represents
/// and the <see cref="Glyph"/> that draws it. Clicking it "types" the character into the input. The
/// space entry carries the literal space character; the View can give it a friendlier caption.
/// </summary>
public sealed record HexahuePaletteEntry(char Character, HexahueGlyph Glyph)
{
    /// <summary>The chart group this entry belongs to, so the View can section the chart.</summary>
    public HexahueGroup Group => Character switch
    {
        >= 'A' and <= 'Z' => HexahueGroup.Letters,
        >= '0' and <= '9' => HexahueGroup.Digits,
        ' ' => HexahueGroup.Space,
        _ => HexahueGroup.Punctuation,
    };
}

/// <summary>The sections of the Hexahue reference chart.</summary>
public enum HexahueGroup
{
    Letters,
    Digits,
    Punctuation,
    Space,
}
