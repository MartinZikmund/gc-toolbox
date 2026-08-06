using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One decoded chunk of a vanity code: the digit run and the dictionary words it can spell
/// (<c>34448 → DIGIT, EIGHT, FIGHT</c>). When no word matches, <see cref="Options"/> falls back to the
/// letters printed on each key so the chunk is still solvable by hand. Carries its own
/// <see cref="CopyCommand"/> so a row never reaches back into the parent ViewModel.
/// </summary>
public sealed class VanityCandidateItem
{
    public VanityCandidateItem(string code, IReadOnlyList<string> words, string letterOptions, Action<string> copy)
    {
        Code = code;
        Words = words;
        HasWords = words.Count > 0;
        Options = HasWords ? string.Join(", ", words) : letterOptions;
        CopyCommand = new RelayCommand(() => copy(Options));
    }

    /// <summary>The digit run this row decodes (e.g. <c>34448</c>).</summary>
    public string Code { get; }

    /// <summary>The matching dictionary words, commonest first; empty when none matched.</summary>
    public IReadOnlyList<string> Words { get; }

    public bool HasWords { get; }

    /// <summary>What the code can spell: the matching words, or the per-key letters when none matched.</summary>
    public string Options { get; }

    public ICommand CopyCommand { get; }

    /// <summary>A ListView item with no explicit automation name announces its ToString(), so make it the row.</summary>
    public override string ToString() => $"{Code}: {Options}";
}
