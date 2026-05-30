using GcToolkit.Core.Localization;
using GcToolkit.Core.Tests.Fakes;

namespace GcToolkit.Core.Tests.Localization;

[TestClass]
public class LanguageServiceTests
{
    [TestMethod]
    public void Current_FirstRun_SystemEnglish_ResolvesToEnglish()
    {
        var service = new LanguageService(new InMemoryPreferences(), "en");

        Assert.AreEqual("en", service.Current.Code);
    }

    [TestMethod]
    public void Current_FirstRun_SystemCzech_ResolvesToCzech()
    {
        var service = new LanguageService(new InMemoryPreferences(), "cs");

        Assert.AreEqual("cs", service.Current.Code);
    }

    [TestMethod]
    public void Current_FirstRun_UnsupportedSystemLanguage_DefaultsToEnglish()
    {
        var service = new LanguageService(new InMemoryPreferences(), "de");

        Assert.AreEqual("en", service.Current.Code);
    }

    [TestMethod]
    public async Task SetAsync_PersistsSelectedCode()
    {
        var preferences = new InMemoryPreferences();
        var service = new LanguageService(preferences, "en");

        await service.SetAsync("cs");

        // A fresh instance (even with an English system) must read back the persisted choice.
        var reloaded = new LanguageService(preferences, "en");
        Assert.AreEqual("cs", reloaded.Current.Code);
    }

    [TestMethod]
    public void Available_ContainsEnglishAndCzech()
    {
        var service = new LanguageService(new InMemoryPreferences(), "en");

        CollectionAssert.AreEquivalent(new[] { "en", "cs" }, service.Available.Select(l => l.Code).ToArray());
    }

    [TestMethod]
    public async Task SetAsync_UnsupportedCode_Throws()
    {
        var service = new LanguageService(new InMemoryPreferences(), "en");

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.SetAsync("fr"));
    }

    [TestMethod]
    public async Task SetAsync_RaisesLanguageChanged_OnlyWhenChanged()
    {
        var service = new LanguageService(new InMemoryPreferences(), "en");
        var raised = 0;
        service.LanguageChanged += (_, _) => raised++;

        await service.SetAsync("cs");
        await service.SetAsync("cs");

        Assert.AreEqual(1, raised);
    }
}
