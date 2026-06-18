using GcToolkit.Core.Catalog;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public sealed class AdfgvxCipherViewModelTests
{
    private static AdfgvxCipherViewModel CreateSut(
        out FakeClipboardService clipboard,
        out FakeShareService share)
    {
        clipboard = new FakeClipboardService();
        share = new FakeShareService();

        var descriptor = new ToolDescriptor(
            "AdfgvxCipher", "AdfgvxCipher_Name", "ciphers", [], "AdfgvxCipher",
            typeof(AdfgvxCipherViewModel), false, "AdfgvxCipher_Tooltip");

        ICatalogService catalog = new SingleToolCatalog(descriptor);
        IStringLocalizer localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["AdfgvxCipher_Name"] = "ADFGX / ADFGVX cipher",
            ["AdfgvxCipher_Tooltip"] = "Tip",
            ["AdfgvxCellLabel"] = "Row {0}, column {1}: {2}",
            ["AdfgvxNoteFoldedJ"] = "J folded to I.",
            ["AdfgvxNoteDropped"] = "Unsupported characters ignored.",
            ["AdfgvxErrorIncompleteSquare"] = "Incomplete square.",
            ["AdfgvxErrorDuplicateCell"] = "Duplicate cell.",
            ["AdfgvxErrorInvalidSquareChar"] = "Invalid square character.",
            ["AdfgvxErrorEmptyKeyword"] = "Keyword required.",
            ["AdfgvxErrorMalformedCiphertext"] = "Malformed ciphertext.",
        });

        var sut = new AdfgvxCipherViewModel(
            catalog, new FakeRecentsService(), new FakeFavoriteToolsService(), localizer, clipboard, share);
        sut.ViewCreated(); // resolve name/tooltip from the catalog
        return sut;
    }

    [TestMethod]
    public void Initial_Adfgx_RendersOrdered25CellGrid()
    {
        var sut = CreateSut(out _, out _);

        Assert.AreEqual(5, sut.SquareSize);
        Assert.AreEqual("ADFGX", sut.Headers);
        Assert.AreEqual(25, sut.SquareCells.Count);
        Assert.AreEqual("A", sut.SquareCells[0].Value); // ordered fill starts at A
    }

    [TestMethod]
    public void SwitchToAdfgvx_Renders36CellGridWithSixHeaders()
    {
        var sut = CreateSut(out _, out _);

        sut.VariantIndex = 1;

        Assert.AreEqual(6, sut.SquareSize);
        Assert.AreEqual("ADFGVX", sut.Headers);
        Assert.AreEqual(36, sut.SquareCells.Count);
    }

    [TestMethod]
    public void Encrypt_WikipediaSquareAndKeyword_ProducesKnownCiphertext()
    {
        var sut = CreateSut(out _, out _);
        sut.SquareFill = "BTALPDHOZKQFVSNGICUXMREWY";
        sut.Keyword = "CARGO";

        sut.InputText = "attack at once";

        Assert.AreEqual("FAXDFADDDGDGFFFAFAXAFAFX", sut.OutputText);
        Assert.IsTrue(sut.HasOutput);
    }

    [TestMethod]
    public void SubstitutionPreview_Encrypt_HasOneStepPerKeptChar()
    {
        var sut = CreateSut(out _, out _);
        sut.Keyword = "KEY";

        sut.InputText = "HELLO";

        Assert.AreEqual(5, sut.SubstitutionSteps.Count);
        Assert.AreEqual("H", sut.SubstitutionSteps[0].PlainChar);
        Assert.AreEqual(2, sut.SubstitutionSteps[0].Pair.Length);
    }

    [TestMethod]
    public void Decrypt_ReversesEncrypt()
    {
        var sut = CreateSut(out _, out _);
        sut.Keyword = "SECRET";
        sut.InputText = "GEOCACHE";
        var cipherText = sut.OutputText;

        sut.DirectionIndex = 1; // Decrypt
        sut.InputText = cipherText;

        Assert.AreEqual("GEOCACHE", sut.OutputText);
    }

    [TestMethod]
    public void Swap_FlipsDirectionAndRoundTrips()
    {
        var sut = CreateSut(out _, out _);
        sut.Keyword = "WALDO";
        sut.InputText = "HIDDEN";
        var cipherText = sut.OutputText;

        sut.SwapCommand.Execute(null);

        Assert.AreEqual(1, sut.DirectionIndex);
        Assert.AreEqual(cipherText, sut.InputText);
        Assert.AreEqual("HIDDEN", sut.OutputText);
    }

    [TestMethod]
    public void Encrypt_WithJ_ShowsFoldNotice()
    {
        var sut = CreateSut(out _, out _);
        sut.Keyword = "KEY";

        sut.InputText = "JAZZ";

        Assert.IsTrue(sut.HasNotice);
        Assert.IsFalse(sut.IsError);
        Assert.IsTrue(sut.NoticeMessage.Contains("J folded"));
    }

    [TestMethod]
    public void EmptyKeyword_WithInput_ShowsError()
    {
        var sut = CreateSut(out _, out _);
        sut.Keyword = string.Empty;

        sut.InputText = "HELLO";

        Assert.IsTrue(sut.HasNotice);
        Assert.IsTrue(sut.IsError);
        Assert.IsFalse(sut.HasOutput);
    }

    [TestMethod]
    public void DuplicateCellInSquare_ShowsError()
    {
        var sut = CreateSut(out _, out _);
        sut.Keyword = "KEY";

        sut.SquareFill = "AABCDEFGHIKLMNOPQRSTUVWXY"; // duplicate A

        Assert.IsTrue(sut.HasNotice);
        Assert.IsTrue(sut.IsError);
    }

    [TestMethod]
    public void OrderedSquareCommand_ResetsToAlphabet()
    {
        var sut = CreateSut(out _, out _);
        sut.SquareFill = "ZYXWVUTSRQPONMLKIHGFEDCBA";

        sut.OrderedSquareCommand.Execute(null);

        Assert.AreEqual("ABCDEFGHIKLMNOPQRSTUVWXYZ", sut.SquareFill);
    }

    [TestMethod]
    public void KeywordSeededSquareCommand_PrependsKeyword()
    {
        var sut = CreateSut(out _, out _);
        sut.Keyword = "SECRET";

        sut.KeywordSeededSquareCommand.Execute(null);

        Assert.IsTrue(sut.SquareFill.StartsWith("SECRT")); // distinct letters of SECRET
        Assert.AreEqual(25, sut.SquareFill.Length);
    }

    [TestMethod]
    public void RandomSquareCommand_ProducesValid25LetterFill()
    {
        var sut = CreateSut(out _, out _);

        sut.RandomSquareCommand.Execute(null);

        Assert.AreEqual(25, sut.SquareFill.Length);
        Assert.AreEqual(25, sut.SquareFill.Distinct().Count());
        Assert.IsFalse(sut.HasNotice); // a valid square produces no error
    }

    [TestMethod]
    public void CopyOutput_CopiesResultToClipboard()
    {
        var sut = CreateSut(out var clipboard, out _);
        sut.Keyword = "KEY";
        sut.InputText = "HELLO";

        sut.CopyOutputCommand.Execute(null);

        Assert.AreEqual(sut.OutputText, clipboard.LastText);
    }

    [TestMethod]
    public async Task ShareOutput_SharesResult()
    {
        var sut = CreateSut(out _, out var share);
        sut.Keyword = "KEY";
        sut.InputText = "HELLO";

        await sut.ShareOutputCommand.ExecuteAsync(null);

        Assert.AreEqual(1, share.ShareTextCallCount);
        Assert.AreEqual(sut.OutputText, share.LastText);
    }

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var sut = CreateSut(out _, out _);
        sut.Keyword = "KEY";
        sut.InputText = "HELLO";

        sut.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, sut.InputText);
        Assert.IsFalse(sut.HasOutput);
    }

    [TestMethod]
    public void CopyAndShare_DisabledWhenNoOutput()
    {
        var sut = CreateSut(out _, out _);

        Assert.IsFalse(sut.CopyOutputCommand.CanExecute(null));
        Assert.IsFalse(sut.ShareOutputCommand.CanExecute(null));
        Assert.IsFalse(sut.SwapCommand.CanExecute(null));
    }

    /// <summary>Minimal catalog returning a single descriptor so the base VM can resolve its metadata.</summary>
    private sealed class SingleToolCatalog(ToolDescriptor descriptor) : ICatalogService
    {
        private readonly IReadOnlyList<ToolDescriptor> _tools = [descriptor];

        public IReadOnlyList<Category> GetCategories() => [];

        public IReadOnlyList<ToolDescriptor> GetTools() => _tools;

        public IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId) => _tools;

        public IReadOnlyList<ToolDescriptor> Search(string query) => _tools;
    }
}
