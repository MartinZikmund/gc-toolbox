namespace GcToolkit.Core.Navigation;

/// <summary>
/// UI-agnostic group → category → tool model that <c>GcToolkit.SourceGenerators</c> emits (as a
/// <c>GeneratedToolCatalog.NavigationTree</c> instance in the app head) and <c>WindowShell</c>
/// renders into its <c>NavigationView</c> (FR-013). Pure data, no XAML — mirroring how WinUI
/// Gallery emits data the app renders.
/// </summary>
/// <remarks>
/// Generator-baked rules: deterministic order (group <c>Order</c> → category <c>Order</c> → tool
/// <c>Id</c> ordinal, consistent with <c>CatalogService</c>); empty categories/groups are omitted
/// (FR-024); a group-less category appears as a top-level node with no heading.
/// </remarks>
public sealed record NavigationTree(IReadOnlyList<NavGroupNode> Roots);

/// <summary>A group heading (or, when <see cref="GroupId"/> is <see langword="null"/>, a synthetic
/// container for group-less categories rendered without a heading).</summary>
public sealed record NavGroupNode(
    string? GroupId,
    string? NameKey,
    int Order,
    IReadOnlyList<NavCategoryNode> Categories);

/// <summary>An expandable-and-clickable category node holding its tools.</summary>
public sealed record NavCategoryNode(
    string CategoryId,
    string NameKey,
    string IconKey,
    int Order,
    IReadOnlyList<NavToolNode> Tools);

/// <summary>A selectable tool leaf that opens its ViewModel view-model-first.</summary>
public sealed record NavToolNode(
    string ToolId,
    string NameKey,
    string TooltipKey,
    string IconKey,
    Type ViewModelType);
