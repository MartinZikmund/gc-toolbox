using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the multi-variant brute force: a variant's display name, the text it produces for the
/// current input, and a self-contained command that copies that text (beyond-parity convenience).
/// </summary>
public sealed class SpellingAlphabetGuess
{
    public SpellingAlphabetGuess(string variantName, string text, Action<string> copy)
    {
        VariantName = variantName;
        Text = text;
        CopyCommand = new RelayCommand(() => copy(text));
    }

    public string VariantName { get; }

    public string Text { get; }

    public ICommand CopyCommand { get; }

    /// <summary>A ListView item with no explicit automation name announces its ToString(), so make it the row.</summary>
    public override string ToString() => $"{VariantName}: {Text}";
}
