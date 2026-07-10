namespace GcToolkit.Core.Alphabets;

/// <summary>
/// One cell of the displayed Polybius square: its 1-based <paramref name="Row"/>/<paramref name="Column"/>
/// coordinates, its tap <paramref name="Numbers"/> (e.g. <c>"34"</c>), the dot pattern that encodes those
/// taps, and the human <paramref name="Label"/> it carries (a single letter, the merged <c>"C/K"</c>, or a
/// digit).
/// </summary>
public sealed record TapCodeCell(int Row, int Column, string Label, string Numbers, string Dots);
