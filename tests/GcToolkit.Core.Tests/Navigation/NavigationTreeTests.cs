using System;
using System.Collections.Generic;
using System.Linq;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.Navigation;

namespace GcToolkit.Core.Tests.Navigation;

[TestClass]
public class NavigationTreeTests
{
    // Category Ids are ToolCategory enum member names; Order is the enum index.
    private static Category CategoryFor(string id, int order)
        => new(id, $"Category_{id}", order, $"Icon_{id}");

    private static ToolDescriptor ToolFor(string id, string categoryId)
        => new(id, $"Name_{id}", categoryId, [], $"Icon_{id}", typeof(object));

    [TestMethod]
    public void Build_CategoryWithoutTools_OmitsEmptyCategory()
    {
        // Coordinates has tools; Ciphers is empty and must be omitted (FR-024).
        NavigationTree tree = NavigationTreeBuilder.Build(
            [CategoryFor("Coordinates", 0), CategoryFor("Ciphers", 1)],
            [ToolFor("Coordinates.One", "Coordinates")]);

        var categoryIds = tree.Roots
            .SelectMany(g => g.Categories)
            .Select(c => c.CategoryId)
            .ToList();

        CollectionAssert.AreEqual(new[] { "Coordinates" }, categoryIds);
    }

    [TestMethod]
    public void Build_GroupWithOnlyEmptyCategories_OmitsGroup()
    {
        // Both Conversion-group categories are empty; only the standalone Ciphers survives,
        // so no Conversion group node is produced.
        NavigationTree tree = NavigationTreeBuilder.Build(
            [CategoryFor("Coordinates", 0), CategoryFor("Ciphers", 1), CategoryFor("Numbers", 2)],
            [ToolFor("Ciphers.One", "Ciphers")]);

        Assert.IsFalse(tree.Roots.Any(g => g.GroupId == "Conversion"));
        Assert.AreEqual(1, tree.Roots.Count);
        Assert.IsNull(tree.Roots[0].GroupId);
        Assert.AreEqual("Ciphers", tree.Roots[0].Categories.Single().CategoryId);
    }

    [TestMethod]
    public void Build_GrouplessCategory_BecomesTopLevelNodeWithNullGroupId()
    {
        // Ciphers has no group → top-level NavGroupNode with GroupId == null and no NameKey.
        NavigationTree tree = NavigationTreeBuilder.Build(
            [CategoryFor("Ciphers", 1)],
            [ToolFor("Ciphers.One", "Ciphers")]);

        NavGroupNode root = tree.Roots.Single();
        Assert.IsNull(root.GroupId);
        Assert.IsNull(root.NameKey);
        Assert.AreEqual("Ciphers", root.Categories.Single().CategoryId);
    }

    [TestMethod]
    public void Build_WithGroupedCategories_CollapseUnderSingleGroup()
    {
        // Coordinates (0) and Numbers (2) both map to the Conversion group and must collapse
        // into one NavGroupNode, sorted at its earliest category — before the standalone Ciphers (1).
        NavigationTree tree = NavigationTreeBuilder.Build(
            [CategoryFor("Coordinates", 0), CategoryFor("Ciphers", 1), CategoryFor("Numbers", 2)],
            [ToolFor("Coordinates.One", "Coordinates"), ToolFor("Ciphers.One", "Ciphers"), ToolFor("Numbers.One", "Numbers")],
            new Dictionary<ToolCategory, ToolGroup>
            {
                [ToolCategory.Coordinates] = ToolGroup.Conversion,
                [ToolCategory.Numbers] = ToolGroup.Conversion,
            });

        Assert.AreEqual(2, tree.Roots.Count);

        NavGroupNode conversion = tree.Roots[0];
        Assert.AreEqual("Conversion", conversion.GroupId);
        Assert.AreEqual("Group_Conversion", conversion.NameKey);
        CollectionAssert.AreEqual(
            new[] { "Coordinates", "Numbers" },
            conversion.Categories.Select(c => c.CategoryId).ToList());

        NavGroupNode standalone = tree.Roots[1];
        Assert.IsNull(standalone.GroupId);
        Assert.AreEqual("Ciphers", standalone.Categories.Single().CategoryId);
    }

    [TestMethod]
    public void Build_WithProductionGrouping_KeepsEveryCategoryTopLevel()
    {
        // Navigation is flat today: ToolGrouping.CategoryGroups is empty, so no category is
        // collapsed under a heading and each becomes its own groupless root, in category order.
        NavigationTree tree = NavigationTreeBuilder.Build(
            [CategoryFor("Coordinates", 0), CategoryFor("Ciphers", 1), CategoryFor("Numbers", 2)],
            [ToolFor("Coordinates.One", "Coordinates"), ToolFor("Ciphers.One", "Ciphers"), ToolFor("Numbers.One", "Numbers")]);

        Assert.AreEqual(3, tree.Roots.Count);
        Assert.IsTrue(tree.Roots.All(r => r.GroupId is null && r.NameKey is null));
        CollectionAssert.AreEqual(
            new[] { "Coordinates", "Ciphers", "Numbers" },
            tree.Roots.Select(r => r.Categories.Single().CategoryId).ToList());
    }

    [TestMethod]
    public void Build_ToolsWithinCategory_OrderedByIdOrdinal()
    {
        NavigationTree tree = NavigationTreeBuilder.Build(
            [CategoryFor("Coordinates", 0)],
            [ToolFor("Coordinates.zebra", "Coordinates"), ToolFor("Coordinates.Apple", "Coordinates"), ToolFor("Coordinates.mango", "Coordinates")]);

        var toolIds = tree.Roots
            .Single()
            .Categories
            .Single()
            .Tools
            .Select(t => t.ToolId)
            .ToList();

        // Ordinal sort: uppercase 'A' precedes lowercase letters.
        CollectionAssert.AreEqual(
            new[] { "Coordinates.Apple", "Coordinates.mango", "Coordinates.zebra" },
            toolIds);
    }
}
