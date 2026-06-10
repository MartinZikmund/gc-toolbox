using CommunityToolkit.Mvvm.Input;
using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One displayable semaphore figure: the figure (arm angles), its caption ("G", "7", "A / 1" or a
/// localized sign name), accessibility text, an arm-position tooltip, and — for chart/palette items —
/// the tap command that appends the figure to the text.
/// </summary>
public sealed class SemaphoreFigureItem(
    SemaphoreFigure figure,
    string caption,
    string automationName,
    string toolTip,
    IRelayCommand? tapCommand = null)
{
    public SemaphoreFigure Figure { get; } = figure;

    public string Caption { get; } = caption;

    public string AutomationName { get; } = automationName;

    public string ToolTip { get; } = toolTip;

    public IRelayCommand? TapCommand { get; } = tapCommand;
}
