namespace GcToolkit.Core.Alphabets;

/// <summary>The Polybius square layout a <see cref="TapCode"/> conversion uses.</summary>
public enum TapCodeGrid
{
    /// <summary>The classic 5×5 square: the 26 letters with <c>K</c> merged into the <c>C</c> cell.</summary>
    FiveByFive,

    /// <summary>A 6×6 square holding the 26 letters followed by the digits <c>0–9</c> (no merge).</summary>
    SixBySix,
}
