using GcToolkit.Core.Ciphers;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Ciphers;

/// <summary>
/// Covers the two Bacon UX behaviors aligned with Kenny code (issue #6 enhancement): toggling
/// the direction auto-swaps the previous result into the input, and the reference chart doubles
/// as a tap-to-insert keypad.
/// </summary>
[TestClass]
public sealed class BaconCipherViewModelTests
{
    [TestMethod]
    public void TogglingDirection_CarriesPreviousResultIntoInputAndRoundTrips()
    {
        var sut = CreateSut();
        sut.InputText = "ab"; // encrypt: a=AAAAA, b=AAAAB
        Assert.AreEqual("AAAAA AAAAB", sut.OutputText);

        sut.DirectionIndex = 1; // switch to decrypt -> auto-swap

        Assert.AreEqual("AAAAA AAAAB", sut.InputText);
        Assert.AreEqual("AB", sut.OutputText);
    }

    [TestMethod]
    public void AppendFromTable_WhileEncrypting_AppendsLowercaseLetterAndEncodes()
    {
        var sut = CreateSut(); // DirectionIndex defaults to 0 (encrypt)

        sut.AppendFromTableCommand.Execute(new BaconTableRow('H', "AABBB"));

        Assert.AreEqual("h", sut.InputText);
        Assert.AreEqual("AABBB", sut.OutputText);
    }

    [TestMethod]
    public void AppendFromTable_WhileDecrypting_AppendsCodeWithTrailingSpaceAndDecodes()
    {
        var sut = CreateSut();
        sut.DirectionIndex = 1; // decrypt

        sut.AppendFromTableCommand.Execute(new BaconTableRow('A', "AAAAA"));

        Assert.AreEqual("AAAAA ", sut.InputText);
        Assert.AreEqual("A", sut.OutputText);
    }

    [TestMethod]
    public void ReferenceTable_ReflectsCustomSymbols()
    {
        var sut = CreateSut();

        sut.FirstSymbol = "0";
        sut.SecondSymbol = "1";

        Assert.AreEqual("00000", sut.ReferenceTable.First(r => r.Letter == 'A').Code);
        Assert.AreEqual("00001", sut.ReferenceTable.First(r => r.Letter == 'B').Code);
    }

    [TestMethod]
    public void ReferenceTable_ReflectsSwappedSymbols()
    {
        var sut = CreateSut();

        sut.SwapSymbols = true; // A/B roles invert, so A's AAAAA renders as BBBBB

        Assert.AreEqual("BBBBB", sut.ReferenceTable.First(r => r.Letter == 'A').Code);
    }

    private static BaconCipherViewModel CreateSut()
        => new(
            new StubCatalogService(),
            new NoopRecents(),
            new NoopFavorites(),
            new FakeStringLocalizer(),
            new NoopClipboard(),
            new NoopShare());

    private sealed class NoopRecents : IRecentsService
    {
        public event EventHandler? RecentsChanged;

        public Task RecordOpenedAsync(string toolId) => Task.CompletedTask;

        public IReadOnlyList<string> GetRecentToolIds() => [];

        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class NoopFavorites : IFavoriteToolsService
    {
        public event EventHandler? FavoriteToolsChanged;

        public bool IsFavorite(string toolId) => false;

        public Task ToggleAsync(string toolId) => Task.CompletedTask;

        public IReadOnlyList<string> GetFavoriteToolIds() => [];
    }

    private sealed class NoopClipboard : IClipboardService
    {
        public void SetText(string text) { }
    }

    private sealed class NoopShare : IShareService
    {
        public Task ShareAsync(string title, string uri) => Task.CompletedTask;

        public Task ShareTextAsync(string title, string text) => Task.CompletedTask;
    }
}
