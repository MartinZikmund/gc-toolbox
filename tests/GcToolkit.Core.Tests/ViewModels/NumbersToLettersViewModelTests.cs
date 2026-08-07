using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.Text;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public sealed class NumbersToLettersViewModelTests
{
    private static NumbersToLettersViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();
        return new NumbersToLettersViewModel(
            new StubCatalogService("NumbersToLetters"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            clipboard,
            share);
    }

    [TestMethod]
    public void Constructor_StartsEmpty_WithOutputActionsDisabled()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
        Assert.IsFalse(vm.ShareOutputCommand.CanExecute(null));
        Assert.IsFalse(vm.SwapCommand.CanExecute(null));
        Assert.AreEqual(8, vm.MethodLabels.Count);
        Assert.AreEqual(26, vm.ConversionTable.Count);
    }

    [TestMethod]
    public void InputText_Typed_EncodesLiveAndEnablesOutputActions()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "HI";

        Assert.AreEqual("8 9", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsTrue(vm.CopyOutputCommand.CanExecute(null));
        Assert.IsTrue(vm.SwapCommand.CanExecute(null));
    }

    [TestMethod]
    public void DirectionIndex_SwitchedToDecode_CarriesTheResultIntoTheInput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HI";

        vm.DirectionIndex = 1;

        // The previous numbers become the input and decode straight back.
        Assert.AreEqual("8 9", vm.InputText);
        Assert.AreEqual("HI", vm.OutputText);
        Assert.IsFalse(vm.IsLettersToNumbers);
    }

    [TestMethod]
    public void SwapCommand_FlipsDirectionAndRoundTrips()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "GEO";

        vm.SwapCommand.Execute(null);

        Assert.AreEqual(1, vm.DirectionIndex);
        Assert.AreEqual("7 5 15", vm.InputText);
        Assert.AreEqual("GEO", vm.OutputText);

        vm.SwapCommand.Execute(null);

        Assert.AreEqual(0, vm.DirectionIndex);
        Assert.AreEqual("GEO", vm.InputText);
        Assert.AreEqual("7 5 15", vm.OutputText);
    }

    [TestMethod]
    public void MethodIndex_Changed_ReflowsTheConversionTableAndOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "A";

        // A=0 … Z=25 (the second method in declaration order).
        vm.MethodIndex = (int)AlphabetMethod.A0Z25;

        Assert.AreEqual("0", vm.OutputText);
        Assert.AreEqual(0, vm.ConversionTable[0].Value);

        // German extends the alphabet to 30 entries.
        vm.MethodIndex = (int)AlphabetMethod.A1Z26German;

        Assert.AreEqual(30, vm.ConversionTable.Count);
        Assert.AreEqual("1", vm.OutputText);
    }

    [TestMethod]
    public void MethodIndex_TransientMinusOne_IsIgnored()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "A";

        vm.MethodIndex = -1;

        Assert.AreEqual("1", vm.OutputText);
        Assert.AreEqual(26, vm.ConversionTable.Count);
    }

    [TestMethod]
    public void Separator_Changed_RegroupsTheOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HI";

        vm.Separator = "-";

        Assert.AreEqual("8-9", vm.OutputText);
    }

    [TestMethod]
    public void Decode_UnknownNumber_UsesTheReplacementText()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 1;
        vm.InputText = "1 99 3";

        Assert.AreEqual("A?C", vm.OutputText);

        vm.KeepOriginalUnknown = true;

        Assert.AreEqual("A99C", vm.OutputText);
        Assert.IsFalse(vm.CanEditReplacement);
    }

    [TestMethod]
    public void Decode_WrapModulo_MapsOutOfRangeValuesBackIntoRange()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 1;
        vm.InputText = "27";

        vm.WrapModulo = true;

        Assert.AreEqual("A", vm.OutputText);
    }

    [TestMethod]
    public void Clear_ResetsInputAndOutput_AndDisablesOutputActions()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "GEOCACHE";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void CopyOutput_CopiesTheConvertedText()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "HI";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("8 9", clipboard.LastText);
        Assert.AreEqual(1, clipboard.SetTextCallCount);
    }

    [TestMethod]
    public async Task ShareOutput_SharesTheConvertedText_UnderTheToolName()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.ViewCreated();
        vm.InputText = "HI";

        await vm.ShareOutputCommand.ExecuteAsync(null);

        Assert.AreEqual("8 9", share.LastText);
        Assert.AreEqual(vm.ToolName, share.LastTitle);
    }
}
