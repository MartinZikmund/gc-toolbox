namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One letter cell in a rendered 5×5 four-square grid. Carries its <see cref="Letter"/>, a screen-reader
/// <see cref="AutomationName"/>, and a <see cref="IsHighlighted"/> flag the view binds to colour the cells
/// involved in the active digraph.
/// </summary>
public sealed partial class FourSquareCellItem : ObservableObject
{
    public FourSquareCellItem(char letter, int row, int column, string automationName)
    {
        Letter = letter.ToString();
        Row = row;
        Column = column;
        AutomationName = automationName;
    }

    public string Letter { get; }

    public int Row { get; }

    public int Column { get; }

    public string AutomationName { get; }

    /// <summary><see langword="true"/> when this cell takes part in the digraph the user is hovering/previewing.</summary>
    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }
}
