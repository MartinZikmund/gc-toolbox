namespace GcToolkit.Core.Alphabets;

/// <summary>
/// One Hexahue character: a 2×3 grid of six coloured squares, read left-to-right then top-to-bottom.
/// Positions are <see cref="TopLeft"/>/<see cref="TopRight"/> (top row), <see cref="MiddleLeft"/>/
/// <see cref="MiddleRight"/> (middle row), <see cref="BottomLeft"/>/<see cref="BottomRight"/> (bottom row).
/// </summary>
public sealed record HexahueGlyph(
    HexahueColor TopLeft,
    HexahueColor TopRight,
    HexahueColor MiddleLeft,
    HexahueColor MiddleRight,
    HexahueColor BottomLeft,
    HexahueColor BottomRight)
{
    /// <summary>The six squares in reading order — handy for rendering and equality on the layout.</summary>
    public IReadOnlyList<HexahueColor> Cells => [TopLeft, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomRight];
}
