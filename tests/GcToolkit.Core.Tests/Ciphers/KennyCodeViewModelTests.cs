using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Ciphers;

/// <summary>
/// Covers the substitution-chart "keypad" behavior: clicking a tile appends the letter while
/// encoding and the trigram while decoding (issue #21 enhancement).
/// </summary>
[TestClass]
public sealed class KennyCodeViewModelTests
{
    [TestMethod]
    public void AppendFromTable_WhileEncoding_AppendsLowercaseLetterAndEncodes()
    {
        var sut = CreateSut(); // DirectionIndex defaults to 0 (encode)

        sut.AppendFromTableCommand.Execute(new KennyCodeTableRow('G', "mfm"));

        Assert.AreEqual("g", sut.InputText);
        Assert.AreEqual("mfm", sut.OutputText);
    }

    [TestMethod]
    public void AppendFromTable_WhileEncoding_AccumulatesAcrossClicks()
    {
        var sut = CreateSut();

        sut.AppendFromTableCommand.Execute(new KennyCodeTableRow('G', "mfm"));
        sut.AppendFromTableCommand.Execute(new KennyCodeTableRow('E', "mpp"));
        sut.AppendFromTableCommand.Execute(new KennyCodeTableRow('O', "ppf"));

        Assert.AreEqual("geo", sut.InputText);
        Assert.AreEqual("mfm mpp ppf", sut.OutputText);
    }

    [TestMethod]
    public void AppendFromTable_WhileDecoding_AppendsTrigramWithTrailingSpaceAndDecodes()
    {
        var sut = CreateSut();
        sut.DirectionIndex = 1; // decode

        sut.AppendFromTableCommand.Execute(new KennyCodeTableRow('A', "mmm"));

        Assert.AreEqual("mmm ", sut.InputText);
        Assert.AreEqual("a", sut.OutputText);
    }

    [TestMethod]
    public void AppendFromTable_WhileDecoding_BuildsDecodableInputAcrossClicks()
    {
        var sut = CreateSut();
        sut.DirectionIndex = 1;

        sut.AppendFromTableCommand.Execute(new KennyCodeTableRow('A', "mmm"));
        sut.AppendFromTableCommand.Execute(new KennyCodeTableRow('B', "mmp"));
        sut.AppendFromTableCommand.Execute(new KennyCodeTableRow('C', "mmf"));

        Assert.AreEqual("mmm mmp mmf ", sut.InputText);
        Assert.AreEqual("abc", sut.OutputText);
    }

    private static KennyCodeViewModel CreateSut()
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
