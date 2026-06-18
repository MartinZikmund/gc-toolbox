using System.Collections.Generic;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class NihilistCipherViewModelTests
{
    private static NihilistCipherViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        StubCatalogService catalog = new("NihilistCipher");
        RecentsService recents = new(new InMemoryPreferences(), catalog);
        FavoriteToolsService favorites = new(new InMemoryPreferences(), catalog);
        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            { "Name_NihilistCipher", "Nihilist cipher" },
            { "NihilistTextLabel", "Text" },
            { "NihilistNumbersLabel", "Numbers" },
        });
        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        return new NihilistCipherViewModel(catalog, recents, favorites, localizer, clipboard, share);
    }

    [TestMethod]
    public void Defaults_EncodeWikipediaVector_ProducesCipherStream()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "DYNAMITEWINTERPALACE";

        Assert.AreEqual(
            "37 106 62 36 67 47 86 26 104 53 62 77 27 55 57 66 55 36 54 27",
            vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void DecodeDirection_NumberStream_RecoversPlaintext()
    {
        var vm = CreateViewModel(out _, out _);
        vm.DirectionIndex = 1;

        vm.InputText = "37 106 62 36 67 47 86 26 104 53 62 77 27 55 57 66 55 36 54 27";

        Assert.AreEqual("DYNAMITEWINTERPALACE", vm.OutputText);
    }

    [TestMethod]
    public void EmptyAdditiveKeyword_ShowsError()
    {
        var vm = CreateViewModel(out _, out _);
        vm.AdditiveKeyword = string.Empty;

        vm.InputText = "HELLO";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(string.Empty, vm.OutputText);
    }

    [TestMethod]
    public void CharacterNotInSquare_ShowsError()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "HELLO123"; // digits aren't in a 5×5 square

        Assert.IsTrue(vm.HasError);
    }

    [TestMethod]
    public void SixBySixGrid_EncodesDigits()
    {
        var vm = CreateViewModel(out _, out _);
        vm.GridSizeIndex = 1; // 6×6
        vm.PolybiusKeyword = "GEOCACHE";
        vm.AdditiveKeyword = "WAYPOINT";

        vm.InputText = "N49E8M2";

        Assert.IsFalse(vm.HasError);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void SquareCells_DefaultZebras_RenderTwentyFiveCells()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.AreEqual(25, vm.SquareCells.Count);
        Assert.AreEqual("Z", vm.SquareCells[0].Symbol);
        Assert.AreEqual("11", vm.SquareCells[0].Coordinate);
        Assert.AreEqual(5, vm.SquareSize);
    }

    [TestMethod]
    public void SquareCells_SixBySix_RenderThirtySixCells()
    {
        var vm = CreateViewModel(out _, out _);

        vm.GridSizeIndex = 1;

        Assert.AreEqual(36, vm.SquareCells.Count);
        Assert.AreEqual(6, vm.SquareSize);
    }

    [TestMethod]
    public void Breakdown_ShowsPlainKeyCipherLines()
    {
        var vm = CreateViewModel(out _, out _);

        vm.InputText = "D";

        Assert.IsTrue(vm.ShowBreakdown);
        Assert.AreEqual(1, vm.BreakdownLines.Count);
        StringAssert.Contains(vm.BreakdownLines[0], "= 37");
    }

    [TestMethod]
    public void SwapDirection_CarriesOutputIntoInputAndFlipsDirection()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "DYN";
        var cipher = vm.OutputText;

        vm.SwapDirectionCommand.Execute(null);

        Assert.AreEqual(1, vm.DirectionIndex);
        Assert.AreEqual(cipher, vm.InputText);
        Assert.AreEqual("DYN", vm.OutputText);
    }

    [TestMethod]
    public void CopyOutput_PutsResultOnClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.InputText = "DYN";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual(vm.OutputText, clipboard.LastText);
    }

    [TestMethod]
    public void CopyOutput_DisabledWhenNoOutput()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void OrientationToggle_ChangesFirstCellCoordinate()
    {
        var vm = CreateViewModel(out _, out _);
        var rowCol = vm.SquareCells[1].Coordinate; // E at row1 col2 => 12 (row-col)

        vm.OrientationIndex = 1; // column-row

        Assert.AreEqual("12", rowCol);
        Assert.AreEqual("21", vm.SquareCells[1].Coordinate);
    }

    [TestMethod]
    public void ZeroBaseToggle_ShiftsAxisLabels()
    {
        var vm = CreateViewModel(out _, out _);

        vm.IsZeroBased = true;

        Assert.AreEqual(0, vm.AxisLabels[0]);
        Assert.AreEqual("0", vm.SquareCells[0].Coordinate); // row0 col0 base-0 => numeric 0
        Assert.AreEqual("1", vm.SquareCells[1].Coordinate);  // row0 col1 base-0 => numeric 1
    }

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "DYN";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void EmptyInput_ClearsOutputAndErrors()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "DYN";

        vm.InputText = string.Empty;

        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.HasError);
    }
}
