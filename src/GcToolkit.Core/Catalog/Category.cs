namespace GcToolkit.Core.Catalog;

/// <summary>
/// A catalog category grouping related tools. Identifiers are stable invariant
/// strings so favorites/recents and ordering survive renames.
/// </summary>
/// <param name="Id">Stable, unique, invariant identifier (e.g. <c>coordinates</c>).</param>
/// <param name="NameKey">Localization key for the display name.</param>
/// <param name="Order">Display order in the catalog (ties broken by <see cref="Id"/>).</param>
/// <param name="IconKey">Optional icon/glyph identifier.</param>
public sealed partial record Category(
    string Id,
    string NameKey,
    int Order,
    string? IconKey = null);
