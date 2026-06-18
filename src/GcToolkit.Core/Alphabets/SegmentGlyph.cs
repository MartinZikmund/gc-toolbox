namespace GcToolkit.Core.Alphabets;

/// <summary>
/// One rendered character of a segment-display conversion: the <see cref="Character"/> it represents (or
/// <c>'?'</c> when the mask matched nothing), the lit-segment <see cref="Mask"/>, and the same mask in
/// every notation so the UI and clipboard can show whichever the user picked. <see cref="LitSegments"/>
/// drives the scalable glyph rendering in the head.
/// </summary>
/// <param name="Character">The decoded/source character, or <c>'?'</c> for an unmatched mask.</param>
/// <param name="DisplayType">Which segment family this glyph belongs to.</param>
/// <param name="Mask">The lit-segment bit mask (bit <c>i</c> = the <c>i</c>-th segment label of the type).</param>
/// <param name="Labels">The lit segment labels in canonical order, e.g. <c>["a","b","c","d","e","f"]</c>.</param>
/// <param name="Binary">The mask as a fixed-width binary string (MSB = last label).</param>
/// <param name="Decimal">The mask as its decimal value.</param>
/// <param name="LitSegments">Per-segment lit flags in label order, for shape-by-shape rendering.</param>
/// <param name="IsExact"><see langword="true"/> when the mask is an exact table entry; <see langword="false"/>
/// when <see cref="Character"/> is a closest (Hamming-nearest) match.</param>
/// <param name="Distance">Hamming distance to the matched character (0 when exact).</param>
public sealed record SegmentGlyph(
    char Character,
    SegmentDisplayType DisplayType,
    int Mask,
    IReadOnlyList<string> Labels,
    string Binary,
    int Decimal,
    IReadOnlyList<SegmentState> LitSegments,
    bool IsExact,
    int Distance);

/// <summary>One segment of a glyph: its <see cref="Label"/> (a, b, …) and whether it is <see cref="Lit"/>.</summary>
public sealed record SegmentState(string Label, bool Lit);
