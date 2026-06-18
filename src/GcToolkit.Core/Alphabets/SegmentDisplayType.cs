namespace GcToolkit.Core.Alphabets;

/// <summary>
/// The four electronic segment-display families this tool understands. Each adds segments to render a
/// wider character set: the 7-segment shows digits and a handful of letters, the 9- adds two diagonals,
/// and the 14-/16-segment "starburst" displays render the full alphabet.
/// </summary>
public enum SegmentDisplayType
{
    /// <summary>Classic calculator/LCD digit: segments a–g (a top, b–c right, d bottom, e–f left, g middle).</summary>
    SevenSegment,

    /// <summary>7-segment plus the two upper diagonals (h ╲, i ╱) for a few more letters.</summary>
    NineSegment,

    /// <summary>Starburst: the 7 outer bars, a split middle (g1/g2), the centre verticals (i/l) and the
    /// four diagonals (h ╲, j ╱, k ╱, m ╲) — renders the full alphabet.</summary>
    FourteenSegment,

    /// <summary>14-segment with the top and bottom bars each split in two (a1/a2, d1/d2) for symmetric glyphs.</summary>
    SixteenSegment,
}
