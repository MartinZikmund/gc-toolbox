using GcToolkit.Core.Catalog;
using GcToolkit.Core.Search;
using GcToolkit.Core.Tests.Fakes;

namespace GcToolkit.Core.Tests.Catalog;

[TestClass]
public class CatalogServiceTests
{
    private static ToolDescriptor ToolFor(string id, string categoryId, string? nameKey = null)
        => new(id, nameKey ?? $"Name_{id}", categoryId, [], null, typeof(object));

    private static CatalogService Create(
        IEnumerable<Category> categories,
        IEnumerable<ToolDescriptor> tools)
        => new(
            [new StubCategoryContributor([.. categories])],
            [new StubToolContributor([.. tools])],
            new ToolMatcher(),
            new FakeStringLocalizer());

    [TestMethod]
    public void GetCategories_OrdersByOrderThenId()
    {
        var service = Create(
            [new Category("ciphers", "Category_Ciphers", 1), new Category("coordinates", "Category_Coordinates", 0)],
            []);

        var ids = service.GetCategories().Select(c => c.Id).ToList();

        CollectionAssert.AreEqual(new[] { "coordinates", "ciphers" }, ids);
    }

    [TestMethod]
    public void GetTools_OrdersByCategoryThenId_Deterministically()
    {
        var service = Create(
            [new Category("a", "Name_A", 0), new Category("b", "Name_B", 1)],
            [ToolFor("b.two", "b"), ToolFor("a.two", "a"), ToolFor("a.one", "a")]);

        var ids = service.GetTools().Select(t => t.Id).ToList();

        CollectionAssert.AreEqual(new[] { "a.one", "a.two", "b.two" }, ids);
    }

    [TestMethod]
    public void GetToolsByCategory_ReturnsOnlyMatchingCategory()
    {
        var service = Create(
            [new Category("a", "Name_A", 0), new Category("b", "Name_B", 1)],
            [ToolFor("a.one", "a"), ToolFor("a.two", "a"), ToolFor("b.one", "b")]);

        var ids = service.GetToolsByCategory("a").Select(t => t.Id).ToList();

        CollectionAssert.AreEquivalent(new[] { "a.one", "a.two" }, ids);
    }

    [TestMethod]
    public void GetToolsByCategory_UnknownCategory_ReturnsEmpty()
    {
        var service = Create([new Category("a", "Name_A", 0)], [ToolFor("a.one", "a")]);

        Assert.AreEqual(0, service.GetToolsByCategory("does-not-exist").Count);
    }

    [TestMethod]
    public void Constructor_EmptyCatalog_HasNoCategoriesOrTools()
    {
        var service = Create([], []);

        Assert.AreEqual(0, service.GetCategories().Count);
        Assert.AreEqual(0, service.GetTools().Count);
    }

    [TestMethod]
    public void Constructor_DuplicateToolId_Throws()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            Create([new Category("a", "Name_A", 0)], [ToolFor("dup", "a"), ToolFor("dup", "a")]));

        StringAssert.Contains(ex.Message, "dup");
    }

    [TestMethod]
    public void Constructor_UnknownCategoryId_Throws()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            Create([new Category("a", "Name_A", 0)], [ToolFor("orphan", "ghost")]));

        StringAssert.Contains(ex.Message, "ghost");
    }

    [TestMethod]
    public void Constructor_DuplicateCategoryId_Throws()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            Create([new Category("a", "Name_A", 0), new Category("a", "Name_A2", 1)], []));

        StringAssert.Contains(ex.Message, "a");
    }
}
