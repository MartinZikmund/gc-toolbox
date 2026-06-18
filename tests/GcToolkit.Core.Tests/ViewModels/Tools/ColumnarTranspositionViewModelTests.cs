using System.Collections.Generic;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels.Tools;

[TestClass]
public class ColumnarTranspositionViewModelTests
{
    private const string Id = "ColumnarTransposition";

    private static ColumnarTranspositionViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        StubCatalogService catalog = new(Id);
        InMemoryPreferences prefs = new();
        RecentsService recents = new(prefs, catalog);
        FavoriteToolsService favorites = new(prefs, catalog);
        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            { $"Name_{Id}", "Columnar transposition" },
            { "ColumnarKeyEmptyError", "Enter a key." },
        });

        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        ColumnarTranspositionViewModel vm = new(catalog, recents, favorites, localizer, clipboard, share);
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void Encrypt_WikipediaVector_ProducesCiphertext()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "ZEBRAS";
        vm.InputText = "WEAREDISCOVEREDFLEEATONCE";

        Assert.AreEqual("EVLNACDTESEAROFODEECWIREE", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void Decrypt_WikipediaVector_RecoversPlaintext()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 1; // decrypt
        vm.KeyText = "ZEBRAS";
        vm.InputText = "EVLNACDTESEAROFODEECWIREE";

        Assert.AreEqual("WEAREDISCOVEREDFLEEATONCE", vm.OutputText);
    }

    [TestMethod]
    public void NumericKey_EncryptsLikeKeyword()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "6 3 2 4 1 5"; // == ZEBRAS
        vm.InputText = "WEAREDISCOVEREDFLEEATONCE";

        Assert.AreEqual("EVLNACDTESEAROFODEECWIREE", vm.OutputText);
    }

    [TestMethod]
    public void SwapDirection_CarriesResultAndFlips()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "ZEBRAS";
        vm.InputText = "WEAREDISCOVEREDFLEEATONCE";
        var cipher = vm.OutputText;

        vm.SwapDirectionCommand.Execute(null);

        Assert.AreEqual(1, vm.DirectionIndex);
        Assert.AreEqual(cipher, vm.InputText);
        Assert.AreEqual("WEAREDISCOVEREDFLEEATONCE", vm.OutputText);
    }

    [TestMethod]
    public void InvalidNumericKey_ShowsError()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "1 3 4"; // not a permutation
        vm.InputText = "HELLO";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(string.Empty, vm.OutputText);
    }

    [TestMethod]
    public void EmptyKeyWithText_ShowsError()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HELLO";

        Assert.IsTrue(vm.HasError);
    }

    [TestMethod]
    public void GridPreview_PopulatesHeadersAndCells()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "ZEBRAS";
        vm.InputText = "ABCDEF";

        Assert.IsTrue(vm.HasGrid);
        Assert.AreEqual(6, vm.ColumnCount);
        Assert.AreEqual(6, vm.Headers.Count);
        Assert.AreEqual("Z", vm.Headers[0].Letter);
        Assert.AreEqual(6, vm.Headers[0].Order);
        Assert.AreEqual(6, vm.GridCells.Count);
        Assert.AreEqual("A", vm.GridCells[0].Glyph);
    }

    [TestMethod]
    public void Padding_AddsPadCharsOnEncrypt()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "1 2 3";
        vm.PadToRectangle = true;
        vm.PadCharText = "X";
        vm.InputText = "ABCDEFG"; // 7 chars -> pad to 9

        Assert.AreEqual(9, vm.OutputText.Length);
    }

    [TestMethod]
    public void DoubleTransposition_RoundTrips()
    {
        var vm = CreateViewModel(out _, out _);
        vm.UseDoubleTransposition = true;
        vm.SecondKeyText = "GERMAN";
        vm.KeyText = "ZEBRAS";
        vm.InputText = "WEAREDISCOVEREDFLEEATONCE";
        var cipher = vm.OutputText;

        // Decrypt back with the same options.
        vm.DirectionIndex = 1;
        vm.InputText = cipher;

        Assert.AreEqual("WEAREDISCOVEREDFLEEATONCE", vm.OutputText);
    }

    [TestMethod]
    public void Copy_SetsClipboardToOutput()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.KeyText = "ZEBRAS";
        vm.InputText = "WEAREDISCOVEREDFLEEATONCE";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual(vm.OutputText, clipboard.LastText);
    }

    [TestMethod]
    public async Task Share_SendsOutputToShareService()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.KeyText = "ZEBRAS";
        vm.InputText = "WEAREDISCOVEREDFLEEATONCE";

        await vm.ShareOutputCommand.ExecuteAsync(null);

        Assert.AreEqual(vm.OutputText, share.LastText);
    }

    [TestMethod]
    public void Clear_ResetsInputAndOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.KeyText = "ZEBRAS";
        vm.InputText = "WEAREDISCOVEREDFLEEATONCE";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
    }
}
