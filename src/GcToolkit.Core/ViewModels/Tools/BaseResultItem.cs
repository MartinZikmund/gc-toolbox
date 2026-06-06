using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the base-converter results grid: a human <see cref="Label"/> for the base (e.g.
/// "Binary (base 2)"), the converted <see cref="Value"/>, and a self-contained
/// <see cref="CopyCommand"/> so each line can be copied without reaching back into the parent VM.
/// </summary>
public sealed class BaseResultItem
{
    public BaseResultItem(string label, string value, Action<string> copy)
    {
        Label = label;
        Value = value;
        CopyCommand = new RelayCommand(() => copy(value));
    }

    public string Label { get; }

    public string Value { get; }

    public ICommand CopyCommand { get; }
}
