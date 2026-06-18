using System.Collections.Generic;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Search;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class ScrabbleScorerViewModelTests
{
    private static ScrabbleScorerViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        CatalogService catalog = new(
            [new StubCategoryContributor(new Category("Text", "Category_Text", 0, "Text"))],
            [new StubToolContributor(new ToolDescriptor("ScrabbleScorer", "ScrabbleScorer_Name", "Text", new string[0], "ScrabbleScorer", typeof(object), false, "ScrabbleScorer_Tooltip"))],
            new ToolMatcher(),
            new FakeStringLocalizer());

        RecentsService recents = new(new InMemoryPreferences(), catalog);
        FavoriteToolsService favorites = new(new InMemoryPreferences(), catalog);
        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            { "ScrabbleScorer_Name", "Scrabble scorer" },
            { "ScrabbleScorer_Tooltip", "Score words." },
            { "ScrabbleTotalLabel", "Total:" },
        });

        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        ScrabbleScorerViewModel vm = new(catalog, recents, favorites, localizer, clipboard, share);
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void InputText_QuizEnglish_ComputesTotalAndDigitalRoot()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "QUIZ";

        Assert.AreEqual(22, vm.GrandTotal);
        Assert.AreEqual("4", vm.DigitalRootText);
        Assert.IsTrue(vm.HasOutput);
        Assert.AreEqual(1, vm.WordResults.Count);
        Assert.AreEqual("Q=10 + U=1 + I=1 + Z=10 = 22", vm.WordResults[0].Breakdown);
    }

    [TestMethod]
    public void InputText_MultipleWords_ProducesOneRowPerWord()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "QUIZ HELLO";

        Assert.AreEqual(2, vm.WordResults.Count);
        Assert.AreEqual(30, vm.GrandTotal);
    }

    [TestMethod]
    public void InputText_Cleared_ResetsTotalsAndOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "QUIZ";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(0, vm.GrandTotal);
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(0, vm.WordResults.Count);
    }

    [TestMethod]
    public void CopyTotal_AfterScoring_CopiesGrandTotal()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "QUIZ";

        vm.CopyTotalCommand.Execute(null);

        Assert.AreEqual("22", clipboard.LastText);
    }

    [TestMethod]
    public void CopyWords_AfterScoring_CopiesPerWordLines()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "QUIZ HELLO";

        vm.CopyWordsCommand.Execute(null);

        StringAssert.Contains(clipboard.LastText, "QUIZ = 22");
        StringAssert.Contains(clipboard.LastText, "HELLO = 8");
        StringAssert.Contains(clipboard.LastText, "Total: 30");
    }

    [TestMethod]
    public void Share_AfterScoring_InvokesShareService()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.InputText = "QUIZ";

        vm.ShareCommand.Execute(null);

        Assert.AreEqual(1, share.ShareTextCount);
        StringAssert.Contains(share.LastText, "22");
    }

    [TestMethod]
    public void ReferenceCells_DefaultEnglish_Has26EntriesStartingWithA1()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.AreEqual(26, vm.ReferenceCells.Count);
        Assert.AreEqual("A", vm.ReferenceCells[0].Letter);
        Assert.AreEqual(1, vm.ReferenceCells[0].Value);
        Assert.AreEqual(10, vm.ReferenceCells['Q' - 'A'].Value);
    }

    [TestMethod]
    public void SelectedSystem_SwitchToA1Z26_RescoresAndUpdatesReference()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "ABZ";

        // Index 6 is A1Z26 in the picker order.
        vm.SelectedSystemIndex = 6;

        Assert.AreEqual(1 + 2 + 26, vm.GrandTotal);
        Assert.AreEqual(26, vm.ReferenceCells['Z' - 'A'].Value);
    }

    [TestMethod]
    public void SelectedSystem_Custom_RevealsEditorAndUsesTable()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "ABC";

        // Last index is Custom.
        vm.SelectedSystemIndex = vm.Systems.Count - 1;
        vm.CustomTableText = string.Join(",", Enumerable.Repeat(2, 26));

        Assert.IsTrue(vm.IsCustomSystem);
        Assert.AreEqual(6, vm.GrandTotal);
    }

    [TestMethod]
    public void CustomTable_WrongLength_FlagsWarningAndNoOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SelectedSystemIndex = vm.Systems.Count - 1;
        vm.InputText = "AB";

        vm.CustomTableText = "1,2,3"; // too few

        Assert.IsTrue(vm.HasWarning);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void UnknownLetter_FlagsWarning()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "Ω";

        Assert.IsTrue(vm.HasWarning);
    }
}
