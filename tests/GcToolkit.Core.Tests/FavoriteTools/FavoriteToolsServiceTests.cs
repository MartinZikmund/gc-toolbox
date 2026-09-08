using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Tests.Fakes;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Tests.FavoriteTools;

[TestClass]
public class FavoriteToolsServiceTests
{
    private static FavoriteToolsService Create(IPreferences preferences, params string[] catalogIds)
        => new(preferences, new StubCatalogService(catalogIds));

    [TestMethod]
    public async Task ToggleAsync_WhenAbsent_AddsFavorite()
    {
        var service = Create(new InMemoryPreferences(), "a");

        await service.ToggleAsync("a");

        Assert.IsTrue(service.IsFavorite("a"));
        CollectionAssert.AreEqual(new[] { "a" }, service.GetFavoriteToolIds().ToArray());
    }

    [TestMethod]
    public async Task ToggleAsync_WhenPresent_RemovesFavorite()
    {
        var service = Create(new InMemoryPreferences(), "a");

        await service.ToggleAsync("a");
        await service.ToggleAsync("a");

        Assert.IsFalse(service.IsFavorite("a"));
        Assert.AreEqual(0, service.GetFavoriteToolIds().Count);
    }

    [TestMethod]
    public async Task GetFavoriteToolIds_OrdersByAddition()
    {
        var service = Create(new InMemoryPreferences(), "a", "b", "c");

        await service.ToggleAsync("a");
        await service.ToggleAsync("b");
        await service.ToggleAsync("c");

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, service.GetFavoriteToolIds().ToArray());
    }

    [TestMethod]
    public async Task FavoriteTools_PersistAcrossServiceInstances()
    {
        var preferences = new InMemoryPreferences();
        var first = Create(preferences, "a");
        await first.ToggleAsync("a");

        var second = Create(preferences, "a");

        Assert.IsTrue(second.IsFavorite("a"));
    }

    [TestMethod]
    public async Task GetFavoriteToolIds_PrunesIdsMissingFromCatalog()
    {
        var service = Create(new InMemoryPreferences(), "a");

        await service.ToggleAsync("a");
        await service.ToggleAsync("ghost");

        CollectionAssert.AreEqual(new[] { "a" }, service.GetFavoriteToolIds().ToArray());
    }

    [TestMethod]
    public async Task ToggleAsync_RaisesFavoriteToolsChanged()
    {
        var service = Create(new InMemoryPreferences(), "a");
        var raised = 0;
        service.FavoriteToolsChanged += (_, _) => raised++;

        await service.ToggleAsync("a");

        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void Constructor_CorruptStoredData_FallsBackToEmpty()
    {
        var preferences = new InMemoryPreferences();
        preferences.SetRawComplex("favoriteTools", "{ not valid json ]");

        var service = Create(preferences, "a");

        Assert.AreEqual(0, service.GetFavoriteToolIds().Count);
    }

    [TestMethod]
    public async Task ToggleAsync_OnATrimmedHead_StillPersists()
    {
        // Trimmed heads (the WASM release publish) resolve only the source-generated contracts.
        var preferences = InMemoryPreferences.TrimmedHead();
        var service = Create(preferences, "a");

        await service.ToggleAsync("a");

        var reloaded = Create(preferences, "a");
        CollectionAssert.AreEqual(new[] { "a" }, reloaded.GetFavoriteToolIds().ToArray());
    }

    [TestMethod]
    public void Constructor_DataWrittenByTheComplexApi_IsStillRead()
    {
        // Earlier builds persisted through IPreferences.SetComplex; the stored shape must keep working.
        var preferences = new InMemoryPreferences();
        preferences.SetComplex("favoriteTools", new List<FavoriteToolEntry>
        {
            new("a", DateTimeOffset.UtcNow.AddMinutes(-1)),
            new("b", DateTimeOffset.UtcNow),
        });

        var service = Create(preferences, "a", "b");

        CollectionAssert.AreEqual(new[] { "a", "b" }, service.GetFavoriteToolIds().ToArray());
    }
}
