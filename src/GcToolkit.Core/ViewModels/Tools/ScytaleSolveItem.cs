using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the Scytale auto-solve list: the <see cref="Columns"/> tried, the resulting decoded
/// <see cref="Text"/>, and a self-contained <see cref="CopyCommand"/> so each candidate can be copied
/// without the row reaching back into the parent ViewModel.
/// </summary>
public sealed class ScytaleSolveItem
{
    public ScytaleSolveItem(int columns, string text, Action<string> copy)
    {
        Columns = columns;
        Text = text;
        CopyCommand = new RelayCommand(() => copy(text));
    }

    public int Columns { get; }

    public string Text { get; }

    public ICommand CopyCommand { get; }

    /// <summary>A ListView item with no explicit automation name announces its ToString(), so make it the row.</summary>
    public override string ToString() => $"{Columns}: {Text}";
}
