using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class BifidCipherViewModelTests
{
    private static BifidCipherViewModel CreateViewModel(out FakeClipboardService clipboard, out FakeShareService share)
    {
        StubCatalogService catalog = new("BifidCipher");
        InMemoryPreferences prefs = new();
        RecentsService recents = new(prefs, catalog);
        FavoriteToolsService favorites = new(prefs, catalog);
        FakeStringLocalizer localizer = new();
        clipboard = new FakeClipboardService();
        share = new FakeShareService();
        return new BifidCipherViewModel(catalog, recents, favorites, localizer, clipboard, share);
    }

    private static BifidCipherViewModel CreateViewModel()
        => CreateViewModel(out _, out _);

    [TestMethod]
    public void Constructor_RendersDefaultSquare_25Cells()
    {
        var vm = CreateViewModel();
        Assert.AreEqual(25, vm.SquareCells.Count);
    }

    [TestMethod]
    public void Encrypt_WikipediaSquareAndPlaintext_ProducesKnownCiphertext()
    {
        var vm = CreateViewModel();
        vm.SquareModeIndex = 1; // manual square
        vm.ManualSquare = "BGWKZQPNDSIOAXEFCLUMTHYVR";
        vm.InputText = "FLEEATONCE";

        Assert.AreEqual("UAEOLWRINS", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void Decrypt_WikipediaSquareAndCiphertext_RecoversPlaintext()
    {
        var vm = CreateViewModel();
        vm.SquareModeIndex = 1;
        vm.ManualSquare = "BGWKZQPNDSIOAXEFCLUMTHYVR";
        vm.DirectionIndex = 1; // decrypt
        vm.InputText = "UAEOLWRINS";

        Assert.AreEqual("FLEEATONCE", vm.OutputText);
    }

    [TestMethod]
    public void Keyword_RebuildsSquare_AndConvertsLive()
    {
        var vm = CreateViewModel();
        vm.Keyword = "GEOCACHE";
        vm.InputText = "FINDTHECACHE";

        var encrypted = vm.OutputText;
        Assert.IsTrue(encrypted.Length > 0);

        // Round-trip via swap recovers the plaintext.
        vm.SwapCommand.Execute(null);
        Assert.AreEqual("FINDTHECACHE", vm.OutputText);
    }

    [TestMethod]
    public void Swap_FlipsDirectionAndCarriesResult()
    {
        var vm = CreateViewModel();
        vm.InputText = "GEOCACHING";
        var cipher = vm.OutputText;

        vm.SwapCommand.Execute(null);

        Assert.AreEqual(1, vm.DirectionIndex);
        Assert.AreEqual(cipher, vm.InputText);
        Assert.AreEqual("GEOCACHING", vm.OutputText);
    }

    [TestMethod]
    public void ManualSquare_Invalid_ShowsWarningAndClearsOutput()
    {
        var vm = CreateViewModel();
        vm.InputText = "HELLO";
        vm.SquareModeIndex = 1;
        vm.ManualSquare = "ABC"; // too short

        Assert.IsTrue(vm.HasWarning);
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.AreEqual(0, vm.SquareCells.Count);
    }

    [TestMethod]
    public void Copy_PutsOutputOnClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "GEOCACHE";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual(vm.OutputText, clipboard.LastText);
    }

    [TestMethod]
    public void Share_SendsOutputToShareService()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.InputText = "GEOCACHE";

        vm.ShareOutputCommand.Execute(null);

        Assert.AreEqual(1, share.ShareTextCount);
        Assert.AreEqual(vm.OutputText, share.LastText);
    }

    [TestMethod]
    public void CopyAndShare_DisabledWhenNoOutput()
    {
        var vm = CreateViewModel();
        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
        Assert.IsFalse(vm.ShareOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var vm = CreateViewModel();
        vm.InputText = "GEOCACHE";
        Assert.IsTrue(vm.HasOutput);

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void Period_ChangesCiphertext()
    {
        var vm = CreateViewModel();
        vm.SquareModeIndex = 1;
        vm.ManualSquare = "BGWKZQPNDSIOAXEFCLUMTHYVR";
        vm.InputText = "FLEEATONCE";
        var whole = vm.OutputText;

        vm.Period = 5;
        Assert.AreNotEqual(whole, vm.OutputText);
    }

    [TestMethod]
    public void SkipMode_BuildsSquareWithoutTheSkippedLetter()
    {
        var vm = CreateViewModel();
        vm.FitModeIndex = 1; // skip
        vm.FitFromLetter = "Q";

        Assert.IsFalse(vm.SquareCells.Any(c => c.Letter == 'Q'));
        Assert.AreEqual(25, vm.SquareCells.Count);
    }

    [TestMethod]
    public void ResetSquare_RestoresKeywordDefaults()
    {
        var vm = CreateViewModel();
        vm.SquareModeIndex = 1;
        vm.ManualSquare = "ABC";
        vm.Keyword = "WHATEVER";

        vm.ResetSquareCommand.Execute(null);

        Assert.AreEqual(0, vm.SquareModeIndex);
        Assert.AreEqual(string.Empty, vm.Keyword);
        Assert.AreEqual(25, vm.SquareCells.Count);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void Input_WithOnlyNonLetters_WarnsNoLetters()
    {
        var vm = CreateViewModel();
        vm.InputText = "12345";

        Assert.IsFalse(vm.HasOutput);
        Assert.IsTrue(vm.HasWarning);
    }
}
