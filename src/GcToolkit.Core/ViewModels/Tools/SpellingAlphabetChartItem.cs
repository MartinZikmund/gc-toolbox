using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the code-word chart for the selected variant: the letter and its code word, plus a
/// self-contained command that appends the letter to the encode input (so the chart doubles as a
/// tappable keyboard).
/// </summary>
public sealed class SpellingAlphabetChartItem
{
    public SpellingAlphabetChartItem(char letter, string word, Action<char> append)
    {
        Letter = letter.ToString();
        Word = word;
        AppendCommand = new RelayCommand(() => append(letter));
    }

    public string Letter { get; }

    public string Word { get; }

    /// <summary>"A — Alfa", used for the tooltip and accessible name.</summary>
    public string Caption => $"{Letter} — {Word}";

    public ICommand AppendCommand { get; }
}
