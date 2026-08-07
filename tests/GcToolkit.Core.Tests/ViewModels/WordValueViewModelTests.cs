using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.Text;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class WordValueViewModelTests
{
    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeShareService _share = new();

    private WordValueViewModel CreateViewModel() => new(
        new StubCatalogService("WordValue"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        new FakeStringLocalizer(),
        _clipboard,
        _share);

    [TestMethod]
    public void InputText_Word_ShowsCalculationAndReduction()
    {
        var vm = CreateViewModel();

        vm.InputText = "cache";

        Assert.AreEqual("3 + 1 + 3 + 8 + 5 = 20", vm.WordValueLine);
        Assert.AreEqual("20 = 2", vm.ReductionLine);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void ShowCalculation_Off_ShowsOnlyTheTotal()
    {
        var vm = CreateViewModel();
        vm.InputText = "cache";

        vm.ShowCalculation = false;

        Assert.AreEqual("20", vm.WordValueLine);
    }

    [TestMethod]
    public void SelectedMethod_Changed_RebuildsTheConversionTable()
    {
        var vm = CreateViewModel();

        vm.SelectedMethod = WordValueMethod.A26Z1;

        Assert.AreEqual(26, vm.Conversion.Count);
        Assert.AreEqual("a = 26", vm.Conversion[0].ToString());
    }

    [TestMethod]
    public void SelectedMethod_DiacriticScheme_DisablesDiacriticRemoval()
    {
        var vm = CreateViewModel();

        vm.SelectedMethod = WordValueMethod.German1;

        Assert.IsFalse(vm.CanRemoveDiacritics);
        Assert.AreEqual(30, vm.Conversion.Count);
    }

    [TestMethod]
    public void CountSeparateWords_Off_KeepsThePerWordListEmpty()
    {
        var vm = CreateViewModel();

        vm.InputText = "geo cache";

        Assert.AreEqual(0, vm.Words.Count);
        Assert.IsFalse(vm.ShowSeparateWords);
    }

    [TestMethod]
    public void CountSeparateWords_On_ListsEachWordWithItsReduction()
    {
        var vm = CreateViewModel();
        vm.InputText = "geo cache";

        vm.CountSeparateWords = true;

        Assert.IsTrue(vm.ShowSeparateWords);
        Assert.AreEqual(2, vm.Words.Count);
        Assert.AreEqual("geo", vm.Words[0].Word);
        Assert.AreEqual("cache", vm.Words[1].Word);
        Assert.AreEqual("3 + 1 + 3 + 8 + 5 = 20 = 2", vm.Words[1].Detail);
    }

    [TestMethod]
    public void NumberMode_Separate_MovesNumbersToTheirOwnTotal()
    {
        var vm = CreateViewModel();
        vm.InputText = "ab 42";

        vm.NumberModeIndex = 1;

        Assert.IsTrue(vm.ShowSeparateNumbers);
        Assert.AreEqual("42 = 42", vm.NumbersLine);
        Assert.AreEqual("42 = 6", vm.NumbersReductionLine);
        Assert.AreEqual("1 + 2 = 3", vm.WordValueLine);
    }

    [TestMethod]
    public void CountNumbers_Off_IgnoresDigitsEntirely()
    {
        var vm = CreateViewModel();
        vm.InputText = "ab 42";

        vm.CountNumbers = false;

        Assert.AreEqual("1 + 2 = 3", vm.WordValueLine);
        Assert.IsFalse(vm.ShowSeparateNumbers);
    }

    [TestMethod]
    public void Reset_RestoresEveryOptionAndClearsTheInput()
    {
        var vm = CreateViewModel();
        vm.InputText = "cache";
        vm.SelectedMethod = WordValueMethod.ScrabbleEnglish;
        vm.CountSeparateWords = true;
        vm.ShowCalculation = false;
        vm.NumberModeIndex = 1;

        vm.ResetCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.AreEqual(WordValueMethod.A1Z26, vm.SelectedMethod);
        Assert.IsFalse(vm.CountSeparateWords);
        Assert.IsTrue(vm.ShowCalculation);
        Assert.AreEqual(0, vm.NumberModeIndex);
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(0, vm.Words.Count);
    }

    [TestMethod]
    public void CopyOutput_WithResult_PutsTheSummaryOnTheClipboard()
    {
        var vm = CreateViewModel();
        vm.InputText = "cache";

        vm.CopyOutputCommand.Execute(null);

        Assert.IsTrue(_clipboard.LastText?.Contains("20 = 2", StringComparison.Ordinal) == true);
    }

    [TestMethod]
    public void CopyOutput_WithoutResult_IsDisabled()
    {
        var vm = CreateViewModel();

        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
    }
}
