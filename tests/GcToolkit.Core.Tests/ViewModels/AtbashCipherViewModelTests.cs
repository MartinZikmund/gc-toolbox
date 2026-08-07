using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public sealed class AtbashCipherViewModelTests
{
    private static AtbashCipherViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();
        return new AtbashCipherViewModel(
            new StubCatalogService("AtbashCipher"),
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
        Assert.IsFalse(vm.UseOutputAsInputCommand.CanExecute(null));
    }

    [TestMethod]
    public void InputText_Typed_TransformsLiveAndEnablesOutputActions()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "Hello, World!";

        Assert.AreEqual("Svool, Dliow!", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsTrue(vm.CopyOutputCommand.CanExecute(null));
        Assert.IsTrue(vm.ShareOutputCommand.CanExecute(null));
        Assert.IsTrue(vm.UseOutputAsInputCommand.CanExecute(null));
    }

    [TestMethod]
    public void InputText_Multiline_TransformsEachLineIndependently()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "ABC\nXYZ";

        Assert.AreEqual("ZYX\nCBA", vm.OutputText);
    }

    [TestMethod]
    public void UseOutputAsInput_RoundTrips_BackToTheOriginalText()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "N 49 13.456";
        var encoded = vm.OutputText;

        vm.UseOutputAsInputCommand.Execute(null);

        Assert.AreEqual(encoded, vm.InputText);
        // Atbash is self-inverse, so feeding the result back restores the original.
        Assert.AreEqual("N 49 13.456", vm.OutputText);
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
    public void CopyOutput_CopiesTheTransformedText()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "ABC";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("ZYX", clipboard.LastText);
        Assert.AreEqual(1, clipboard.SetTextCallCount);
    }

    [TestMethod]
    public async Task ShareOutput_SharesTheTransformedText_UnderTheToolName()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.ViewCreated();
        vm.InputText = "ABC";

        await vm.ShareOutputCommand.ExecuteAsync(null);

        Assert.AreEqual("ZYX", share.LastText);
        Assert.AreEqual(vm.ToolName, share.LastTitle);
        Assert.AreEqual(1, share.ShareTextCallCount);
    }

    [TestMethod]
    public void Key_ExposesTheFixedSubstitutionRows()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", vm.KeyPlain);
        Assert.AreEqual("ZYXWVUTSRQPONMLKJIHGFEDCBA", vm.KeyCipher);
    }
}
