using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Tests.Recents;

[TestClass]
public class RecentsServiceTests
{
    private static RecentsService Create(IPreferences preferences, params string[] catalogIds)
        => new(preferences, new StubCatalogService(catalogIds));

    [TestMethod]
    public async Task RecordOpenedAsync_OrdersMostRecentFirst()
    {
        var service = Create(new InMemoryPreferences(), "a", "b");

        await service.RecordOpenedAsync("a");
        await service.RecordOpenedAsync("b");

        CollectionAssert.AreEqual(new[] { "b", "a" }, service.GetRecentToolIds().ToArray());
    }

    [TestMethod]
    public async Task RecordOpenedAsync_Duplicate_DedupesAndBumpsToTop()
    {
        var service = Create(new InMemoryPreferences(), "a", "b", "c");

        await service.RecordOpenedAsync("a");
        await service.RecordOpenedAsync("b");
        await service.RecordOpenedAsync("c");
        await service.RecordOpenedAsync("a");

        CollectionAssert.AreEqual(new[] { "a", "c", "b" }, service.GetRecentToolIds().ToArray());
    }

    [TestMethod]
    public async Task RecordOpenedAsync_CapsAtTen_EvictingOldest()
    {
        var ids = Enumerable.Range(0, 12).Select(i => $"t{i}").ToArray();
        var service = Create(new InMemoryPreferences(), ids);

        foreach (var id in ids)
        {
            await service.RecordOpenedAsync(id);
        }

        var recents = service.GetRecentToolIds();

        Assert.AreEqual(RecentsService.MaxEntries, recents.Count);
        Assert.AreEqual("t11", recents[0]);
        CollectionAssert.DoesNotContain(recents.ToArray(), "t0");
        CollectionAssert.DoesNotContain(recents.ToArray(), "t1");
    }

    [TestMethod]
    public async Task ClearAsync_EmptiesTheList()
    {
        var service = Create(new InMemoryPreferences(), "a");

        await service.RecordOpenedAsync("a");
        await service.ClearAsync();

        Assert.AreEqual(0, service.GetRecentToolIds().Count);
    }

    [TestMethod]
    public async Task Recents_PersistAcrossServiceInstances()
    {
        var preferences = new InMemoryPreferences();
        var first = Create(preferences, "a", "b");
        await first.RecordOpenedAsync("a");
        await first.RecordOpenedAsync("b");

        var second = Create(preferences, "a", "b");

        CollectionAssert.AreEqual(new[] { "b", "a" }, second.GetRecentToolIds().ToArray());
    }

    [TestMethod]
    public async Task GetRecentToolIds_PrunesIdsMissingFromCatalog()
    {
        var service = Create(new InMemoryPreferences(), "a");

        await service.RecordOpenedAsync("a");
        await service.RecordOpenedAsync("ghost");

        CollectionAssert.AreEqual(new[] { "a" }, service.GetRecentToolIds().ToArray());
    }

    [TestMethod]
    public async Task RecordOpenedAsync_RaisesRecentsChanged()
    {
        var service = Create(new InMemoryPreferences(), "a");
        var raised = 0;
        service.RecentsChanged += (_, _) => raised++;

        await service.RecordOpenedAsync("a");

        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void Constructor_CorruptStoredData_FallsBackToEmpty()
    {
        var preferences = new InMemoryPreferences();
        preferences.SetRawComplex("recents", "<<<not json>>>");

        var service = Create(preferences, "a");

        Assert.AreEqual(0, service.GetRecentToolIds().Count);
    }

    [TestMethod]
    public async Task RecordOpenedAsync_OnATrimmedHead_StillPersists()
    {
        // Trimmed heads (the WASM release publish) resolve only the source-generated contracts.
        var preferences = InMemoryPreferences.TrimmedHead();
        var service = Create(preferences, "a", "b");

        await service.RecordOpenedAsync("a");
        await service.RecordOpenedAsync("b");

        var reloaded = Create(preferences, "a", "b");
        CollectionAssert.AreEqual(new[] { "b", "a" }, reloaded.GetRecentToolIds().ToArray());
    }

    [TestMethod]
    public void Constructor_DataWrittenByTheComplexApi_IsStillRead()
    {
        // Earlier builds persisted through IPreferences.SetComplex; the stored shape must keep working.
        var preferences = new InMemoryPreferences();
        preferences.SetComplex("recents", new List<RecentEntry>
        {
            new("b", DateTimeOffset.UtcNow),
            new("a", DateTimeOffset.UtcNow.AddMinutes(-1)),
        });

        var service = Create(preferences, "a", "b");

        CollectionAssert.AreEqual(new[] { "b", "a" }, service.GetRecentToolIds().ToArray());
    }
}
