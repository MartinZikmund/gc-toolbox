namespace GcToolkit.Core.Catalog;

/// <summary>
/// Immutable metadata describing a single catalog entry. Provided by tool
/// contributors via DI so the shell never hardcodes individual tools.
/// </summary>
/// <param name="Id">Stable, unique, invariant identifier (e.g. <c>coordinates.conversion</c>). Primary key for favorites/recents.</param>
/// <param name="NameKey">Localization key resolved via the string localizer.</param>
/// <param name="CategoryId">References <see cref="Category.Id"/>.</param>
/// <param name="Keywords">Search aliases (localized + invariant) searched alongside the name.</param>
/// <param name="IconKey">Optional icon/glyph identifier.</param>
/// <param name="ViewModelType">Navigation target (view-model-first). For SP-1 placeholders this is the stub tool host view model.</param>
/// <param name="IsPlaceholder"><see langword="true"/> for SP-1 sample entries; removed as real tools land.</param>
public sealed partial record ToolDescriptor(
    string Id,
    string NameKey,
    string CategoryId,
    IReadOnlyList<string> Keywords,
    string? IconKey,
    Type ViewModelType,
    bool IsPlaceholder = false);
