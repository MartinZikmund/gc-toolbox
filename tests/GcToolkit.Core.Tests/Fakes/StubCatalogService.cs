using GcToolkit.Core.Catalog;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>
/// Minimal <see cref="ICatalogService"/> exposing a fixed set of tool ids. Used by favorites/
/// recents tests to verify pruning of ids that are no longer in the catalog.
/// </summary>
public sealed class StubCatalogService(params string[] toolIds) : ICatalogService
{
    private readonly IReadOnlyList<ToolDescriptor> _tools =
        [.. toolIds.Select(id => new ToolDescriptor(id, $"Name_{id}", "cat", [], "", typeof(object)))];

    public IReadOnlyList<Category> GetCategories() => [];

    public IReadOnlyList<ToolDescriptor> GetTools() => _tools;

    public IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId)
        => [.. _tools.Where(t => t.CategoryId == categoryId)];

    public IReadOnlyList<ToolDescriptor> Search(string query) => _tools;
}
