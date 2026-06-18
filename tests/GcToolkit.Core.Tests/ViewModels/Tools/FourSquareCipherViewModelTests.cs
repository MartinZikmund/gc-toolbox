using System.Linq;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels.Tools;

[TestClass]
public sealed class FourSquareCipherViewModelTests
{
    private static FourSquareCipherViewModel CreateSut(
        FakeClipboardService? clipboard = null,
        FakeShareService? share = null)
    {
        var catalog = new TestCatalog();
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["FourSquareCipher_Name"] = "Four-square cipher",
            ["FourSquareCipher_Tooltip"] = "Digraph cipher",
            ["FourSquareIncompleteSquare"] = "Incomplete square",
            ["FourSquareNeedsKeywords"] = "Enter keywords",
        });

        return new FourSquareCipherViewModel(
            catalog,
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            localizer,
            clipboard ?? new FakeClipboardService(),
            share ?? new FakeShareService());
    }

    private static FourSquareCipherViewModel CreateActivated(
        FakeClipboardService? clipboard = null,
        FakeShareService? share = null)
    {
        var sut = CreateSut(clipboard, share);
        sut.ViewCreated();
        return sut;
    }

    [TestMethod]
    public void Default_UsesWikipediaKeywords_AndSkipQAlphabet()
    {
        var sut = CreateActivated();

        sut.TopRightKeyword = "EXAMPLE";
        sut.BottomLeftKeyword = "KEYWORD";
        sut.AlphabetModeIndex = 1; // Skip
        sut.SkipLetter = "Q";
        sut.InputText = "HELPMEOBIWANKENOBI";

        Assert.AreEqual("FYGMKYHOBXMFKKKIMD", sut.OutputText);
        Assert.IsTrue(sut.HasOutput);
    }

    [TestMethod]
    public void Encrypt_LivePreview_UpdatesAsInputChanges()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "SECRET";
        sut.BottomLeftKeyword = "PUZZLE";

        sut.InputText = "GEO";
        var first = sut.OutputText;

        sut.InputText = "GEOCACHE";
        Assert.AreNotEqual(first, sut.OutputText);
        Assert.IsTrue(sut.OutputText.Length > 0);
    }

    [TestMethod]
    public void Decrypt_ReversesEncrypt()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "SECRET";
        sut.BottomLeftKeyword = "PUZZLE";
        sut.InputText = "GEOCACHE";
        var cipher = sut.OutputText;

        sut.DirectionIndex = 1; // Decrypt
        sut.InputText = cipher;

        Assert.AreEqual("GEOCACHE", sut.OutputText);
    }

    [TestMethod]
    public void Swap_CarriesOutputIntoInputAndFlipsDirection()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "SECRET";
        sut.BottomLeftKeyword = "PUZZLE";
        sut.InputText = "GEOCACHE";
        var cipher = sut.OutputText;

        sut.SwapCommand.Execute(null);

        Assert.AreEqual(1, sut.DirectionIndex);
        Assert.AreEqual(cipher, sut.InputText);
        Assert.AreEqual("GEOCACHE", sut.OutputText);
    }

    [TestMethod]
    public void OddLength_AppendsFiller_AndShowsNotice()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "SECRET";
        sut.BottomLeftKeyword = "PUZZLE";

        sut.InputText = "ABC";

        Assert.IsTrue(sut.PaddingApplied);
        Assert.AreEqual(4, sut.OutputText.Length);
    }

    [TestMethod]
    public void CustomFiller_ChangesPaddedOutput()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "SECRET";
        sut.BottomLeftKeyword = "PUZZLE";
        sut.InputText = "A";

        var withX = sut.OutputText;
        sut.Filler = "Z";
        var withZ = sut.OutputText;

        Assert.AreNotEqual(withX, withZ);
    }

    [TestMethod]
    public void Squares_ExposeTwentyFiveCellsEach()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "EXAMPLE";
        sut.BottomLeftKeyword = "KEYWORD";

        Assert.AreEqual(25, sut.PlainSquare.Count);
        Assert.AreEqual(25, sut.TopRightSquare.Count);
        Assert.AreEqual(25, sut.BottomLeftSquare.Count);
    }

    [TestMethod]
    public void Squares_ReflectKeywordChanges()
    {
        var sut = CreateActivated();
        sut.AlphabetModeIndex = 1;
        sut.SkipLetter = "Q";
        sut.TopRightKeyword = "EXAMPLE";
        sut.BottomLeftKeyword = "KEYWORD";

        var topRight = string.Concat(sut.TopRightSquare.Select(c => c.Letter));
        Assert.AreEqual("EXAMPLBCDFGHIJKNORSTUVWYZ", topRight);
    }

    [TestMethod]
    public void EmptyInput_NoOutput_NoWarning()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "SECRET";
        sut.BottomLeftKeyword = "PUZZLE";

        sut.InputText = "";

        Assert.AreEqual(string.Empty, sut.OutputText);
        Assert.IsFalse(sut.HasOutput);
        Assert.IsFalse(sut.PaddingApplied);
    }

    [TestMethod]
    public void CopyOutput_PutsResultOnClipboard()
    {
        var clipboard = new FakeClipboardService();
        var sut = CreateActivated(clipboard);
        sut.TopRightKeyword = "SECRET";
        sut.BottomLeftKeyword = "PUZZLE";
        sut.InputText = "GEOCACHE";

        sut.CopyOutputCommand.Execute(null);

        Assert.AreEqual(sut.OutputText, clipboard.LastText);
    }

    [TestMethod]
    public void ShareOutput_SharesResult()
    {
        var share = new FakeShareService();
        var sut = CreateActivated(share: share);
        sut.TopRightKeyword = "SECRET";
        sut.BottomLeftKeyword = "PUZZLE";
        sut.InputText = "GEOCACHE";

        sut.ShareOutputCommand.Execute(null);

        Assert.AreEqual(sut.OutputText, share.LastText);
    }

    [TestMethod]
    public void Clear_ResetsInput()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "SECRET";
        sut.BottomLeftKeyword = "PUZZLE";
        sut.InputText = "GEOCACHE";

        sut.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, sut.InputText);
        Assert.IsFalse(sut.HasOutput);
    }

    [TestMethod]
    public void CopyCommand_Disabled_WhenNoOutput()
    {
        var sut = CreateActivated();
        Assert.IsFalse(sut.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void Encrypt_HighlightsFourCellsForFirstDigraph()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "EXAMPLE";
        sut.BottomLeftKeyword = "KEYWORD";
        sut.AlphabetModeIndex = 1;
        sut.SkipLetter = "Q";
        sut.InputText = "HELPMEOBIWANKENOBI";

        // First digraph HE -> H,E in the plain squares and F,Y in the keyword squares.
        var plainH = sut.PlainSquare.Single(c => c.Letter == "H");
        var plainE = sut.PlainSquare.Single(c => c.Letter == "E");
        var cipherF = sut.TopRightSquare.Single(c => c.Letter == "F");
        var cipherY = sut.BottomLeftSquare.Single(c => c.Letter == "Y");

        Assert.IsTrue(plainH.IsHighlighted);
        Assert.IsTrue(plainE.IsHighlighted);
        Assert.IsTrue(cipherF.IsHighlighted);
        Assert.IsTrue(cipherY.IsHighlighted);
    }

    [TestMethod]
    public void EmptyInput_ClearsHighlights()
    {
        var sut = CreateActivated();
        sut.TopRightKeyword = "EXAMPLE";
        sut.BottomLeftKeyword = "KEYWORD";
        sut.InputText = "HE";
        sut.InputText = "";

        Assert.IsFalse(sut.PlainSquare.Any(c => c.IsHighlighted));
        Assert.IsFalse(sut.TopRightSquare.Any(c => c.IsHighlighted));
    }

    [TestMethod]
    public void SkipLetterChange_RebuildsAlphabet()
    {
        var sut = CreateActivated();
        sut.AlphabetModeIndex = 1;
        sut.TopRightKeyword = "";
        sut.BottomLeftKeyword = "";

        sut.SkipLetter = "Z";
        var plain = string.Concat(sut.PlainSquare.Select(c => c.Letter));
        Assert.IsFalse(plain.Contains('Z'));
        Assert.IsTrue(plain.Contains('J'));
    }

    /// <summary>Minimal catalog returning the four-square descriptor so the base activates.</summary>
    private sealed class TestCatalog : ICatalogService
    {
        private readonly ToolDescriptor _tool = new(
            "FourSquareCipher",
            "FourSquareCipher_Name",
            "ciphers",
            [],
            "FourSquareCipher",
            typeof(FourSquareCipherViewModel),
            IsPlaceholder: false,
            TooltipKey: "FourSquareCipher_Tooltip");

        public IReadOnlyList<Category> GetCategories() => [];

        public IReadOnlyList<ToolDescriptor> GetTools() => [_tool];

        public IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId) => [_tool];

        public IReadOnlyList<ToolDescriptor> Search(string query) => [_tool];
    }
}
