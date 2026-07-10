using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Ciphers;

/// <summary>
/// Covers the thin-VM behaviors layered on top of the pure <c>OneTimePad</c> codec: live recompute and
/// the encrypt/decrypt auto-swap (toggling direction carries the previous result back into the input).
/// </summary>
[TestClass]
public sealed class OneTimePadViewModelTests
{
    [TestMethod]
    public void InputText_WhileEncrypting_ProducesLiveOutput()
    {
        var sut = CreateSut();
        sut.KeyText = "XMCKL";

        sut.InputText = "HELLO";

        Assert.AreEqual("EQNVZ", sut.OutputText);
        Assert.IsTrue(sut.HasOutput);
    }

    [TestMethod]
    public void ToggleDirection_CarriesOutputIntoInputForOneTapRoundTrip()
    {
        var sut = CreateSut();
        sut.KeyText = "XMCKL";
        sut.InputText = "HELLO"; // encrypts to EQNVZ

        sut.DirectionIndex = 1; // switch to decrypt

        // The ciphertext became the new input, and decrypting it recovers the plaintext.
        Assert.AreEqual("EQNVZ", sut.InputText);
        Assert.AreEqual("HELLO", sut.OutputText);
    }

    [TestMethod]
    public void ToggleDirectionTwice_ReturnsToOriginalPlaintext()
    {
        var sut = CreateSut();
        sut.KeyText = "XMCKL";
        sut.InputText = "HELLO";

        sut.DirectionIndex = 1; // decrypt EQNVZ -> HELLO
        sut.DirectionIndex = 0; // encrypt HELLO -> EQNVZ

        Assert.AreEqual("HELLO", sut.InputText);
        Assert.AreEqual("EQNVZ", sut.OutputText);
    }

    [TestMethod]
    public void GeneratePad_ProducesKeyAsLongAsTheMessage()
    {
        var sut = CreateSut();
        sut.InputText = "ATTACKATDAWN";

        sut.GeneratePadCommand.Execute(null);

        Assert.AreEqual(sut.InputText.Length, sut.KeyText.Length);
        Assert.IsFalse(sut.KeyTooShort);
    }

    private static OneTimePadViewModel CreateSut()
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
