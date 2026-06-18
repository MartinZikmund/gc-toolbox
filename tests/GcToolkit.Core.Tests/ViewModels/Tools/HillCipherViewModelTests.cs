using System.Collections.Generic;
using System.Linq;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels.Tools;

[TestClass]
public class HillCipherViewModelTests
{
    private static readonly IReadOnlyDictionary<string, string> Strings = new Dictionary<string, string>
    {
        ["HillCipher_Name"] = "Hill Cipher",
        ["HillCipher_Tooltip"] = "Matrix polygraphic cipher.",
        ["HillCellLabel"] = "Row {0}, column {1}",
        ["HillDeterminantFormat"] = "Determinant (mod 26): {0}",
        ["HillNotInvertible"] = "This key matrix has no inverse mod 26 — pick a matrix whose determinant is coprime with 26.",
        ["HillInvalidKey"] = "Enter a whole number in every matrix cell.",
        ["HillKeywordTooShort"] = "The keyword is too short to fill the matrix.",
        ["HillNoLetters"] = "Enter some letters to transform.",
    };

    private static HillCipherViewModel CreateViewModel(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        var catalog = new StubHillCatalog();
        var prefs = new InMemoryPreferences();
        RecentsService recents = new(prefs, catalog);
        FavoriteToolsService favorites = new(prefs, catalog);
        FakeStringLocalizer localizer = new(Strings);

        HillCipherViewModel vm = new(catalog, recents, favorites, localizer, clipboard, share);
        vm.ViewCreated();
        return vm;
    }

    [TestMethod]
    public void Default3x3Key_EncryptsACTtoPOH()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SizeIndex = 1; // 3x3 -> seeds the Wikipedia key
        vm.InputText = "ACT";

        Assert.AreEqual("POH", vm.OutputText);
        Assert.IsTrue(vm.HasOutput);
        Assert.IsFalse(vm.HasWarning);
    }

    [TestMethod]
    public void Decrypt_3x3_RecoversPlaintext()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SizeIndex = 1;
        vm.DirectionIndex = 1; // decrypt
        vm.InputText = "POH";

        Assert.AreEqual("ACT", vm.OutputText);
    }

    [TestMethod]
    public void LivePreview_RecomputesOnInputChange()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SizeIndex = 1;

        vm.InputText = "ACT";
        Assert.AreEqual("POH", vm.OutputText);

        vm.InputText = "CAT";
        Assert.AreEqual("FIN", vm.OutputText);
    }

    [TestMethod]
    public void EditingMatrixCell_RecomputesOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HI";
        var before = vm.OutputText;

        // Change one cell of the 2x2 key; output must update.
        vm.MatrixCells.First().Text = "5";
        Assert.AreNotEqual(before, vm.OutputText);
    }

    [TestMethod]
    public void NonInvertibleKey_ShowsWarning_NoOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HI";

        // Make the 2x2 key singular mod 26: [[1,1],[1,3]] det=2, gcd(2,26)=2.
        var cells = vm.MatrixCells;
        cells[0].Text = "1";
        cells[1].Text = "1";
        cells[2].Text = "1";
        cells[3].Text = "3";

        Assert.IsTrue(vm.HasWarning);
        Assert.IsFalse(vm.HasOutput);
        Assert.AreEqual(string.Empty, vm.OutputText);
        Assert.IsFalse(vm.HasInverse);
    }

    [TestMethod]
    public void InvalidCellText_ShowsInvalidKeyWarning()
    {
        var vm = CreateViewModel(out _, out _);
        vm.InputText = "HI";

        vm.MatrixCells.First().Text = "abc";

        Assert.IsTrue(vm.HasWarning);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void KeywordMode_DerivesKey_AndEncrypts()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SizeIndex = 1;            // 3x3
        vm.KeyModeIndex = 1;         // keyword mode
        vm.KeywordText = "GYBNQKURP";
        vm.InputText = "ACT";

        Assert.AreEqual("POH", vm.OutputText);
        Assert.IsTrue(vm.IsKeywordMode);
    }

    [TestMethod]
    public void KeywordMode_TooShort_ShowsWarning()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SizeIndex = 1;
        vm.KeyModeIndex = 1;
        vm.KeywordText = "ABC"; // needs 9 letters for 3x3

        Assert.IsTrue(vm.HasWarning);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void InverseDisplay_PopulatedForInvertibleKey()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SizeIndex = 1;

        Assert.IsTrue(vm.HasInverse);
        Assert.IsFalse(string.IsNullOrEmpty(vm.InverseMatrixDisplay));
        StringAssert.Contains(vm.DeterminantText, "Determinant");
    }

    [TestMethod]
    public void Swap_CarriesOutputIntoInput_AndFlipsDirection()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SizeIndex = 1;
        vm.InputText = "ACT";
        Assert.AreEqual("POH", vm.OutputText);

        vm.SwapCommand.Execute(null);

        Assert.AreEqual(1, vm.DirectionIndex);  // now decrypt
        Assert.AreEqual("POH", vm.InputText);    // ciphertext moved to input
        Assert.AreEqual("ACT", vm.OutputText);   // decrypts back to plaintext
    }

    [TestMethod]
    public void CustomPadCharacter_AffectsOutput()
    {
        var vm = CreateViewModel(out _, out _);
        // 2x2, odd-length input pads the last block.
        vm.InputText = "ACT";

        vm.PadCharacter = "X";
        var withX = vm.OutputText;

        vm.PadCharacter = "Z";
        Assert.AreNotEqual(withX, vm.OutputText);
    }

    [TestMethod]
    public void Copy_PutsOutputOnClipboard()
    {
        var vm = CreateViewModel(out var clipboard, out _);
        vm.SizeIndex = 1;
        vm.InputText = "ACT";

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual("POH", clipboard.LastText);
    }

    [TestMethod]
    public void Share_SendsOutputWithToolName()
    {
        var vm = CreateViewModel(out _, out var share);
        vm.SizeIndex = 1;
        vm.InputText = "ACT";

        vm.ShareOutputCommand.Execute(null);

        Assert.AreEqual("POH", share.LastText);
        Assert.AreEqual("Hill Cipher", share.LastTitle);
    }

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var vm = CreateViewModel(out _, out _);
        vm.SizeIndex = 1;
        vm.InputText = "ACT";
        Assert.IsTrue(vm.HasOutput);

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasOutput);
    }

    [TestMethod]
    public void Copy_DisabledWhenNoOutput()
    {
        var vm = CreateViewModel(out _, out _);
        // Fresh VM has no input -> no output.
        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
    }

    /// <summary>Minimal catalog exposing just the Hill Cipher descriptor with real Name/Tooltip keys.</summary>
    private sealed class StubHillCatalog : ICatalogService
    {
        private readonly IReadOnlyList<ToolDescriptor> _tools =
        [
            new ToolDescriptor("HillCipher", "HillCipher_Name", "Ciphers", [], "HillCipher", typeof(object),
                IsPlaceholder: false, TooltipKey: "HillCipher_Tooltip"),
        ];

        public IReadOnlyList<Category> GetCategories() => [];

        public IReadOnlyList<ToolDescriptor> GetTools() => _tools;

        public IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId) => _tools;

        public IReadOnlyList<ToolDescriptor> Search(string query) => _tools;
    }
}
