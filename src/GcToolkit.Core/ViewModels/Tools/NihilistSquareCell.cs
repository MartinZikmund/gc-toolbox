namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One cell of the rendered Polybius square: the <see cref="Symbol"/> and its two-digit
/// <see cref="Coordinate"/>. <see cref="AutomationName"/> spells both out for screen readers
/// (e.g. <c>"D, 23"</c>).
/// </summary>
/// <param name="Symbol">The letter/digit placed in this cell.</param>
/// <param name="Coordinate">The cell's coordinate honoring the active orientation and base.</param>
public readonly record struct NihilistSquareCell(string Symbol, string Coordinate)
{
    /// <summary>Screen-reader label combining symbol and coordinate.</summary>
    public string AutomationName => $"{Symbol}, {Coordinate}";
}
