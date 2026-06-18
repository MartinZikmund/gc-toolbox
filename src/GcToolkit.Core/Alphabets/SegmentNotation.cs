namespace GcToolkit.Core.Alphabets;

/// <summary>
/// How a single character's lit segments are written down. The codec round-trips between all three and
/// can auto-detect which one a piece of input uses.
/// </summary>
public enum SegmentNotation
{
    /// <summary>The set of lit segment labels, e.g. <c>"a b c d e f"</c> for the digit 0.</summary>
    Labels,

    /// <summary>A binary mask, one bit per segment in label order, e.g. <c>"0111111"</c> for the digit 0.</summary>
    Binary,

    /// <summary>The decimal value of the mask, e.g. <c>63</c> for the digit 0 on a 7-segment display.</summary>
    Decimal,
}
