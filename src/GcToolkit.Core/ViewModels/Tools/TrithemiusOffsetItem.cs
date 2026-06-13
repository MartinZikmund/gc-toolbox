using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the "show all offsets" brute-force list: the <see cref="StartOffset"/> tried, the
/// resulting decoded <see cref="Text"/>, and a self-contained <see cref="CopyCommand"/> so each
/// candidate can be copied without the row reaching back into the parent ViewModel.
/// </summary>
public sealed class TrithemiusOffsetItem
{
    public TrithemiusOffsetItem(int startOffset, string text, Action<string> copy)
    {
        StartOffset = startOffset;
        Text = text;
        CopyCommand = new RelayCommand(() => copy(text));
    }

    public int StartOffset { get; }

    public string Text { get; }

    public ICommand CopyCommand { get; }
}
