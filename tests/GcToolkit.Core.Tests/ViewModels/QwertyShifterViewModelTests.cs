using System.Collections.Generic;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public sealed class QwertyShifterViewModelTests
{
    private static QwertyShifterViewModel CreateSut(
        out RecordingClipboardService clipboard,
        out RecordingShareService share)
    {
        clipboard = new RecordingClipboardService();
        share = new RecordingShareService();

        var catalog = new StubCatalogService("QwertyShifter");
        var prefs = new InMemoryPreferences();
        var recents = new RecentsService(prefs, catalog);
        var favorites = new FavoriteToolsService(prefs, catalog);
        // StubCatalogService exposes NameKey = "Name_<id>" and an empty tooltip key.
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            { "Name_QwertyShifter", "QWERTY Shifter" },
        });

        var vm = new QwertyShifterViewModel(catalog, recents, favorites, localizer, clipboard, share);
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void Input_ProducesShiftedOutput_Live()
    {
        var sut = CreateSut(out _, out _);
        sut.Shift = 1;

        sut.InputText = "Q";

        Assert.AreEqual("W", sut.OutputText);
        Assert.IsTrue(sut.HasOutput);
    }

    [TestMethod]
    public void DirectionLeft_ShiftsTheOtherWay()
    {
        var sut = CreateSut(out _, out _);
        sut.Shift = 1;
        sut.InputText = "W";

        sut.DirectionIndex = 1; // Left

        Assert.AreEqual("Q", sut.OutputText);
    }

    [TestMethod]
    public void EmptyInput_ClearsOutputAndDisablesActions()
    {
        var sut = CreateSut(out _, out _);
        sut.InputText = "Q";
        Assert.IsTrue(sut.HasOutput);

        sut.InputText = string.Empty;

        Assert.AreEqual(string.Empty, sut.OutputText);
        Assert.IsFalse(sut.HasOutput);
        Assert.IsFalse(sut.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void AlphabetIndex_UpdatesMaxShift()
    {
        var sut = CreateSut(out _, out _);

        Assert.AreEqual(36, sut.MaxShift);

        sut.AlphabetIndex = 1; // Extended

        Assert.AreEqual(47, sut.MaxShift);
    }

    [TestMethod]
    public void Shift_AboveMax_IsClampedToAlphabetSize()
    {
        var sut = CreateSut(out _, out _);

        sut.Shift = 99;

        Assert.AreEqual(sut.MaxShift, sut.Shift);
    }

    [TestMethod]
    public void SwitchingToSmallerAlphabet_ReclampsTheShift()
    {
        var sut = CreateSut(out _, out _);
        sut.AlphabetIndex = 1; // 47
        sut.Shift = 47;

        sut.AlphabetIndex = 0; // back to 36

        Assert.AreEqual(36, sut.MaxShift);
        Assert.IsTrue(sut.Shift <= 36);
    }

    [TestMethod]
    public void FoldToUpper_UppercasesOutput()
    {
        var sut = CreateSut(out _, out _);
        sut.Shift = 1;
        sut.FoldToUpper = true;

        sut.InputText = "q";

        Assert.AreEqual("W", sut.OutputText);
    }

    [TestMethod]
    public void BatchMode_ShiftsEachLineIndependently()
    {
        var sut = CreateSut(out _, out _);
        sut.Shift = 1;
        sut.BatchMode = true;

        sut.InputText = "Q\nA";

        Assert.AreEqual("W\nS", sut.OutputText);
    }

    [TestMethod]
    public void BruteForce_Lists35Candidates_ForLettersDigits()
    {
        var sut = CreateSut(out _, out _);
        sut.InputText = "Q";

        Assert.AreEqual(35, sut.BruteForceResults.Count);
        Assert.AreEqual(1, sut.BruteForceResults[0].Shift);
    }

    [TestMethod]
    public void Keyboard_HasFourRows_WithShiftedSubstitutes()
    {
        var sut = CreateSut(out _, out _);
        sut.Shift = 1;
        sut.InputText = "X"; // any input to populate

        Assert.AreEqual(4, sut.KeyboardRows.Count);
        // First key of the number row is '1'; one right is '2'.
        var firstKey = sut.KeyboardRows[0][0];
        Assert.AreEqual("1", firstKey.Plain);
        Assert.AreEqual("2", firstKey.Shifted);
    }

    [TestMethod]
    public void CopyOutput_CopiesResultToClipboard()
    {
        var sut = CreateSut(out var clipboard, out _);
        sut.Shift = 1;
        sut.InputText = "Q";

        sut.CopyOutputCommand.Execute(null);

        Assert.AreEqual("W", clipboard.LastText);
    }

    [TestMethod]
    public void Clear_EmptiesInput()
    {
        var sut = CreateSut(out _, out _);
        sut.InputText = "GEOCACHE";

        sut.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, sut.InputText);
    }

    [TestMethod]
    public void ToolName_ResolvedFromLocalizer()
    {
        var sut = CreateSut(out _, out _);
        Assert.AreEqual("QWERTY Shifter", sut.ToolName);
    }
}
