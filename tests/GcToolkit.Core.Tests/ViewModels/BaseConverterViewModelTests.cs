using GcToolkit.Core.Numbers;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public sealed class BaseConverterViewModelTests
{
    private static BaseConverterViewModel CreateViewModel(out FakeClipboardService clipboard)
    {
        clipboard = new FakeClipboardService();
        return new BaseConverterViewModel(
            new StubCatalogService("BaseConverter"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            clipboard,
            new FakeShareService());
    }

    [TestMethod]
    public void Constructor_StartsEmpty_WithOutputActionsDisabled()
    {
        var vm = CreateViewModel(out _);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.TargetOutput);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
        Assert.AreEqual(0, vm.CommonResults.Count);
        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void Bases_CoverEveryRadixFromTwoToSixtyTwo()
    {
        var vm = CreateViewModel(out _);

        Assert.AreEqual(61, vm.Bases.Count);
        Assert.AreEqual(NumberBaseConverter.MinBase, vm.Bases[0].Radix);
        Assert.AreEqual(NumberBaseConverter.MaxBase, vm.Bases[^1].Radix);
    }

    [TestMethod]
    public void InputText_SingleValue_ConvertsLiveToCommonAndTargetBases()
    {
        var vm = CreateViewModel(out _);

        vm.InputText = "255";

        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
        Assert.IsFalse(vm.IsBatch);
        Assert.AreEqual("FF", vm.TargetOutput);   // default target base 16
        CollectionAssert.AreEqual(
            new[] { "11111111", "377", "255", "FF" },
            vm.CommonResults.Select(r => r.Value).ToArray());
    }

    [TestMethod]
    public void InputText_InvalidDigitForBase_ShowsErrorAndClearsOutput()
    {
        var vm = CreateViewModel(out _);
        vm.FromBase = 2;

        vm.InputText = "1012";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(string.Empty, vm.TargetOutput);
        Assert.AreEqual(0, vm.CommonResults.Count);
    }

    [TestMethod]
    public void InputText_Cleared_ResetsEveryOutput()
    {
        var vm = CreateViewModel(out _);
        vm.InputText = "255";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
        Assert.AreEqual(0, vm.CommonResults.Count);
    }

    [TestMethod]
    public void InputText_SeveralValues_ConvertsAsABatchAndFlagsUnknownTokens()
    {
        var vm = CreateViewModel(out _);
        vm.FromBase = 16;
        vm.ToBase = 10;

        vm.InputText = "FF ZZ 10";

        Assert.IsTrue(vm.IsBatch);
        Assert.IsFalse(vm.IsSingleValue);
        Assert.IsTrue(vm.HasOutput);
        Assert.AreEqual(3, vm.BatchResults.Count);
        Assert.AreEqual("255", vm.BatchResults[0].Value);
        Assert.IsFalse(vm.BatchResults[1].IsValid);
        Assert.AreEqual("16", vm.BatchResults[2].Value);
    }

    [TestMethod]
    public void ShowAllBases_Enabled_RendersEveryBaseTwoToSixtyTwo()
    {
        var vm = CreateViewModel(out _);
        vm.InputText = "255";

        vm.ShowAllBases = true;

        Assert.AreEqual(61, vm.AllBaseResults.Count);
        Assert.AreEqual("11111111", vm.AllBaseResults[0].Value);
        Assert.AreEqual("47", vm.AllBaseResults[^1].Value);

        vm.ShowAllBases = false;

        Assert.AreEqual(0, vm.AllBaseResults.Count);
    }

    [TestMethod]
    public void SwapBases_SwapsTheBasesAndFeedsTheResultBackIn()
    {
        var vm = CreateViewModel(out _);
        vm.FromBase = 10;
        vm.ToBase = 2;
        vm.InputText = "5";
        Assert.AreEqual("101", vm.TargetOutput);

        vm.SwapBasesCommand.Execute(null);

        Assert.AreEqual(2, vm.FromBase);
        Assert.AreEqual(10, vm.ToBase);
        Assert.AreEqual("101", vm.InputText);
        Assert.AreEqual("5", vm.TargetOutput);   // one tap round-trips
    }

    [TestMethod]
    public void CaseSensitive_Enabled_RejectsLowerCaseDigitsBelowBase37()
    {
        var vm = CreateViewModel(out _);
        vm.FromBase = 16;
        vm.InputText = "ff";
        Assert.IsTrue(vm.HasOutput);

        vm.CaseSensitive = true;

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void IsCaseToggleEnabled_IsOffAboveBase36()
    {
        var vm = CreateViewModel(out _);

        Assert.IsTrue(vm.IsCaseToggleEnabled);

        vm.FromBase = 62;

        Assert.IsFalse(vm.IsCaseToggleEnabled);
    }

    [TestMethod]
    public void FromBase_Base62_ReadsBothLetterCasesAsDistinctDigits()
    {
        var vm = CreateViewModel(out _);
        vm.FromBase = 62;
        vm.ToBase = 10;

        vm.InputText = "aA";

        Assert.IsFalse(vm.HasError);
        Assert.AreEqual(((10 * 62) + 36).ToString(), vm.TargetOutput);
    }

    [TestMethod]
    public void FromBaseIndex_MapsOntoTheDropdownOrder()
    {
        var vm = CreateViewModel(out _);

        vm.FromBaseIndex = 0;
        Assert.AreEqual(2, vm.FromBase);

        vm.ToBaseIndex = 60;
        Assert.AreEqual(62, vm.ToBase);

        vm.FromBase = 16;
        Assert.AreEqual(14, vm.FromBaseIndex);
    }

    [TestMethod]
    public void DigitReference_FollowsTheSourceBase()
    {
        var vm = CreateViewModel(out _);
        vm.FromBase = 2;

        Assert.AreEqual("01", vm.DigitReference);

        vm.FromBase = 16;

        Assert.AreEqual("0123456789ABCDEF", vm.DigitReference);
    }

    [TestMethod]
    public void UseCustomAlphabet_SeedsTheAlphabetsFromTheCurrentBases()
    {
        var vm = CreateViewModel(out _);
        vm.FromBase = 2;
        vm.ToBase = 16;

        vm.UseCustomAlphabet = true;

        Assert.IsFalse(vm.UseStandardBases);
        Assert.AreEqual("01", vm.SourceAlphabet);
        Assert.AreEqual("0123456789ABCDEF", vm.TargetAlphabet);
    }

    [TestMethod]
    public void UseCustomAlphabet_ConvertsThroughTheUserGlyphSets()
    {
        var vm = CreateViewModel(out _);
        vm.UseCustomAlphabet = true;
        vm.SourceAlphabet = "0123456789ABCDEFGHJKMNPQRTVWXYZ";   // GC base 31
        vm.TargetAlphabet = "0123456789";

        vm.InputText = "16XYD";

        Assert.IsFalse(vm.HasError);
        Assert.IsTrue(vm.HasOutput);
        Assert.AreEqual("1130087", vm.TargetOutput);
    }

    [TestMethod]
    public void UseCustomAlphabet_RepeatedGlyph_ShowsTheAlphabetError()
    {
        var vm = CreateViewModel(out _);
        vm.UseCustomAlphabet = true;
        vm.InputText = "01";

        vm.SourceAlphabet = "0011";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void SwapBases_InCustomAlphabetMode_SwapsTheAlphabetsToo()
    {
        var vm = CreateViewModel(out _);
        vm.UseCustomAlphabet = true;
        vm.SourceAlphabet = "01";
        vm.TargetAlphabet = "AB";
        vm.InputText = "101";
        Assert.AreEqual("BAB", vm.TargetOutput);

        vm.SwapBasesCommand.Execute(null);

        Assert.AreEqual("AB", vm.SourceAlphabet);
        Assert.AreEqual("01", vm.TargetAlphabet);
        Assert.AreEqual("BAB", vm.InputText);
        Assert.AreEqual("101", vm.TargetOutput);
    }

    [TestMethod]
    public void CopyOutput_CopiesEveryShownLine()
    {
        var vm = CreateViewModel(out var clipboard);
        vm.InputText = "255";

        vm.CopyOutputCommand.Execute(null);

        StringAssert.Contains(clipboard.LastText!, "11111111");
        StringAssert.Contains(clipboard.LastText!, "FF");
    }

    [TestMethod]
    public void CopyRow_CopiesJustThatValue()
    {
        var vm = CreateViewModel(out var clipboard);
        vm.InputText = "255";

        vm.CommonResults[0].CopyCommand.Execute(null);

        Assert.AreEqual("11111111", clipboard.LastText!);
    }
}
