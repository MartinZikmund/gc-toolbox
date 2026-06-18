using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One cell of the rendered, tappable Polybius square: its <see cref="Letter"/>, the
/// <see cref="Coordinate"/> label pair, a <see cref="TapCommand"/> that appends the letter to the
/// plaintext, and an observable <see cref="IsHighlighted"/> flag the View binds to so a cell lights up
/// while its letter is the one under the cursor.
/// </summary>
public sealed partial class PolybiusCellItem : ObservableObject
{
    public PolybiusCellItem(string letter, string coordinate, Action<string> onTap)
    {
        Letter = letter;
        Coordinate = coordinate;
        TapCommand = new RelayCommand(() => onTap(PrimaryLetter));
    }

    /// <summary>The letter(s) shown in the cell (e.g. <c>"A"</c> or the merged <c>"I/J"</c>).</summary>
    public string Letter { get; }

    /// <summary>The rendered coordinate label pair (e.g. <c>"23"</c> or <c>"DF"</c>).</summary>
    public string Coordinate { get; }

    /// <summary>Appends this cell's primary letter to the input when the cell is tapped.</summary>
    public ICommand TapCommand { get; }

    /// <summary><see langword="true"/> while this cell holds the letter currently being hovered/selected.</summary>
    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }

    /// <summary>The single letter a tap inserts — the part before the slash for a merged cell.</summary>
    private string PrimaryLetter => Letter.Contains('/') ? Letter[..Letter.IndexOf('/')] : Letter;
}
