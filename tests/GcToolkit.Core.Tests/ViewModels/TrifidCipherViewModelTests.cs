using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

/// <summary>
/// Exercises <see cref="TrifidCipherViewModel"/> with hand-written fakes: live preview, swap,
/// drop notice, cube rendering, copy/share, and the reset/random cube commands.
/// </summary>
[TestClass]
public sealed class TrifidCipherViewModelTests
{
    private static TrifidCipherViewModel CreateSut(
        FakeClipboardService? clipboard = null,
        FakeShareService? share = null)
    {
        var catalog = new SingleToolCatalog("TrifidCipher", "TrifidCipher_Name", "TrifidCipher_Tooltip");
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TrifidCipher_Name"] = "Trifid cipher",
            ["TrifidCipher_Tooltip"] = "Encode and decode the Trifid cipher.",
            ["TrifidDropNotice"] = "{0} character(s) outside the alphabet were dropped.",
        });

        var sut = new TrifidCipherViewModel(
            catalog,
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            localizer,
            clipboard ?? new FakeClipboardService(),
            share ?? new FakeShareService());
        sut.ViewCreated();
        return sut;
    }

    [TestMethod]
    public void Constructor_RendersStandardCubeAsThreeSquares()
    {
        var sut = CreateSut();

        Assert.AreEqual(9, sut.SquareOne.Count);
        Assert.AreEqual(9, sut.SquareTwo.Count);
        Assert.AreEqual(9, sut.SquareThree.Count);
        Assert.AreEqual('A', sut.SquareOne[0].Symbol);
        Assert.AreEqual('+', sut.SquareThree[8].Symbol); // last cell = filler
    }

    [TestMethod]
    public void Input_LivePreview_EncryptsWithStandardCube()
    {
        var sut = CreateSut();
        sut.Period = 5;

        sut.InputText = "CACHE";

        Assert.AreEqual("AACPW", sut.OutputText);
        Assert.IsTrue(sut.HasOutput);
    }

    [TestMethod]
    public void Decrypt_RecoversPlaintext()
    {
        var sut = CreateSut();
        sut.Period = 5;
        sut.DirectionIndex = 1; // Decrypt

        sut.InputText = "AACPW";

        Assert.AreEqual("CACHE", sut.OutputText);
    }

    [TestMethod]
    public void Swap_CarriesResultIntoInputAndFlipsDirection()
    {
        var sut = CreateSut();
        sut.Period = 5;
        sut.InputText = "CACHE";
        var encrypted = sut.OutputText;

        sut.SwapCommand.Execute(null);

        Assert.AreEqual(1, sut.DirectionIndex);       // now decrypting
        Assert.AreEqual(encrypted, sut.InputText);    // previous output became the input
        Assert.AreEqual("CACHE", sut.OutputText);     // round-trips back to the plaintext
    }

    [TestMethod]
    public void Input_WithOutOfAlphabetChars_ShowsDropNotice()
    {
        var sut = CreateSut();
        sut.Period = 5;

        sut.InputText = "CACHE123";

        Assert.IsTrue(sut.HasDropNotice);
        StringAssert.Contains(sut.DropNotice, "3");
        Assert.AreEqual("AACPW", sut.OutputText); // digits dropped → same as CACHE
    }

    [TestMethod]
    public void Keyword_ReseedsCube()
    {
        var sut = CreateSut();

        sut.Keyword = "CACHE";

        // "CACHE" deduped → CAHE seeds the first cells of square one.
        Assert.AreEqual('C', sut.SquareOne[0].Symbol);
        Assert.AreEqual('A', sut.SquareOne[1].Symbol);
        Assert.AreEqual('H', sut.SquareOne[2].Symbol);
    }

    [TestMethod]
    public void FillerSelector_ChangesTheFillerSymbol()
    {
        var sut = CreateSut();

        sut.FillerIndex = 1; // '.'

        Assert.AreEqual('.', sut.SquareThree[8].Symbol);
    }

    [TestMethod]
    public void WholeMessage_IgnoresPeriod()
    {
        var sut = CreateSut();
        sut.InputText = "DELASTELLETRIFID";
        sut.Period = 4;
        var blocked = sut.OutputText;

        sut.WholeMessage = true;
        var whole = sut.OutputText;

        Assert.AreNotEqual(blocked, whole);

        // And still round-trips as a whole block.
        var cipher = new TrifidCipher();
        var back = cipher.Decrypt(whole, cipher.BuildAlphabet(), 0, TrifidReadingOrder.SquareRowColumn);
        Assert.AreEqual("DELASTELLETRIFID", back.Text);
    }

    [TestMethod]
    public void ResetCube_RestoresStandardAlphabet()
    {
        var sut = CreateSut();
        sut.Keyword = "SECRET";
        sut.FillerIndex = 2;

        sut.ResetCubeCommand.Execute(null);

        Assert.AreEqual(string.Empty, sut.Keyword);
        Assert.AreEqual('A', sut.SquareOne[0].Symbol);
        Assert.AreEqual('+', sut.SquareThree[8].Symbol);
    }

    [TestMethod]
    public void RandomCube_ProducesValid27CellPermutation()
    {
        var sut = CreateSut();

        sut.RandomCubeCommand.Execute(null);

        var all = sut.SquareOne.Concat(sut.SquareTwo).Concat(sut.SquareThree).Select(c => c.Symbol).ToList();
        Assert.AreEqual(27, all.Count);
        Assert.AreEqual(27, all.Distinct().Count());
    }

    [TestMethod]
    public void CopyOutput_CopiesResultToClipboard()
    {
        var clipboard = new FakeClipboardService();
        var sut = CreateSut(clipboard);
        sut.Period = 5;
        sut.InputText = "CACHE";

        sut.CopyOutputCommand.Execute(null);

        Assert.AreEqual("AACPW", clipboard.LastText);
    }

    [TestMethod]
    public async Task ShareOutput_SharesResult()
    {
        var share = new FakeShareService();
        var sut = CreateSut(share: share);
        sut.Period = 5;
        sut.InputText = "CACHE";

        await sut.ShareOutputCommand.ExecuteAsync(null);

        Assert.AreEqual("AACPW", share.LastSharedText);
    }

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var sut = CreateSut();
        sut.InputText = "CACHE";

        sut.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, sut.InputText);
        Assert.AreEqual(string.Empty, sut.OutputText);
        Assert.IsFalse(sut.HasOutput);
    }

    [TestMethod]
    public void EmptyInput_ProducesNoOutput()
    {
        var sut = CreateSut();

        sut.InputText = "   ";

        Assert.AreEqual(string.Empty, sut.OutputText);
        Assert.IsFalse(sut.HasOutput);
    }

    /// <summary>A one-tool catalog so the base ViewModel can resolve its descriptor and localize the name.</summary>
    private sealed class SingleToolCatalog : ICatalogService
    {
        private readonly ToolDescriptor _tool;

        public SingleToolCatalog(string id, string nameKey, string tooltipKey)
            => _tool = new ToolDescriptor(id, nameKey, "Ciphers", [], id, typeof(object), TooltipKey: tooltipKey);

        public IReadOnlyList<Category> GetCategories() => [];

        public IReadOnlyList<ToolDescriptor> GetTools() => [_tool];

        public IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId) => [_tool];

        public IReadOnlyList<ToolDescriptor> Search(string query) => [_tool];
    }
}
