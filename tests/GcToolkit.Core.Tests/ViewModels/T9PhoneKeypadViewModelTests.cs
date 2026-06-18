using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class T9PhoneKeypadViewModelTests
{
    private static T9PhoneKeypadViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share,
        out FakeRecentsService recents,
        out FakeFavoriteToolsService favorites)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();
        recents = new FakeRecentsService();
        favorites = new FakeFavoriteToolsService();
        var catalog = new StubCatalogService("T9PhoneKeypad");
        var localizer = new FakeStringLocalizer();
        return new T9PhoneKeypadViewModel(catalog, recents, favorites, localizer, clipboard, share);
    }

    private static T9PhoneKeypadViewModel CreateViewModel()
        => CreateViewModel(out _, out _, out _, out _);

    // ---- Multi-tap mode ----

    [TestMethod]
    public void Multitap_Encode_CodeProducesGroupedTaps()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeMultitap;
        vm.DirectionIndex = T9PhoneKeypadViewModel.DirectionEncode;
        vm.InputText = "CODE";
        Assert.AreEqual("222-666-3-33", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void Multitap_Decode_GroupedTapsProduceCode()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeMultitap;
        vm.DirectionIndex = T9PhoneKeypadViewModel.DirectionDecode;
        vm.InputText = "222-666-3-33";
        Assert.AreEqual("CODE", vm.OutputText);
    }

    // ---- Key+position mode ----

    [TestMethod]
    public void KeyPosition_Decode_SevenThreeIsR()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeKeyPosition;
        vm.DirectionIndex = T9PhoneKeypadViewModel.DirectionDecode;
        vm.InputText = "7-3";
        Assert.AreEqual("R", vm.OutputText);
    }

    // ---- T9 predictive mode ----

    [TestMethod]
    public void T9_Encode_GeocacheProducesSequence()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeT9;
        vm.DirectionIndex = T9PhoneKeypadViewModel.DirectionEncode;
        vm.InputText = "GEOCACHE";
        Assert.AreEqual("43622243", vm.OutputText);
        Assert.IsFalse(vm.ShowCandidates, "Candidates only appear when decoding.");
    }

    [TestMethod]
    public void T9_Decode_ShowsCandidatesIncludingGeocache()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeT9;
        vm.DirectionIndex = T9PhoneKeypadViewModel.DirectionDecode;
        vm.InputText = "43622243";

        Assert.IsTrue(vm.ShowCandidates);
        Assert.AreEqual(1, vm.Candidates.Count);
        CollectionAssert.Contains(vm.Candidates[0].Combinations.ToList(), "GEOCACHE");
    }

    [TestMethod]
    public void T9_Decode_MultipleWords_SplitsOnZero()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeT9;
        vm.DirectionIndex = T9PhoneKeypadViewModel.DirectionDecode;
        // "26330466" -> "2633" (code) + "466" via the 0 separator.
        vm.InputText = "2633046637";
        Assert.AreEqual(2, vm.Candidates.Count);
    }

    // ---- Direction swap carries the result ----

    [TestMethod]
    public void SwapDirection_CarriesPreviousResultIntoInput()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeMultitap;
        vm.InputText = "CODE"; // encode -> 222-666-3-33

        vm.DirectionIndex = T9PhoneKeypadViewModel.DirectionDecode;

        Assert.AreEqual("222-666-3-33", vm.InputText);
        Assert.AreEqual("CODE", vm.OutputText);
    }

    // ---- Empty / error states ----

    [TestMethod]
    public void EmptyInput_ProducesNoOutputOrWarning()
    {
        var vm = CreateViewModel();
        vm.InputText = "   ";
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void DecodeWithNoMappableInput_RaisesWarning()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeKeyPosition;
        vm.DirectionIndex = T9PhoneKeypadViewModel.DirectionDecode;
        vm.InputText = "1-1"; // key 1 has no letters -> nothing decodes
        Assert.IsFalse(vm.HasOutput);
        Assert.IsTrue(vm.HasWarning);
    }

    // ---- Copy / Share ----

    [TestMethod]
    public void Copy_CopiesOutputToClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _, out _, out _);
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeMultitap;
        vm.InputText = "A";
        vm.CopyOutputCommand.Execute(null);
        Assert.AreEqual(vm.OutputText, clipboard.LastText);
    }

    [TestMethod]
    public async Task Share_SharesOutput()
    {
        var vm = CreateViewModel(out _, out var share, out _, out _);
        vm.ModeIndex = T9PhoneKeypadViewModel.ModeMultitap;
        vm.InputText = "A";
        await vm.ShareOutputCommand.ExecuteAsync(null);
        Assert.AreEqual(vm.OutputText, share.LastText);
    }

    [TestMethod]
    public void Copy_DisabledWhenNoOutput()
    {
        var vm = CreateViewModel();
        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
        vm.InputText = "A";
        Assert.IsTrue(vm.CopyOutputCommand.CanExecute(null));
    }

    // ---- Clear ----

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var vm = CreateViewModel();
        vm.InputText = "CODE";
        vm.ClearCommand.Execute(null);
        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.OutputText);
    }

    // ---- On-screen keypad ----

    [TestMethod]
    public void PressKey_AppendsDigitToInput()
    {
        var vm = CreateViewModel();
        vm.PressKeyCommand.Execute("4");
        vm.PressKeyCommand.Execute("3");
        Assert.AreEqual("43", vm.InputText);
    }

    [TestMethod]
    public void Keypad_ExposesTenKeys()
    {
        var vm = CreateViewModel();
        Assert.AreEqual(10, vm.Keypad.Count);
    }

    // ---- Lifecycle: recents ----

    [TestMethod]
    public void OnNavigatedTo_RecordsOpenInRecents()
    {
        var vm = CreateViewModel(out _, out _, out var recents, out _);
        vm.OnNavigatedTo(null);
        CollectionAssert.Contains(recents.Opened.ToList(), "T9PhoneKeypad");
    }
}
