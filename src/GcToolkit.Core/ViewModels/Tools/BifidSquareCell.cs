namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One cell of the rendered 5×5 Bifid key square: the letter plus its 1-based row/column for
/// the screen-reader label.</summary>
public sealed record BifidSquareCell(char Letter, int Row, int Column)
{
    /// <summary>The cell's letter as a single-character string for direct text binding.</summary>
    public string Glyph => Letter.ToString();

    /// <summary>"K, row 1, column 1" — the accessible name announced for the cell.</summary>
    public string AutomationName => $"{Letter}, row {Row}, column {Column}";
}
