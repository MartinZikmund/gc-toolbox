using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class PolybiusSquareViewModelTests
{
    private static PolybiusSquareViewModel CreateActivated(
        out FakeClipboardService clipboard, out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        var localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["Name_PolybiusSquare"] = "Polybius Square",
            ["PolybiusDecodeInvalidWarning"] = "Invalid pairs: {0}",
            ["PolybiusDecodeLeftoverWarning"] = "Leftover digit",
            ["PolybiusDecodeMixedWarning"] = "Invalid and leftover",
            ["PolybiusSquareInvalidOptions"] = "Bad square",
        });

        var vm = new PolybiusSquareViewModel(
            new StubCatalogService("PolybiusSquare"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            localizer,
            clipboard,
            share);

        vm.ViewCreated(); // builds the square and renders the grid
        return vm;
    }

    private static PolybiusSquareViewModel CreateActivated()
        => CreateActivated(out _, out _);

    [TestMethod]
    public void TypingPlainText_LiveEncodesToCipher()
    {
        var vm = CreateActivated();

        vm.PlainText = "HELLO";

        Assert.AreEqual("23 15 31 31 34", vm.CipherText);
        Assert.IsTrue(vm.HasCipher);
    }

    [TestMethod]
    public void TypingCipherText_LiveDecodesToPlain()
    {
        var vm = CreateActivated();

        vm.CipherText = "23 15 31 31 34";

        Assert.AreEqual("HELLO", vm.PlainText);
    }

    [TestMethod]
    public void EditingCipher_DoesNotReEncodeOverIt()
    {
        var vm = CreateActivated();

        // Decoding sets PlainText, which must not bounce back and overwrite the user's CipherText.
        vm.CipherText = "23 15";
        Assert.AreEqual("HE", vm.PlainText);
        Assert.AreEqual("23 15", vm.CipherText);
    }

    [TestMethod]
    public void Grid_IsRenderedWith25CellsForFiveBySix()
    {
        var vm = CreateActivated();
        Assert.AreEqual(25, vm.Cells.Count);
        Assert.AreEqual(5, vm.Side);
    }

    [TestMethod]
    public void SwitchingToSixBySix_Renders36CellsAndReEncodes()
    {
        var vm = CreateActivated();
        vm.PlainText = "A";

        vm.GridSizeIndex = 1;

        Assert.AreEqual(36, vm.Cells.Count);
        Assert.AreEqual(6, vm.Side);
        Assert.AreEqual("11", vm.CipherText);
        Assert.IsFalse(vm.IsFiveByFive);
    }

    [TestMethod]
    public void Keyword_ReshufflesTheSquare()
    {
        var vm = CreateActivated();
        vm.PlainText = "K";
        var before = vm.CipherText;

        vm.Keyword = "KEYWORD";

        Assert.AreEqual("11", vm.CipherText); // K is now the first cell
        Assert.AreNotEqual(before, vm.CipherText);
    }

    [TestMethod]
    public void AdfgxLabels_ProduceLetterCoordinates()
    {
        var vm = CreateActivated();
        vm.LabelSchemeIndex = 1;
        vm.PlainText = "H";

        Assert.AreEqual("DF", vm.CipherText);
    }

    [TestMethod]
    public void Highlight_TracksTheLastTypedLetter()
    {
        var vm = CreateActivated();
        vm.PlainText = "H";

        var highlighted = vm.Cells.Where(c => c.IsHighlighted).ToList();
        Assert.AreEqual(1, highlighted.Count);
        Assert.AreEqual("H", highlighted[0].Letter);
    }

    [TestMethod]
    public void TappingACell_AppendsItsLetterToPlainText()
    {
        var vm = CreateActivated();
        var hCell = vm.Cells.First(c => c.Letter == "H");

        hCell.TapCommand.Execute(null);

        Assert.AreEqual("H", vm.PlainText);
        Assert.AreEqual("23", vm.CipherText);
    }

    [TestMethod]
    public void ForgivingDecode_FlagsNoiseWithWarning()
    {
        var vm = CreateActivated();

        vm.CipherText = "23 ?? 15";

        Assert.AreEqual("HE", vm.PlainText);
        Assert.IsTrue(vm.HasWarning);
    }

    [TestMethod]
    public void InvalidCustomLabels_SetErrorState()
    {
        var vm = CreateActivated();

        vm.LabelSchemeIndex = 2;
        vm.CustomLabels = "AA"; // wrong length and not distinct

        Assert.IsTrue(vm.HasError);
        Assert.AreEqual("Bad square", vm.ErrorMessage);
    }

    [TestMethod]
    public void Copy_PutsCipherOnClipboard()
    {
        var vm = CreateActivated(out var clipboard, out _);
        vm.PlainText = "HELLO";

        vm.CopyCipherCommand.Execute(null);

        Assert.AreEqual("23 15 31 31 34", clipboard.LastText);
    }

    [TestMethod]
    public void Example_PopulatesBothPanes()
    {
        var vm = CreateActivated();

        vm.ExampleCommand.Execute(null);

        Assert.IsTrue(vm.PlainText.Length > 0);
        Assert.IsTrue(vm.CipherText.Length > 0);
    }

    [TestMethod]
    public void Clear_EmptiesBothPanes()
    {
        var vm = CreateActivated();
        vm.PlainText = "HELLO";

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.PlainText);
        Assert.AreEqual(string.Empty, vm.CipherText);
        Assert.IsFalse(vm.HasCipher);
    }

    [TestMethod]
    public void Reset_RestoresDefaultsAndClearsText()
    {
        var vm = CreateActivated();
        vm.GridSizeIndex = 1;
        vm.Keyword = "SECRET";
        vm.PlainText = "TEST";

        vm.ResetCommand.Execute(null);

        Assert.AreEqual(0, vm.GridSizeIndex);
        Assert.AreEqual(string.Empty, vm.Keyword);
        Assert.AreEqual(string.Empty, vm.PlainText);
        Assert.AreEqual(string.Empty, vm.CipherText);
    }

    [TestMethod]
    public void ColumnThenRow_SwapsCoordinateOrder()
    {
        var vm = CreateActivated();
        vm.PlainText = "H";
        Assert.AreEqual("23", vm.CipherText);

        vm.ColumnThenRow = true;

        Assert.AreEqual("32", vm.CipherText);
    }
}
