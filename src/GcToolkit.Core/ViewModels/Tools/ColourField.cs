using CommunityToolkit.Mvvm.ComponentModel;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One editable channel of a colour model card (e.g. "R", "Hue", "Cb"): a fixed
/// <see cref="Label"/>, an optional <see cref="Unit"/> suffix (<c>%</c>/<c>°</c>), and a two-way
/// bound <see cref="Value"/> string. When the user edits <see cref="Value"/> the field raises
/// <see cref="ValueEdited"/> so the owning ViewModel can re-derive the canonical colour. A
/// suppression flag lets the owner write derived values back without re-triggering the callback.
/// </summary>
public sealed partial class ColourField : ObservableObject
{
    private bool _suppress;

    public ColourField(string label, string unit = "")
    {
        Label = label;
        Unit = unit;
    }

    /// <summary>Raised when the user changes <see cref="Value"/> (not on programmatic writes).</summary>
    public event Action? ValueEdited;

    public string Label { get; }

    public string Unit { get; }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    /// <summary>Writes <paramref name="value"/> without raising <see cref="ValueEdited"/>.</summary>
    public void SetSilently(string value)
    {
        _suppress = true;
        try
        {
            Value = value;
        }
        finally
        {
            _suppress = false;
        }
    }

    partial void OnValueChanged(string value)
    {
        if (!_suppress)
        {
            ValueEdited?.Invoke();
        }
    }
}
