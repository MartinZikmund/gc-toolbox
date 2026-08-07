using System.Windows.Input;
using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the coordinate-conversion result list: a localized <see cref="Label"/> for the
/// notation, the formatted <see cref="Value"/>, an accessible <see cref="CopyAutomationName"/> for the
/// row's copy button, and a self-contained <see cref="CopyCommand"/> so a row can be copied without
/// reaching back into the parent ViewModel (mirrors <see cref="CaesarShiftItem"/>).
/// </summary>
public sealed class CoordinateFormatRow
{
    public CoordinateFormatRow(CoordinateFormat format, string label, string value, string copyAutomationName, Action<string> copy)
    {
        Format = format;
        Label = label;
        Value = value;
        CopyAutomationName = copyAutomationName;
        CopyCommand = new RelayCommand(() => copy(value));
    }

    public CoordinateFormat Format { get; }

    public string Label { get; }

    public string Value { get; }

    public string CopyAutomationName { get; }

    public ICommand CopyCommand { get; }

    /// <summary>Screen readers announce a list item's <see cref="object.ToString"/> when the row template
    /// carries no automation name — so read out the notation and its value, not the type name.</summary>
    public override string ToString() => $"{Label}: {Value}";
}
