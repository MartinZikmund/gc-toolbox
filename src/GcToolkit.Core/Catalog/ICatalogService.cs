namespace GcToolkit.Core.Catalog;

/// <summary>
/// Aggregates all contributed categories and tools, exposes them in a deterministic
/// order, and runs accent-/case-insensitive search over them.
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
}
