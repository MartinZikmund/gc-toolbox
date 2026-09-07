using GcToolkit.Core.Navigation;
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
    private readonly IReadOnlyList<ToolDescriptor> _allTools;
    private readonly IReadOnlyList<ToolDescriptor> _tools;
    private readonly NavigationTree _navigationTree;
    private readonly ILookup<string, ToolDescriptor> _toolsByCategory;
    private readonly IToolMatcher _matcher;
    private readonly IStringLocalizer _localizer;

    /// <summary>
    /// Ungated overload for hosts and tests that register no availability policies. Kept so the
    /// device-gating parameter could be added without touching every existing call site.
    /// </summary>
    public CatalogService(
        IEnumerable<ICategoryContributor> categoryContributors,
        IEnumerable<IToolContributor> toolContributors,
        IToolMatcher matcher,
        IStringLocalizer localizer)
        : this(categoryContributors, toolContributors, [], matcher, localizer)
    {
    }

    /// <param name="policies">
    /// ANDed device-availability gates. Registering none leaves every discovered tool visible.
    /// </param>
    public CatalogService(
        IEnumerable<ICategoryContributor> categoryContributors,
        IEnumerable<IToolContributor> toolContributors,
        IEnumerable<IToolAvailabilityPolicy> policies,
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

        // Validation deliberately runs over the FULL discovered set, before device filtering: a
        // duplicate id or bad category inside a compass-only tool must still fail on desktop CI.
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

        var policyList = policies.ToList();

        _allTools = tools
            .OrderBy(t => categoryOrder[t.CategoryId])
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();

        _tools = _allTools.Where(t => policyList.All(p => p.IsAvailable(t))).ToList();
        _toolsByCategory = _tools.ToLookup(t => t.CategoryId, StringComparer.Ordinal);
        _navigationTree = NavigationTreeBuilder.Build(_categories, _tools);
    }

    public IReadOnlyList<Category> GetCategories() => _categories;

    public IReadOnlyList<ToolDescriptor> GetTools() => _tools;

    public IReadOnlyList<ToolDescriptor> GetAllTools() => _allTools;

    public ToolDescriptor? FindTool(string toolId)
        => _allTools.FirstOrDefault(t => string.Equals(t.Id, toolId, StringComparison.Ordinal));

    public NavigationTree GetNavigationTree() => _navigationTree;

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
