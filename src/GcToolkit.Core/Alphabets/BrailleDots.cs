namespace GcToolkit.Core.Alphabets;

/// <summary>
/// The six raised-dot flags of a braille cell, ready for a 2×3 dot-grid rendering (filled vs. hollow
/// circles) — unlike the bare Unicode glyph, this makes the <i>empty</i> positions visible too. Dots are
/// numbered the standard way: 1-2-3 down the left column, 4-5-6 down the right.
/// </summary>
public readonly record struct BrailleDots(
    bool Dot1, bool Dot2, bool Dot3, bool Dot4, bool Dot5, bool Dot6)
{
    /// <summary>Extracts the dot flags from a Unicode braille-pattern cell (<c>U+2800</c>–<c>U+28FF</c>);
    /// any non-braille character yields an all-empty cell.</summary>
    public static BrailleDots FromCell(char cell)
    {
        if (cell is < '⠀' or > '⣿')
        {
            return default;
        }

        var bits = cell - '⠀';
        return new BrailleDots(
            (bits & 0x01) != 0, (bits & 0x02) != 0, (bits & 0x04) != 0,
            (bits & 0x08) != 0, (bits & 0x10) != 0, (bits & 0x20) != 0);
    }
}
