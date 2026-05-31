namespace GcToolkit.Core.Catalog;

/// <summary>
/// Immutable metadata describing a single catalog entry. Produced by the generated tool
/// contributor (and any manual <see cref="IToolContributor"/>) and consumed by the shell so
/// individual tools are never hardcoded.
/// </summary>
/// <param name="Id">Stable, unique, resource-key-safe PascalCase identifier (e.g. <c>CoordinateConversion</c>). Primary key for favorites/recents (R5).</param>
/// <param name="NameKey">Localization key for the display name — by convention <c>&lt;Id&gt;_Name</c>.</param>
/// <param name="CategoryId">References <see cref="Category.Id"/>.</param>
/// <param name="Keywords">Search aliases (localized + invariant) searched alongside the name.</param>
/// <param name="IconKey">Bitmap icon key (= <see cref="Id"/>) resolved by the app head to <c>ms-appx:///Assets/Icons/Tools/&lt;IconKey&gt;.png</c> (R7).</param>
/// <param name="ViewModelType">Navigation target (view-model-first); a <c>ToolViewModelBase</c>-derived type (R10, FR-009).</param>
/// <param name="IsPlaceholder"><see langword="true"/> for stub tools without real functionality yet (R3).</param>
/// <param name="TooltipKey">Localization key for the tooltip — by convention <c>&lt;Id&gt;_Tooltip</c> (FR-005).</param>
/// <param name="GroupId">The <c>ToolGroup</c> of the tool's category, or <see langword="null"/>.</param>
/// <param name="IntroducedDate">Date the tool was introduced, parsed from the attribute (FR-011).</param>
/// <param name="UpdatedDate">Date the tool was last updated, parsed from the attribute.</param>
public sealed partial record ToolDescriptor(
    string Id,
    string NameKey,
    string CategoryId,
    IReadOnlyList<string> Keywords,
    string IconKey,
    Type ViewModelType,
    bool IsPlaceholder = false,
    string? TooltipKey = null,
    string? GroupId = null,
    DateOnly IntroducedDate = default,
    DateOnly UpdatedDate = default);
