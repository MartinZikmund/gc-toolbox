using System.Windows.Input;
using GcToolkit.Core.Text;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the per-word results list: the scored <see cref="Word"/>, its <see cref="Total"/>,
/// the per-letter <see cref="Breakdown"/> string, the geocaching <see cref="DigitalRoot"/>, and a
/// self-contained <see cref="CopyCommand"/> so a row can be copied without reaching into the parent.
/// </summary>
public sealed class ScrabbleWordItem
{
    public ScrabbleWordItem(WordScore score, Action<string> copy)
    {
        Word = score.Text;
        Total = score.Total;
        Breakdown = score.Breakdown;
        DigitalRoot = score.Reductions.DigitalRoot;
        HasUnknown = score.HasUnknown;
        CopyCommand = new RelayCommand(() => copy($"{Word} = {Total}"));
    }

    public string Word { get; }

    public int Total { get; }

    public string Breakdown { get; }

    public int DigitalRoot { get; }

    public bool HasUnknown { get; }

    public ICommand CopyCommand { get; }
}
