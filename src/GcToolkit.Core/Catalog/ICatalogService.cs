using GcToolkit.Core.Navigation;

namespace GcToolkit.Core.Catalog;

/// <summary>
/// Aggregates all contributed categories and tools, exposes them in a deterministic
/// order, and runs accent-/case-insensitive search over them. Every member except
/// <see cref="GetAllTools"/> and <see cref="FindTool"/> reflects what this device can actually run
/// (see <see cref="IToolAvailabilityPolicy"/>).
/// </summary>
public interface ICatalogService
{
    IReadOnlyList<Category> GetCategories();

    IReadOnlyList<ToolDescriptor> GetTools();

    IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId);

    /// <summary>
    /// Accent- and case-insensitive match over each tool's localized name and keywords
    /// (FR-003). An empty or whitespace query returns the full catalog.
    /// </summary>
    IReadOnlyList<ToolDescriptor> Search(string query);

    /// <summary>Every discovered tool, INCLUDING ones this device cannot run. For id-to-descriptor
    /// resolution (restored selections, future deep links) which must still work while a tool is hidden.</summary>
    IReadOnlyList<ToolDescriptor> GetAllTools();

    /// <summary>Resolves a tool by id over the UNFILTERED set. Returns null only for an id no build contains.</summary>
    ToolDescriptor? FindTool(string toolId);

    /// <summary>The navigation pane's tree, built by <c>NavigationTreeBuilder</c> from the FILTERED tool
    /// list, so a hidden tool never reaches the pane and a category left empty by hiding disappears (FR-024).</summary>
    NavigationTree GetNavigationTree();
}
