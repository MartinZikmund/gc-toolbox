using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Search;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class CatalogViewModelScopeTests
{
    private static CatalogViewModel Create(out SearchViewModel search)
    {
        var localizer = new FakeStringLocalizer();
        var catalog = new CatalogService(
            [new StubCategoryContributor(
                new Category("Coordinates", "Category_Coordinates", 0, "Coordinates"),
                new Category("Ciphers", "Category_Ciphers", 1, "Ciphers"))],
            [new StubToolContributor(
                new ToolDescriptor("CoordA", "CoordA_Name", "Coordinates", [], "CoordA", typeof(object)),
                new ToolDescriptor("CipherA", "CipherA_Name", "Ciphers", [], "CipherA", typeof(object)))],
            new ToolMatcher(),
            localizer);

        var nav = new FakeNavigationService();
        search = new SearchViewModel(nav);
        var favorites = new FavoriteToolsService(new InMemoryPreferences(), catalog);
        return new CatalogViewModel(catalog, favorites, nav, localizer, search);
    }

    [TestMethod]
    public void OnNavigatedTo_NullParameter_ShowsAllCategories()
    {
        var vm = Create(out _);

        vm.OnNavigatedTo(null);

        Assert.AreEqual(2, vm.Groups.Count);
    }

    [TestMethod]
    public void OnNavigatedTo_CategoryId_ScopesToThatCategory()
    {
        var vm = Create(out _);

        vm.OnNavigatedTo("Coordinates");

        Assert.AreEqual(1, vm.Groups.Count);
        CollectionAssert.AreEqual(new[] { "CoordA" }, vm.Groups[0].Tools.Select(t => t.ToolId).ToList());
    }

    [TestMethod]
    public void OnNavigatedTo_AfterScoping_NullParameter_ResetsToUnscoped()
    {
        // Regression: CatalogView is a cached page, so the scope must reset on global navigation.
        var vm = Create(out _);

        vm.OnNavigatedTo("Coordinates");
        Assert.AreEqual(1, vm.Groups.Count);

        vm.OnNavigatedTo(null);

        Assert.AreEqual(2, vm.Groups.Count);
    }

    [TestMethod]
    public void ScopedMode_IgnoresSearchQuery()
    {
        // R12: a category-scoped catalog deliberately ignores the global search query.
        var vm = Create(out var search);
        search.Query = "zzz-no-match";

        vm.OnNavigatedTo("Coordinates");

        Assert.AreEqual(1, vm.Groups.Count);
        Assert.AreEqual("CoordA", vm.Groups[0].Tools[0].ToolId);
    }
}
