namespace GcToolkit.Core.Catalog;

/// <summary>
/// A catalog category grouping related tools. Identifiers are stable invariant strings so
/// favorites/recents and ordering survive renames.
/// </summary>
/// <param name="Id">Stable, unique, invariant identifier (e.g. <c>Coordinates</c>).</param>
/// <param name="NameKey">Localization key for the display name — by convention <c>Category_&lt;Id&gt;</c>.</param>
/// <param name="Order">Display order in the catalog (ties broken by <see cref="Id"/>).</param>
/// <param name="IconKey">Bitmap icon key (= category member name) resolved to <c>ms-appx:///Assets/Icons/Categories/&lt;IconKey&gt;.png</c> (FR-008a, R7).</param>
/// <param name="GroupId">The owning <c>ToolGroup</c> from the category→group map, or <see langword="null"/>.</param>
public sealed partial record Category(
    string Id,
    string NameKey,
    int Order,
    string IconKey = "",
    string? GroupId = null);
