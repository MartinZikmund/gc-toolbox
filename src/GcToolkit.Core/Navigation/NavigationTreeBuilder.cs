using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;

namespace GcToolkit.Core.Navigation;

/// <summary>
/// Shapes the flat discovered catalog (categories + tools) into the hierarchical
/// <see cref="NavigationTree"/> the pane renders. Group membership comes from the single source
/// of truth <see cref="ToolGrouping.CategoryGroups"/> (research R6); ordering is deterministic and
/// empty categories/groups are omitted (FR-023/FR-024). Hand-written and unit-tested so the
/// generator only emits data, not tree logic.
/// </summary>
public static class NavigationTreeBuilder
{
    public static NavigationTree Build(IReadOnlyList<Category> categories, IReadOnlyList<ToolDescriptor> tools)
    {
        var toolsByCategory = tools
            .GroupBy(t => t.CategoryId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(t => t.Id, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        // Non-empty category nodes, in category order (empty categories omitted — FR-024).
        var categoryNodes = categories
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .Where(c => toolsByCategory.TryGetValue(c.Id, out var t) && t.Count > 0)
            .Select(c => new NavCategoryNode(
                c.Id,
                c.NameKey,
                c.IconKey,
                c.Order,
                toolsByCategory[c.Id]
                    .Select(t => new NavToolNode(t.Id, t.NameKey, t.TooltipKey ?? string.Empty, t.IconKey, t.ViewModelType))
                    .ToList()))
            .ToList();

        var roots = new List<(int sortKey, NavGroupNode node)>();
        var grouped = new Dictionary<ToolGroup, List<NavCategoryNode>>();

        foreach (var categoryNode in categoryNodes)
        {
            if (TryGetGroup(categoryNode.CategoryId, out var group))
            {
                if (!grouped.TryGetValue(group, out var list))
                {
                    list = new List<NavCategoryNode>();
                    grouped[group] = list;
                }

                list.Add(categoryNode);
            }
            else
            {
                // A group-less category is a top-level node with no heading (US3 scenario 3).
                roots.Add((categoryNode.Order, new NavGroupNode(null, null, categoryNode.Order, new[] { categoryNode })));
            }
        }

        foreach (var (group, cats) in grouped)
        {
            var orderedCats = cats
                .OrderBy(c => c.Order)
                .ThenBy(c => c.CategoryId, StringComparer.Ordinal)
                .ToList();

            // The group sorts at the position of its earliest category, so the layout follows the
            // category declaration order (a group "pulls in" its later categories).
            var sortKey = orderedCats.Min(c => c.Order);
            roots.Add((sortKey, new NavGroupNode(group.ToString(), "Group_" + group, (int)group, orderedCats)));
        }

        var ordered = roots
            .OrderBy(r => r.sortKey)
            .ThenBy(r => r.node.Order)
            .Select(r => r.node)
            .ToList();

        return new NavigationTree(ordered);
    }

    private static bool TryGetGroup(string categoryId, out ToolGroup group)
    {
        if (Enum.TryParse<ToolCategory>(categoryId, out var category) &&
            ToolGrouping.CategoryGroups.TryGetValue(category, out group))
        {
            return true;
        }

        group = default;
        return false;
    }
}
