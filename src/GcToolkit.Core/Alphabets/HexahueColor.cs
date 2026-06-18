namespace GcToolkit.Core.Alphabets;

/// <summary>
/// The nine distinguishable colours of the Hexahue alphabet. The six hues (<see cref="Red"/>,
/// <see cref="Green"/>, <see cref="Blue"/>, <see cref="Yellow"/>, <see cref="Cyan"/>,
/// <see cref="Magenta"/>) build the 26 letters (each hue used exactly once per glyph); the three greys
/// (<see cref="White"/>, <see cref="Grey"/>, <see cref="Black"/>) build the digits and punctuation.
/// </summary>
public enum HexahueColor
{
    Red,
    Green,
    Blue,
    Yellow,
    Cyan,

    /// <summary>The sixth hue. Drawn as magenta by default; the View can render it purple for the
    /// purple-convention variant (the colour token is the same either way).</summary>
    Magenta,

    White,
    Grey,
    Black,
}
