using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One selectable colour in a band's dropdown: the <see cref="Color"/>, its localized
/// <see cref="Name"/>, and a <c>#RRGGBB</c> <see cref="SwatchHex"/> the View paints as a chip.</summary>
public sealed class ResistorColorOption(ResistorColor color, string name)
{
    public ResistorColor Color { get; } = color;

    public string Name { get; } = name;

    public string SwatchHex { get; } = ResistorCode.SwatchHex(color);

    /// <summary>Screen readers fall back to <c>ToString()</c> for list/combo items, so return the colour name.</summary>
    public override string ToString() => Name;
}

/// <summary>
/// A single band of the resistor in the decode view: the localized role <see cref="RoleLabel"/>
/// (digit / multiplier / tolerance / temp. coefficient), the list of <see cref="Options"/> valid in
/// that position, and the currently <see cref="SelectedOption"/>. Changing the selection raises a
/// callback so the parent ViewModel can re-decode live.
/// </summary>
public sealed partial class ResistorBandSlot : ObservableObject
{
    private readonly Action _onChanged;
    private readonly bool _ready;

    public ResistorBandSlot(string roleLabel, IReadOnlyList<ResistorColorOption> options, int selectedIndex, Action onChanged)
    {
        RoleLabel = roleLabel;
        Options = options;
        _onChanged = onChanged;
        SelectedOption = options[Math.Clamp(selectedIndex, 0, options.Count - 1)];
        _ready = true;
    }

    public string RoleLabel { get; }

    public IReadOnlyList<ResistorColorOption> Options { get; }

    [ObservableProperty]
    public partial ResistorColorOption SelectedOption { get; set; }

    /// <summary>The colour the user currently has selected for this band.</summary>
    public ResistorColor SelectedColor => SelectedOption.Color;

    partial void OnSelectedOptionChanged(ResistorColorOption value)
    {
        if (_ready)
        {
            _onChanged();
        }
    }

    public override string ToString() => $"{RoleLabel}: {SelectedOption.Name}";
}
