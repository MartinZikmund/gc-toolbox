using GcToolkit.Core.Search;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Catalog;

/// <summary>
/// Aggregates contributed categories and tools into a validated, deterministically
/// ordered catalog and runs search over it. Validation failures (duplicate ids, a tool
/// pointing at an unknown category) are startup errors surfaced from the constructor.
/// </summary>
public sealed class CatalogService : ICatalogService
{
    private readonly IReadOnlyList<Category> _categories;
    private readonly IReadOnlyList<ToolDescriptor> _tools;
    private readonly ILookup<string, ToolDescriptor> _toolsByCategory;
    private readonly IToolMatcher _matcher;
    private readonly IStringLocalizer _localizer;

    public CatalogService(
        IEnumerable<ICategoryContributor> categoryContributors,
        IEnumerable<IToolContributor> toolContributors,
        IToolMatcher matcher,
        IStringLocalizer localizer)
    {
        _matcher = matcher;
        _localizer = localizer;

        _categories = categoryContributors
            .SelectMany(c => c.GetCategories())
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        var duplicateCategory = _categories
            .GroupBy(c => c.Id, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateCategory is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate category id '{duplicateCategory.Key}' registered.");
        }

        var categoryOrder = _categories
            .Select((c, index) => (c.Id, index))
            .ToDictionary(x => x.Id, x => x.index, StringComparer.Ordinal);

        var tools = toolContributors.SelectMany(c => c.GetTools()).ToList();

        var duplicateTool = tools
            .GroupBy(t => t.Id, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateTool is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate tool id '{duplicateTool.Key}' registered.");
        }

        var unknownCategory = tools.FirstOrDefault(t => !categoryOrder.ContainsKey(t.CategoryId));
        if (unknownCategory is not null)
        {
            throw new InvalidOperationException(
                $"Tool '{unknownCategory.Id}' references unknown category '{unknownCategory.CategoryId}'.");
        }

        _tools = tools
            .OrderBy(t => categoryOrder[t.CategoryId])
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();

        _toolsByCategory = _tools.ToLookup(t => t.CategoryId, StringComparer.Ordinal);
    }

    public IReadOnlyList<Category> GetCategories() => _categories;

    public IReadOnlyList<ToolDescriptor> GetTools() => _tools;

    public IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId)
        => _toolsByCategory[categoryId].ToList();

    public IReadOnlyList<ToolDescriptor> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _tools;
        }

        return _tools
            .Where(t => _matcher.Matches(query, GetSearchableValues(t)))
            .ToList();
    }

    private IEnumerable<string> GetSearchableValues(ToolDescriptor tool)
    {
        yield return _localizer[tool.NameKey].Value;
        foreach (var keyword in tool.Keywords)
        {
            yield return keyword;
        }
    }
}
