namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One cell of a rendered Trifid square: its <see cref="Symbol"/> plus the 1-based
/// <see cref="Layer"/>/<see cref="Row"/>/<see cref="Column"/> coordinates used to build a
/// screen-reader label (e.g. "Square 1, row 2, column 3: E").
/// </summary>
public sealed record TrifidCubeCell(int Layer, int Row, int Column, char Symbol)
{
    /// <summary>The symbol as a one-character string for direct text binding.</summary>
    public string Display => Symbol.ToString();

    /// <summary>Accessible coordinate description, e.g. <c>"1,2,3"</c>, for an automation name suffix.</summary>
    public string Coordinate => $"{Layer},{Row},{Column}";
}
