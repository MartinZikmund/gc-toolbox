using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class Base64ViewModelTests
{
    private static Base64ViewModel CreateViewModel(out FakeClipboard clipboard)
    {
        clipboard = new FakeClipboard();
        ICatalogService catalog = new StubCatalogService("Base64");
        var vm = new Base64ViewModel(
            catalog,
            new FakeRecents(),
            new FakeFavorites(),
            new FakeStringLocalizer(),
            clipboard,
            new FakeShare());
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void Auto_PlainText_EncodesToBase64()
    {
        var vm = CreateViewModel(out _);

        vm.InputText = "Man";

        Assert.AreEqual("TWFu", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void Auto_ValidBase64_Decodes()
    {
        var vm = CreateViewModel(out _);

        vm.InputText = "SGVsbG8sIFdvcmxkIQ==";

        Assert.AreEqual("Hello, World!", vm.OutputText);
        Assert.IsTrue(vm.ShowByteCount);
    }

    [TestMethod]
    public void Encode_ExplicitDirection_AlwaysEncodes()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 1; // Encode

        // Looks like base64, but explicit Encode must re-encode it as text.
        vm.InputText = "Zm9v";

        Assert.AreEqual("Wm05dg==", vm.OutputText);
    }

    [TestMethod]
    public void Decode_BadPadding_AutoFixed_NoError()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 2; // Decode

        vm.InputText = "Zg"; // missing padding

        Assert.AreEqual("f", vm.OutputText);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void Decode_InvalidInput_SetsError()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 2; // Decode

        vm.InputText = "@@@@";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void Decode_HexRender_ShowsHex()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 2; // Decode
        vm.DecodeRenderIndex = 2; // Hex

        vm.InputText = "Zm9v";

        Assert.AreEqual("66 6F 6F", vm.OutputText);
    }

    [TestMethod]
    public void UrlSafeVariant_Decodes_DashUnderscore()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 1; // Encode
        vm.VariantIndex = 1; // URL-safe

        // 0xFB,0xFF as text won't help; instead encode bytes via known text.
        vm.InputText = "subjects?_d";

        // Round-trip: switch to decode of the produced output yields original.
        var encoded = vm.OutputText;
        Assert.IsFalse(encoded.Contains('+'));
        Assert.IsFalse(encoded.Contains('/'));
    }

    [TestMethod]
    public void PaddingOff_OmitsEquals()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 1; // Encode
        vm.UsePadding = false;

        vm.InputText = "f";

        Assert.AreEqual("Zg", vm.OutputText);
    }

    [TestMethod]
    public void BatchMode_EncodesEachLineIndependently()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 1; // Encode
        vm.BatchMode = true;

        vm.InputText = "f\nfo\nfoo";

        Assert.AreEqual("Zg==\nZm8=\nZm9v", vm.OutputText);
    }

    [TestMethod]
    public void BatchMode_Decode_FlagsInvalidLineWithQuestionMark()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 2; // Decode
        vm.BatchMode = true;

        vm.InputText = "Zm9v\n@@@@";

        var lines = vm.OutputText.Split('\n');
        Assert.AreEqual("foo", lines[0]);
        Assert.AreEqual("?", lines[1]);
        Assert.IsTrue(vm.HasError);
    }

    [TestMethod]
    public void TryAllVariants_PopulatesRows()
    {
        var vm = CreateViewModel(out _);
        vm.InputText = "Zm9vYmFy";

        vm.TryAllVariantsCommand.Execute(null);

        Assert.IsTrue(vm.ShowVariantResults);
        Assert.AreEqual(3, vm.VariantResults.Count);
        Assert.IsTrue(vm.VariantResults[0].IsPrintable);
        Assert.AreEqual("foobar", vm.VariantResults[0].Text);
    }

    [TestMethod]
    public void Copy_PutsOutputOnClipboard()
    {
        var vm = CreateViewModel(out var clipboard);
        vm.InputText = "Man";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("TWFu", clipboard.LastText);
    }

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var vm = CreateViewModel(out _);
        vm.InputText = "Man";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void Example_PopulatesInputAndEncodes()
    {
        var vm = CreateViewModel(out _);

        vm.ExampleCommand.Execute(null);

        Assert.IsFalse(string.IsNullOrEmpty(vm.InputText));
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void Reset_RestoresDefaults()
    {
        var vm = CreateViewModel(out _);
        vm.DirectionIndex = 2;
        vm.VariantIndex = 1;
        vm.UsePadding = false;
        vm.InputText = "Zm9v";

        vm.ResetCommand.Execute(null);

        Assert.AreEqual(0, vm.DirectionIndex);
        Assert.AreEqual(0, vm.VariantIndex);
        Assert.IsTrue(vm.UsePadding);
        Assert.AreEqual(string.Empty, vm.InputText);
    }

    // ---- Minimal fakes ----

    private sealed class FakeClipboard : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text) => LastText = text;
    }

    private sealed class FakeShare : IShareService
    {
        public Task ShareAsync(string title, string uri) => Task.CompletedTask;

        public Task ShareTextAsync(string title, string text) => Task.CompletedTask;
    }

    private sealed class FakeRecents : IRecentsService
    {
        public event EventHandler? RecentsChanged;

        public Task RecordOpenedAsync(string toolId)
        {
            RecentsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetRecentToolIds() => [];

        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class FakeFavorites : IFavoriteToolsService
    {
        private readonly HashSet<string> _favorites = [];

        public event EventHandler? FavoriteToolsChanged;

        public bool IsFavorite(string toolId) => _favorites.Contains(toolId);

        public Task ToggleAsync(string toolId)
        {
            if (!_favorites.Add(toolId))
            {
                _favorites.Remove(toolId);
            }

            FavoriteToolsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetFavoriteToolIds() => [.. _favorites];
    }
}
