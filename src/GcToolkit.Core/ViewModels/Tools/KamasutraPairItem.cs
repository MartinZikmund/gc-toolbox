namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One column of the visual Kamasutra pairing grid — the <see cref="Top"/> letter over its
/// <see cref="Bottom"/> partner — for the two-row key display in the View.
/// </summary>
public sealed record KamasutraPairItem(string Top, string Bottom);
