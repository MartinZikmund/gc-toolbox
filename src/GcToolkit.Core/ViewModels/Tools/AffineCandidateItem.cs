using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the affine "auto-solve" brute-force list: the key (<see cref="A"/>, <see cref="B"/>) tried,
/// the resulting <see cref="Text"/>, and a self-contained <see cref="CopyCommand"/> so each candidate can
/// be copied without the row reaching back into the parent ViewModel.
/// </summary>
public sealed class AffineCandidateItem
{
    public AffineCandidateItem(int a, int b, string text, Action<string> copy)
    {
        A = a;
        B = b;
        Text = text;
        Key = $"a={a}, b={b}";
        CopyCommand = new RelayCommand(() => copy(text));
    }

    public int A { get; }

    public int B { get; }

    public string Key { get; }

    public string Text { get; }

    public ICommand CopyCommand { get; }

    /// <summary>A ListView item with no explicit automation name announces its ToString(), so make it the row.</summary>
    public override string ToString() => $"{Key}: {Text}";
}
