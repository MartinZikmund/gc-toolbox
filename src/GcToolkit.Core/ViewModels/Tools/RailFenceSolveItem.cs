using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the Rail Fence auto-solve list: the candidate <see cref="Rails"/> and <see cref="Offset"/>
/// that produced <see cref="Text"/>, plus a self-contained <see cref="CopyCommand"/> so a candidate can be
/// copied without the row reaching back into the parent ViewModel.
/// </summary>
public sealed class RailFenceSolveItem
{
    public RailFenceSolveItem(int rails, int offset, string text, Action<string> copy)
    {
        Rails = rails;
        Offset = offset;
        Text = text;
        CopyCommand = new RelayCommand(() => copy(text));
    }

    public int Rails { get; }

    public int Offset { get; }

    public string Text { get; }

    /// <summary>A compact "rails / offset" label for the row, e.g. <c>"3 / 0"</c>.</summary>
    public string Label => $"{Rails} / {Offset}";

    public ICommand CopyCommand { get; }
}
