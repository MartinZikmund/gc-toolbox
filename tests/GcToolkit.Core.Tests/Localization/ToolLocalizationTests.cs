using System;
using System.Collections.Generic;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Search;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.Localization;

[TestClass]
public class ToolLocalizationTests
{
    private static (ICatalogService Catalog, IRecentsService Recents, IFavoriteToolsService Favorites) CreateDependencies()
    {
        CatalogService catalog = new(
            [new StubCategoryContributor(new Category("Cat", "Cat_Name", 0, "Cat"))],
            [new StubToolContributor(new ToolDescriptor("Demo", "Demo_Name", "Cat", new string[0], "Demo", typeof(object), true, "Demo_Tooltip"))],
            new ToolMatcher(),
            new FakeStringLocalizer());

        RecentsService recents = new(new InMemoryPreferences(), catalog);
        FavoriteToolsService favorites = new(new InMemoryPreferences(), catalog);

        return (catalog, recents, favorites);
    }

    [TestMethod]
    public void ViewCreated_EnglishLocalizer_ResolvesNameAndTooltip()
    {
        var (catalog, recents, favorites) = CreateDependencies();
        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            { "Demo_Name", "Demo tool" },
            { "Demo_Tooltip", "A demo." },
        });

        DemoToolViewModel vm = new(catalog, recents, favorites, localizer);
        vm.ViewCreated();

        Assert.AreEqual("Demo tool", vm.ToolName);
        Assert.AreEqual("A demo.", vm.Tooltip);
        Assert.IsTrue(vm.IsPlaceholder);
    }

    [TestMethod]
    public void ViewCreated_CzechLocalizer_ResolvesLocalizedNameAndTooltip()
    {
        var (catalog, recents, favorites) = CreateDependencies();
        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            { "Demo_Name", "Demo nástroj" },
            { "Demo_Tooltip", "Ukázka." },
        });

        DemoToolViewModel vm = new(catalog, recents, favorites, localizer);
        vm.ViewCreated();

        Assert.AreEqual("Demo nástroj", vm.ToolName);
        Assert.AreEqual("Ukázka.", vm.Tooltip);
    }

    private sealed class DemoToolViewModel : ToolViewModelBase
    {
        public DemoToolViewModel(ICatalogService c, IRecentsService r, IFavoriteToolsService f, IStringLocalizer l)
            : base("Demo", c, r, f, l)
        {
        }
    }
}
