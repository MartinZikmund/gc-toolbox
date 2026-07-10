using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the "all units" table: the localized unit <see cref="Name"/>, the formatted
/// <see cref="Value"/> in that unit, and a self-contained <see cref="CopyCommand"/> so each row can be
/// copied without reaching back into the parent ViewModel (mirrors <c>CaesarShiftItem</c>).
/// </summary>
public sealed class AngleUnitResult
{
    public AngleUnitResult(string name, string value, Action<string> copy)
    {
        Name = name;
        Value = value;
        CopyCommand = new RelayCommand(() => copy(value));
    }

    public string Name { get; }

    public string Value { get; }

    public ICommand CopyCommand { get; }
}
