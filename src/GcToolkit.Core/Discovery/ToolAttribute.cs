namespace GcToolkit.Core.Discovery;

/// <summary>
/// Marks a tool's ViewModel (a class deriving <c>ToolViewModelBase</c>) for compile-time
/// discovery by <c>GcToolkit.SourceGenerators</c>. The generator scans the referenced
/// <c>GcToolkit.Core</c> assembly's metadata for this attribute and emits the catalog
/// contributor, navigation tree, and view registrations — so adding a tool needs no edit
/// to any shared/central file (FR-001, SC-001).
/// </summary>
/// <remarks>
/// The icon is resolved by convention from <see cref="Id"/> (research R7) and the group is a
/// property of the category, not the tool (research R6); neither appears on this attribute.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ToolAttribute : Attribute
{
    public ToolAttribute(string id, ToolCategory category)
    {
        Id = id;
        Category = category;
    }

    /// <summary>Unique, resource-key-safe PascalCase id (e.g. <c>CoordinateConversion</c>). Doubles as the
    /// localization base key (<c>&lt;Id&gt;_Name</c>/<c>&lt;Id&gt;_Tooltip</c>) and the favorites/recents key (R5).</summary>
    public string Id { get; }

    /// <summary>The single category that directly contains the tool (FR-007).</summary>
    public ToolCategory Category { get; }

    /// <summary>ISO <c>yyyy-MM-dd</c> date the tool was introduced. Missing/invalid format is a build error (GCTOOL001/GCTOOL003).</summary>
    public string Introduced { get; init; } = "";

    /// <summary>ISO <c>yyyy-MM-dd</c> date the tool was last updated (FR-011).</summary>
    public string Updated { get; init; } = "";

    /// <summary>Optional search aliases, merged with the localized name (reuses <c>ToolDescriptor.Keywords</c>).</summary>
    public string[] Keywords { get; init; } = [];
}
