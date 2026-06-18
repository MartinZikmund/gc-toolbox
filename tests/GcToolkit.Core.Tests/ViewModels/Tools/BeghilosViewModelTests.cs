using System.Collections.Generic;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Search;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels.Tools;

[TestClass]
public class BeghilosViewModelTests
{
    private static BeghilosViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        CatalogService catalog = new(
            [new StubCategoryContributor(new Category("Alphabets", "Alphabets_Name", 0, "Alphabets"))],
            [new StubToolContributor(new ToolDescriptor("Beghilos", "Beghilos_Name", "Alphabets", [], "Beghilos", typeof(object), false, "Beghilos_Tooltip"))],
            new ToolMatcher(),
            new FakeStringLocalizer());

        RecentsService recents = new(new InMemoryPreferences(), catalog);
        FavoriteToolsService favorites = new(new InMemoryPreferences(), catalog);
        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            { "Beghilos_Name", "Calculator Spelling" },
            { "Beghilos_Tooltip", "Spell with a calculator." },
            { "BeghilosResolvedEncode", "Word to number" },
            { "BeghilosResolvedDecode", "Number to word" },
            { "BeghilosUnsupportedNotice", "{0} character(s) cannot be converted: {1}" },
        });

        clipboard = new FakeClipboardService();
        share = new FakeShareService();
        return new BeghilosViewModel(catalog, recents, favorites, localizer, clipboard, share);
    }

    [TestMethod]
    public void Typing_Word_AutoEncodesToNumber()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HELLO";

        Assert.AreEqual("07734", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void Typing_Number_AutoDecodesToWord()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "0.7734";

        Assert.AreEqual("HELLO", vm.OutputText);
        Assert.AreEqual("Number to word", vm.ResolvedDirectionLabel);
    }

    [TestMethod]
    public void DirectionIndex_ForceEncode_OverridesAutoDetection()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 1; // force encode
        vm.InputText = "07734"; // digits would auto-decode, but encode treats them as unconvertible

        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsTrue(vm.HasWarning);
    }

    [TestMethod]
    public void DirectionIndex_ForceDecode_DecodesEvenWithLetters()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 2; // force decode
        vm.InputText = "07734abc"; // letters reported, digits decode

        Assert.AreEqual("HELLO", vm.OutputText);
        Assert.IsTrue(vm.HasWarning);
    }

    [TestMethod]
    public void ProfileIndex_Extended_DecodesNineAsG()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 2; // decode
        vm.ProfileIndex = 1;   // extended
        vm.InputText = "9";

        Assert.AreEqual("G", vm.OutputText);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void ProfileIndex_Strict_FlagsNineAsUnsupported()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 2; // decode
        vm.ProfileIndex = 0;   // strict
        vm.InputText = "9";

        Assert.IsTrue(vm.HasWarning);
        StringAssert.Contains(vm.WarningMessage, "9");
    }

    [TestMethod]
    public void UnsupportedInput_RaisesWarningWithCountAndChars()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "CAT"; // none convertible

        Assert.IsTrue(vm.HasWarning);
        StringAssert.Contains(vm.WarningMessage, "C");
    }

    [TestMethod]
    public void Glyphs_PopulatedForResultDigits()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HELLO"; // encodes to 07734

        Assert.IsTrue(vm.HasGlyphs);
        Assert.AreEqual(5, vm.Glyphs.Count);
        Assert.AreEqual(5, vm.FlippedGlyphs.Count);
    }

    [TestMethod]
    public void EmptyInput_ClearsEverything()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HELLO";
        vm.InputText = "   ";

        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasGlyphs);
        Assert.IsFalse(vm.HasWarning);
        Assert.AreEqual(0, vm.Glyphs.Count);
    }

    [TestMethod]
    public void Clear_ResetsInput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HELLO";
        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(string.Empty, vm.OutputText);
    }

    [TestMethod]
    public void CopyOutput_PutsResultOnClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "HELLO";
        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("07734", clipboard.LastText);
    }

    [TestMethod]
    public void CopyOutput_CannotExecuteWithoutOutput()
    {
        var vm = CreateViewModel(out _, out _);
        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));

        vm.InputText = "HELLO";
        Assert.IsTrue(vm.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ShareOutput_SharesResultText()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.ViewCreated(); // resolve ToolName
        vm.InputText = "HELLO";

        await vm.ShareOutputCommand.ExecuteAsync(null);

        Assert.AreEqual("07734", share.LastText);
        Assert.AreEqual("Calculator Spelling", share.LastTitle);
    }
}
