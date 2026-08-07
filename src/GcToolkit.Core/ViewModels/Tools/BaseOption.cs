namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One entry of the From/To base dropdowns: the <see cref="Radix"/> and its display text — the
/// traditional name where one exists ("Hexadecimal (16)"), otherwise just "Base 17".
/// </summary>
public sealed record BaseOption(int Radix, string DisplayName)
{
    /// <summary>A ComboBox item with no DisplayMemberPath announces and renders its ToString().</summary>
    public override string ToString() => DisplayName;
}
