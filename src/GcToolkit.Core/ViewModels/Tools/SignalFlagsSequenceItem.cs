using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One rendered position of the encoded hoist: a flag sized to the current flag height, or a word
/// gap (<see cref="Flag"/> is <see langword="null"/>) rendered as empty space. Items are immutable —
/// the ViewModel rebuilds the sequence when the input or the flag size changes.
/// </summary>
public sealed class SignalFlagsSequenceItem
{
    private const double GapWidthFactor = 0.5;

    public SignalFlagsSequenceItem(SignalFlagDescriptor? flag, double height, string toolTipText, string automationName)
    {
        Flag = flag;
        Height = height;
        Width = flag is null ? height * GapWidthFactor : flag.AspectRatio * height;
        ToolTipText = toolTipText;
        AutomationName = automationName;
    }

    public SignalFlagDescriptor? Flag { get; }

    public bool IsGap => Flag is null;

    public double Width { get; }

    public double Height { get; }

    public string ToolTipText { get; }

    public string AutomationName { get; }
}
