using System.Windows.Input;
using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One flag in the reference chart. Letter and numeral flags carry a self-contained
/// <see cref="AppendCommand"/> that appends their character to the input text; the answering
/// pennant and substitutes are display-only (<see cref="AppendCommand"/> is <see langword="null"/>),
/// matching the geocachingtoolbox.com reference chart.
/// </summary>
public sealed class SignalFlagsPaletteItem
{
    public SignalFlagsPaletteItem(
        SignalFlagDescriptor flag,
        string caption,
        string subcaption,
        string toolTipText,
        string automationName,
        Action<SignalFlagDescriptor>? append)
    {
        Flag = flag;
        Caption = caption;
        Subcaption = subcaption;
        ToolTipText = toolTipText;
        AutomationName = automationName;
        AppendCommand = append is null ? null : new RelayCommand(() => append(flag));
    }

    public SignalFlagDescriptor Flag { get; }

    /// <summary>The big caption under the flag — the character, or the localized special name.</summary>
    public string Caption { get; }

    /// <summary>The small caption — the ICS phonetic name; empty for specials.</summary>
    public string Subcaption { get; }

    public string ToolTipText { get; }

    public string AutomationName { get; }

    public ICommand? AppendCommand { get; }
}
