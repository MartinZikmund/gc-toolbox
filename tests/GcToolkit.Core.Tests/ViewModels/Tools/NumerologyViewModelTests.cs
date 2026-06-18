using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels.Tools;

[TestClass]
public class NumerologyViewModelTests
{
    private static NumerologyViewModel Create(
        FakeClipboardService? clipboard = null,
        FakeShareService? share = null)
    {
        var catalog = new StubCatalogService("Numerology");
        var preferences = new InMemoryPreferences();
        var recents = new RecentsService(preferences, catalog);
        var favorites = new FavoriteToolsService(preferences, catalog);
        var localizer = new FakeStringLocalizer();
        return new NumerologyViewModel(
            catalog,
            recents,
            favorites,
            localizer,
            clipboard ?? new FakeClipboardService(),
            share ?? new FakeShareService());
    }

    [TestMethod]
    public void Systems_Initialized_HasFourSystems()
    {
        var vm = Create();

        Assert.AreEqual(4, vm.Systems.Count);
    }

    [TestMethod]
    public void InputText_Hello_UpdatesAllSystemSummaries()
    {
        var vm = Create();

        vm.InputText = "HELLO";

        // Systems are in codec order: Pythagorean1To9, Pythagorean1To0, Chaldean, Simple.
        Assert.AreEqual(25, vm.Systems[0].Total);
        Assert.AreEqual(7, vm.Systems[0].Reduced);
        Assert.AreEqual(23, vm.Systems[2].Total); // Chaldean
        Assert.AreEqual(5, vm.Systems[2].Reduced);
    }

    [TestMethod]
    public void InputText_Hello_FocusedSystemDrivesBreakdown()
    {
        var vm = Create();

        vm.InputText = "HELLO";

        Assert.IsTrue(vm.HasResult);
        Assert.AreEqual(25, vm.FocusedTotal);
        Assert.AreEqual(7, vm.FocusedReduced);
        Assert.AreEqual(5, vm.Breakdown.Count);
        Assert.AreEqual("H", vm.Breakdown[0].Letter);
        Assert.AreEqual(8, vm.Breakdown[0].Value);
    }

    [TestMethod]
    public void SelectedSystemIndex_Chaldean_RefocusesBreakdown()
    {
        var vm = Create();
        vm.InputText = "HELLO";

        vm.SelectedSystemIndex = 2; // Chaldean

        Assert.AreEqual(23, vm.FocusedTotal);
        Assert.AreEqual(5, vm.FocusedReduced);
        Assert.AreEqual(5, vm.Breakdown[0].Value); // Chaldean H = 5
        Assert.IsTrue(vm.Systems[2].IsSelected);
        Assert.IsFalse(vm.Systems[0].IsSelected);
    }

    [TestMethod]
    public void ReductionModeIndex_RawTotal_LeavesTotalUnreduced()
    {
        var vm = Create();
        vm.InputText = "HELLO";

        vm.ReductionModeIndex = 2; // raw total

        Assert.AreEqual(25, vm.FocusedReduced);
    }

    [TestMethod]
    public void PreserveMasterNumbers_KeepsElevenUnreduced()
    {
        var vm = Create();
        vm.SelectedSystemIndex = 3; // Simple
        vm.InputText = "K"; // Simple K = 11

        vm.PreserveMasterNumbers = true;
        Assert.AreEqual(11, vm.FocusedReduced);

        vm.PreserveMasterNumbers = false;
        Assert.AreEqual(2, vm.FocusedReduced);
    }

    [TestMethod]
    public void WordTotals_MultipleWords_ArePopulated()
    {
        var vm = Create();
        vm.SelectedSystemIndex = 3; // Simple

        vm.InputText = "ABC DEF";

        Assert.IsTrue(vm.ShowWordTotals);
        Assert.AreEqual(2, vm.WordTotals.Count);
        Assert.AreEqual(6, vm.WordTotals[0].Total);
        Assert.AreEqual(15, vm.WordTotals[1].Total);
    }

    [TestMethod]
    public void WordTotals_SingleWord_AreHidden()
    {
        var vm = Create();

        vm.InputText = "HELLO";

        Assert.IsFalse(vm.ShowWordTotals);
        Assert.AreEqual(0, vm.WordTotals.Count);
    }

    [TestMethod]
    public void EmptyNotice_ShownForDigitsOnly()
    {
        var vm = Create();

        vm.InputText = "12345";

        Assert.IsFalse(vm.HasResult);
        Assert.IsTrue(vm.ShowEmptyNotice);
    }

    [TestMethod]
    public void EmptyNotice_HiddenWhenInputBlank()
    {
        var vm = Create();

        vm.InputText = string.Empty;

        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.ShowEmptyNotice);
    }

    [TestMethod]
    public void CopyCommand_DisabledWithoutResult_EnabledWithResult()
    {
        var vm = Create();

        Assert.IsFalse(vm.CopyCommand.CanExecute(null));

        vm.InputText = "HELLO";

        Assert.IsTrue(vm.CopyCommand.CanExecute(null));
    }

    [TestMethod]
    public void CopyCommand_CopiesReportContainingResult()
    {
        var clipboard = new FakeClipboardService();
        var vm = Create(clipboard);
        vm.InputText = "ABC";

        vm.CopyCommand.Execute(null);

        Assert.IsNotNull(clipboard.LastText);
        StringAssert.Contains(clipboard.LastText, "ABC");
    }

    [TestMethod]
    public async Task ShareCommand_SharesReport()
    {
        var share = new FakeShareService();
        var vm = Create(share: share);
        vm.InputText = "ABC";

        await vm.ShareCommand.ExecuteAsync(null);

        Assert.IsNotNull(share.LastText);
        StringAssert.Contains(share.LastText, "ABC");
    }

    [TestMethod]
    public void ClearCommand_ResetsInput()
    {
        var vm = Create();
        vm.InputText = "HELLO";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasResult);
    }
}
