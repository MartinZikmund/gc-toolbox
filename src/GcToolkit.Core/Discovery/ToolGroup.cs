namespace GcToolkit.Core.Discovery;

/// <summary>
/// The closed set of optional "uber-categories" that aggregate categories and render as a
/// navigation-pane section header (text, no icon). Convention-only metadata (research R6):
/// <c>NameKey</c> = <c>Group_&lt;Member&gt;</c>, <c>Order</c> = declaration index.
/// </summary>
public enum ToolGroup
{
    Conversion,
}

/// <summary>
/// The one relationship a member name cannot express: which <see cref="ToolGroup"/> a
/// <see cref="ToolCategory"/> belongs to. Kept as a small explicit map beside the enums — the
/// deliberate, minimal exception to "convention only" (research R6). A category absent from the
/// map has no group and renders without a heading.
/// </summary>
public static class ToolGrouping
{
    public static IReadOnlyDictionary<ToolCategory, ToolGroup> CategoryGroups { get; } =
        new Dictionary<ToolCategory, ToolGroup>
        {
            // Flat navigation: every category is its own top-level section with no super-group
            // heading — matching how geocachingtoolbox.com and CacheSleuth present their tools.
            // Add an entry here to put a category back under a ToolGroup heading.
        };
}
