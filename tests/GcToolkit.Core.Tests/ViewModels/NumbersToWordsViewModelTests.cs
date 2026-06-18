using GcToolkit.Core.Catalog;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class NumbersToWordsViewModelTests
{
    private static NumbersToWordsViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        // Catalog must surface the tool so the base can resolve name/tooltip and record recents.
        var catalog = new SingleToolCatalog("NumbersToWords");
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["NumbersToWords_Name"] = "Numbers to words",
            ["NumbersToWords_Tooltip"] = "Spell numbers and parse words back.",
        });

        var vm = new NumbersToWordsViewModel(
            catalog, new FakeRecentsService(), new FakeFavoriteToolsService(), localizer, clipboard, share);
        vm.ViewCreated();
        return vm;
    }

    private static NumbersToWordsViewModel CreateViewModel()
        => CreateViewModel(out _, out _);

    [TestMethod]
    public void TypingDigits_ProducesWords()
    {
        var vm = CreateViewModel();
        vm.InputText = "354";

        Assert.AreEqual("THREE HUNDRED FIFTY-FOUR", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void TypingWords_ProducesNumber()
    {
        var vm = CreateViewModel();
        vm.InputText = "three hundred and fifty-four";

        Assert.AreEqual("354", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void InvalidInput_FlagsWarningAndMarkerOutput()
    {
        var vm = CreateViewModel();
        vm.InputText = "banana";

        Assert.IsTrue(vm.HasWarning);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void EmptyInput_ClearsOutput()
    {
        var vm = CreateViewModel();
        vm.InputText = "10";
        vm.InputText = string.Empty;

        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void OrdinalMode_SpellsOrdinals()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 1; // Ordinal
        vm.InputText = "23";

        Assert.AreEqual("TWENTY-THIRD", vm.OutputText);
    }

    [TestMethod]
    public void YearMode_ReadsAsYear()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 2; // Year
        vm.InputText = "1984";

        Assert.AreEqual("NINETEEN EIGHTY-FOUR", vm.OutputText);
    }

    [TestMethod]
    public void BritishAndStyle_InsertsAnd()
    {
        var vm = CreateViewModel();
        vm.UseBritishAnd = true;
        vm.InputText = "101";

        Assert.AreEqual("ONE HUNDRED AND ONE", vm.OutputText);
    }

    [TestMethod]
    public void CzechLanguage_SpellsCzech()
    {
        var vm = CreateViewModel();
        vm.UseCzech = true;
        vm.InputText = "354";

        Assert.AreEqual("TŘI STA PADESÁT ČTYŘI", vm.OutputText);
    }

    [TestMethod]
    public void Swap_CarriesResultIntoInputAndRoundTrips()
    {
        var vm = CreateViewModel();
        vm.InputText = "21";
        Assert.AreEqual("TWENTY-ONE", vm.OutputText);

        vm.SwapCommand.Execute(null);

        Assert.AreEqual("TWENTY-ONE", vm.InputText);
        Assert.AreEqual("21", vm.OutputText);
    }

    [TestMethod]
    public void Copy_CopiesOutputToClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "7";
        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("SEVEN", clipboard.LastText);
    }

    [TestMethod]
    public void Share_SharesOutput()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.InputText = "7";
        vm.ShareOutputCommand.Execute(null);

        Assert.AreEqual("SEVEN", share.LastText);
    }

    [TestMethod]
    public void Clear_EmptiesInput()
    {
        var vm = CreateViewModel();
        vm.InputText = "7";
        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
    }

    [TestMethod]
    public void SolverHelpers_PopulateForSingleWordResult()
    {
        var vm = CreateViewModel();
        vm.InputText = "354";

        Assert.AreEqual(21, vm.TotalLetterCount); // THREE(5)+HUNDRED(7)+FIFTY(5)+FOUR(4)
        Assert.AreEqual("THFF", vm.FirstLetters);
    }

    [TestMethod]
    public void BatchMode_ConvertsEachLine()
    {
        var vm = CreateViewModel();
        vm.IsBatchMode = true;
        vm.InputText = "1\n21\none hundred";

        // Each line on its own output line.
        var lines = vm.OutputText.Split('\n');
        Assert.AreEqual("ONE", lines[0]);
        Assert.AreEqual("TWENTY-ONE", lines[1]);
        Assert.AreEqual("100", lines[2]);
    }

    [TestMethod]
    public void ToggleFavorite_FlipsIsFavorite()
    {
        var vm = CreateViewModel();
        Assert.IsFalse(vm.IsFavorite);

        vm.ToggleFavoriteCommand.Execute(null);

        Assert.IsTrue(vm.IsFavorite);
    }

    /// <summary>Catalog returning a single real tool descriptor whose name key the localizer maps.</summary>
    private sealed class SingleToolCatalog(string id) : ICatalogService
    {
        private readonly ToolDescriptor _tool = new(id, $"{id}_Name", "Numbers", [], id, typeof(object), TooltipKey: $"{id}_Tooltip");

        public IReadOnlyList<Category> GetCategories() => [];

        public IReadOnlyList<ToolDescriptor> GetTools() => [_tool];

        public IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId) => [_tool];

        public IReadOnlyList<ToolDescriptor> Search(string query) => [_tool];
    }
}
