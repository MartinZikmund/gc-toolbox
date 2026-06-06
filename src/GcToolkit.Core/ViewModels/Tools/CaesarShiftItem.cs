using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the "show all shifts" brute-force list: the <see cref="Shift"/> amount, the resulting
/// <see cref="Text"/>, and a self-contained <see cref="CopyCommand"/> so each candidate can be copied
/// without the row needing to reach back into the parent ViewModel.
/// </summary>
public sealed class CaesarShiftItem
{
    public CaesarShiftItem(int shift, string text, Action<string> copy)
    {
        Shift = shift;
        Text = text;
        CopyCommand = new RelayCommand(() => copy(text));
    }

    public int Shift { get; }

    public string Text { get; }

    public ICommand CopyCommand { get; }
}
