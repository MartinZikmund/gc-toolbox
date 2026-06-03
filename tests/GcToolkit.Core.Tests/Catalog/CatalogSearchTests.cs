using GcToolkit.Core.Catalog;
using GcToolkit.Core.Search;
using GcToolkit.Core.Tests.Fakes;

namespace GcToolkit.Core.Tests.Catalog;

[TestClass]
public class CatalogSearchTests
{
    private static CatalogService Create()
    {
        Category[] categories = [new Category("coordinates", "Category_Coordinates", 0)];

        ToolDescriptor[] tools =
        [
            new("coordinates.conversion", "Name_Conversion", "coordinates", ["wgs84", "gps"], "", typeof(object)),
            new("coordinates.solution", "Name_Solution", "coordinates", ["answer"], "", typeof(object)),
        ];

        // Localized names: one accented Czech name to exercise accent-insensitive search.
        var names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name_Conversion"] = "Coordinate conversion",
            ["Name_Solution"] = "Řešení",
        };

        return new CatalogService(
            [new StubCategoryContributor(categories)],
            [new StubToolContributor(tools)],
            new ToolMatcher(),
            new FakeStringLocalizer(names));
    }

    [TestMethod]
    public void Search_NarrowsToMatchingTools()
    {
        var results = Create().Search("conversion");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("coordinates.conversion", results[0].Id);
    }

    [TestMethod]
    public void Search_MatchesKeyword()
    {
        var results = Create().Search("gps");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("coordinates.conversion", results[0].Id);
    }

    [TestMethod]
    public void Search_AccentInsensitive_MatchesLocalizedName()
    {
        // "reseni" (no diacritics) must match the localized name "Řešení".
        var results = Create().Search("reseni");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("coordinates.solution", results[0].Id);
    }

    [TestMethod]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var results = Create().Search("zzzznomatch");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Search_EmptyQuery_ReturnsFullCatalog()
    {
        var service = Create();

        Assert.AreEqual(service.GetTools().Count, service.Search(string.Empty).Count);
    }

    [TestMethod]
    public void Search_WhitespaceQuery_ReturnsFullCatalog()
    {
        var service = Create();

        Assert.AreEqual(service.GetTools().Count, service.Search("   ").Count);
    }
}
