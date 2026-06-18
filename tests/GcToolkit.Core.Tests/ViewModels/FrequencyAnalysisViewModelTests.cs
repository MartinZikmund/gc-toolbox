using System.Linq;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class FrequencyAnalysisViewModelTests
{
    private static FrequencyAnalysisViewModel CreateViewModel(
        out FakeClipboardService clipboard, out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();
        return new FrequencyAnalysisViewModel(
            new StubCatalogService("FrequencyAnalysis"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            clipboard,
            share);
    }

    [TestMethod]
    public void InputText_WhenTyped_RecomputesTotalsLive()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "HELLO";

        Assert.AreEqual(5, vm.LetterCount);
        Assert.AreEqual(1, vm.WordCount);
        Assert.IsTrue(vm.HasContent);
        Assert.AreEqual(4, vm.CharacterBars.Count);
    }

    [TestMethod]
    public void CharacterBars_FillFactor_IsRelativeToMostCommon()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "AAAB"; // A=3, B=1
        var a = vm.CharacterBars.First(b => b.Display == "A");
        var b = vm.CharacterBars.First(x => x.Display == "B");

        Assert.AreEqual(1.0, a.FillFactor, 0.0001);
        Assert.AreEqual(1.0 / 3.0, b.FillFactor, 0.0001);
    }

    [TestMethod]
    public void CaseSensitive_Toggle_SplitsUpperAndLower()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "Aa";

        Assert.AreEqual(1, vm.CharacterBars.Count); // folded

        vm.CaseSensitive = true;
        Assert.AreEqual(2, vm.CharacterBars.Count); // split
    }

    [TestMethod]
    public void SortIndex_Alphabetical_OrdersBars()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "BCA";
        vm.SortIndex = 2; // alphabetical

        CollectionAssert.AreEqual(
            new[] { "A", "B", "C" },
            vm.CharacterBars.Select(b => b.Display).ToArray());
    }

    [TestMethod]
    public void Bigrams_PopulateFromInput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "ABAB";

        var ab = vm.Bigrams.First(b => b.Display == "AB");
        Assert.AreEqual(2, ab.Count);
    }

    [TestMethod]
    public void Cryptanalysis_ReadoutsPopulateForLetters()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG";

        Assert.IsFalse(string.IsNullOrEmpty(vm.IndexOfCoincidenceText));
        Assert.IsFalse(string.IsNullOrEmpty(vm.KeyLengthText));
        Assert.IsTrue(vm.HasMapping);
    }

    [TestMethod]
    public void Cryptanalysis_NoLetters_ClearsReadouts()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "123 456";

        Assert.AreEqual(string.Empty, vm.IndexOfCoincidenceText);
        Assert.IsFalse(vm.HasMapping);
    }

    [TestMethod]
    public void CopyCsv_CopiesCsvToClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "AAB";

        vm.CopyCsvCommand.Execute(null);

        StringAssert.StartsWith(clipboard.LastText, "Character,Count,Percentage");
    }

    [TestMethod]
    public void CopyTable_EmptyInput_CommandDisabled()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.IsFalse(vm.CopyTableCommand.CanExecute(null));

        vm.InputText = "A";
        Assert.IsTrue(vm.CopyTableCommand.CanExecute(null));
    }

    [TestMethod]
    public void LoadExample_FillsInputAndAnalyzes()
    {
        var vm = CreateViewModel(out _, out _);

        vm.LoadExampleCommand.Execute(null);

        Assert.IsTrue(vm.LetterCount > 0);
        Assert.IsTrue(vm.HasContent);
    }

    [TestMethod]
    public void Clear_ResetsInputAndContent()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HELLO";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasContent);
    }

    [TestMethod]
    public void ScopeIndex_LettersOnly_DropsDigitsAndSymbols()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "A1!A";
        vm.ScopeIndex = 1; // letters only

        Assert.AreEqual(1, vm.CharacterBars.Count);
        Assert.AreEqual("A", vm.CharacterBars[0].Display);
    }
}
