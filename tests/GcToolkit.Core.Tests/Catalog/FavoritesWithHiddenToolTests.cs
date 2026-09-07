using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Search;
using GcToolkit.Core.Services.Devices;
using GcToolkit.Core.Tests.Fakes;

namespace GcToolkit.Core.Tests.Catalog;

/// <summary>
/// A favorite pointing at a device-gated tool must vanish from the UI without being erased: the
/// same profile syncing to a phone has to get its compass favorite back.
/// </summary>
[TestClass]
public class FavoritesWithHiddenToolTests
{
    [RequiresDeviceCapability(DeviceCapability.Compass)]
    private sealed class CompassRequiringViewModel;

    private static CatalogService CreateCatalog(params DeviceCapability[] supported)
        => new(
            [new StubCategoryContributor(new Category("Field", "Category_Field", 0, "Field"))],
            [new StubToolContributor(
                new ToolDescriptor("Compass", "Compass_Name", "Field", [], "Compass", typeof(CompassRequiringViewModel)),
                new ToolDescriptor("Flashlight", "Flashlight_Name", "Field", [], "Flashlight", typeof(object)))],
            [new DeviceCapabilityToolPolicy(new FakeDeviceCapabilityService(supported))],
            new ToolMatcher(),
            new FakeStringLocalizer());

    [TestMethod]
    public async Task GetFavoriteToolIds_HiddenTool_IsPrunedButPersistedEntrySurvives()
    {
        InMemoryPreferences preferences = new();

        // Favorite the compass while the device has one.
        FavoriteToolsService onPhone = new(preferences, CreateCatalog(DeviceCapability.Compass));
        await onPhone.ToggleAsync("Compass");
        await onPhone.ToggleAsync("Flashlight");
        CollectionAssert.AreEqual(new[] { "Compass", "Flashlight" }, onPhone.GetFavoriteToolIds().ToList());

        // Same stored preferences, now on a machine with no magnetometer.
        FavoriteToolsService onDesktop = new(preferences, CreateCatalog());

        CollectionAssert.AreEqual(new[] { "Flashlight" }, onDesktop.GetFavoriteToolIds().ToList());

        var stored = preferences.GetComplex("favoriteTools", new List<FavoriteToolEntry>());
        CollectionAssert.Contains(stored.Select(e => e.ToolId).ToList(), "Compass");
    }

    [TestMethod]
    public async Task IsFavorite_HiddenTool_StaysTrue()
    {
        InMemoryPreferences preferences = new();
        FavoriteToolsService favorites = new(preferences, CreateCatalog());

        // Toggling a hidden id is not something the UI can do, but stale state must not throw.
        await favorites.ToggleAsync("Compass");

        Assert.IsTrue(favorites.IsFavorite("Compass"));
        Assert.AreEqual(0, favorites.GetFavoriteToolIds().Count);
    }
}
