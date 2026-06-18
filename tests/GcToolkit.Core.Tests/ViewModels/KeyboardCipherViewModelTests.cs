using GcToolkit.Core.Recents;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class KeyboardCipherViewModelTests
{
    private const string ToolId = "KeyboardCipher";

    private static KeyboardCipherViewModel Create(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        var catalog = new StubCatalogService(ToolId);
        var preferences = new InMemoryPreferences();
        var favorites = new FavoriteToolsService(preferences, catalog);
        var recents = new RecentsService(preferences, catalog);
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"Name_{ToolId}"] = "Keyboard Cipher",
            ["KeyboardInvalidLayoutNotice"] = "The custom layout must contain each of the 26 letters exactly once.",
        });

        var vm = new KeyboardCipherViewModel(catalog, recents, favorites, localizer, clipboard, share);
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void InputText_EncryptDefault_TransformsLiveWithQwerty()
    {
        var vm = Create(out _, out _);

        vm.InputText = "ABC";

        Assert.AreEqual("QWE", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void DirectionIndex_Decrypt_InvertsTheMapping()
    {
        var vm = Create(out _, out _);

        vm.DirectionIndex = 1; // Decrypt
        vm.InputText = "QWE";

        Assert.AreEqual("ABC", vm.OutputText);
    }

    [TestMethod]
    public void DirectionIndex_Toggle_CarriesOutputIntoInput()
    {
        var vm = Create(out _, out _);
        vm.InputText = "HELLO"; // -> ITSSG

        vm.DirectionIndex = 1; // switching to decrypt carries ITSSG into input

        Assert.AreEqual("ITSSG", vm.InputText);
        Assert.AreEqual("HELLO", vm.OutputText);
    }

    [TestMethod]
    public void Swap_FlipsDirectionAndRoundTrips()
    {
        var vm = Create(out _, out _);
        vm.InputText = "HELLO"; // encrypt -> ITSSG

        vm.SwapCommand.Execute(null);

        Assert.AreEqual(1, vm.DirectionIndex);
        Assert.AreEqual("ITSSG", vm.InputText);
        Assert.AreEqual("HELLO", vm.OutputText);
    }

    [TestMethod]
    public void InputText_MultiLine_TransformsEachLineIndependently()
    {
        var vm = Create(out _, out _);

        vm.InputText = "ABC\nHELLO";

        Assert.AreEqual("QWE\nITSSG", vm.OutputText);
    }

    [TestMethod]
    public void LayoutIndex_Custom_UsesCustomKeyOrder()
    {
        var vm = Create(out _, out _);
        vm.LayoutIndex = 5; // Custom
        vm.CustomLayout = "QWERTYUIOPASDFGHJKLZXCVBNM"; // same as QWERTY

        vm.InputText = "ABC";

        Assert.IsTrue(vm.IsCustomLayout);
        Assert.AreEqual("QWE", vm.OutputText);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void CustomLayout_Invalid_RaisesWarningAndClearsOutput()
    {
        var vm = Create(out _, out _);
        vm.LayoutIndex = 5; // Custom
        vm.InputText = "ABC";

        vm.CustomLayout = "NOTAVALIDPERMUTATION1234567";

        Assert.IsTrue(vm.HasWarning);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void Mapping_Qwerty_HasTwentySixRows()
    {
        var vm = Create(out _, out _);

        Assert.AreEqual(26, vm.Mapping.Count);
        Assert.AreEqual("A", vm.Mapping[0].Letter);
        Assert.AreEqual("Q", vm.Mapping[0].Mapped);
    }

    [TestMethod]
    public void CopyOutput_CopiesResultToClipboard()
    {
        var vm = Create(out var clipboard, out _);
        vm.InputText = "ABC";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("QWE", clipboard.LastText);
    }

    [TestMethod]
    public async Task ShareOutput_SharesResult()
    {
        var vm = Create(out _, out var share);
        vm.InputText = "ABC";

        await vm.ShareOutputCommand.ExecuteAsync(null);

        Assert.AreEqual("QWE", share.LastText);
    }

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var vm = Create(out _, out _);
        vm.InputText = "ABC";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void CopyAndShare_DisabledWhenNoOutput()
    {
        var vm = Create(out _, out _);

        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
        Assert.IsFalse(vm.ShareOutputCommand.CanExecute(null));
    }
}
