using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public sealed class HexahueViewModelTests
{
    private static HexahueViewModel CreateSut(
        FakeClipboardService? clipboard = null,
        FakeShareService? share = null)
    {
        var catalog = new StubCatalogService("Hexahue");
        return new HexahueViewModel(
            catalog,
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            clipboard ?? new FakeClipboardService(),
            share ?? new FakeShareService());
    }

    [TestMethod]
    public void Typing_Text_EncodesToGlyphsLive()
    {
        var sut = CreateSut();

        sut.InputText = "AB";

        Assert.AreEqual(2, sut.Glyphs.Count);
        Assert.AreEqual('A', sut.Glyphs[0].Character);
        Assert.AreEqual('B', sut.Glyphs[1].Character);
        Assert.IsTrue(sut.HasOutput);
    }

    [TestMethod]
    public void Encode_OutputText_IsNormalizedDecodedString()
    {
        var sut = CreateSut();

        sut.InputText = "cache";

        // In encode mode the text output echoes the normalized (upper-cased) plain text.
        Assert.AreEqual("CACHE", sut.OutputText);
    }

    [TestMethod]
    public void Encode_UnsupportedCharacter_RaisesWarning()
    {
        var sut = CreateSut();

        sut.InputText = "A#";

        Assert.IsTrue(sut.HasWarning);
        Assert.AreEqual(1, sut.Glyphs.Count);
    }

    [TestMethod]
    public void Encode_AllSupported_HasNoWarning()
    {
        var sut = CreateSut();

        sut.InputText = "HELLO";

        Assert.IsFalse(sut.HasWarning);
    }

    [TestMethod]
    public void SwapDirection_CarriesResultIntoInput()
    {
        var sut = CreateSut();
        sut.InputText = "GC";

        // Switch to decode-by-character mode; the previous normalized result becomes the new input.
        sut.DirectionIndex = 1;

        Assert.AreEqual("GC", sut.InputText);
    }

    [TestMethod]
    public void Decode_GlyphLikeInput_ReturnsPlainText()
    {
        var sut = CreateSut();
        sut.DirectionIndex = 1; // decode: characters chosen via the chart map back to text

        sut.InputText = "HI";

        // Decode mode still shows the glyphs for the chosen characters and the plain text result.
        Assert.AreEqual("HI", sut.OutputText);
        Assert.AreEqual(2, sut.Glyphs.Count);
    }

    [TestMethod]
    public void Copy_PutsResultOnClipboard()
    {
        var clipboard = new FakeClipboardService();
        var sut = CreateSut(clipboard: clipboard);
        sut.InputText = "FOUND";

        sut.CopyOutputCommand.Execute(null);

        Assert.AreEqual("FOUND", clipboard.LastText);
    }

    [TestMethod]
    public void Copy_DisabledWhenNoOutput()
    {
        var sut = CreateSut();

        Assert.IsFalse(sut.CopyOutputCommand.CanExecute(null));

        sut.InputText = "X";
        Assert.IsTrue(sut.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void Share_SharesResult()
    {
        var share = new FakeShareService();
        var sut = CreateSut(share: share);
        sut.InputText = "ABC";

        sut.ShareOutputCommand.Execute(null);

        Assert.AreEqual(1, share.ShareTextCallCount);
        Assert.AreEqual("ABC", share.LastText);
    }

    [TestMethod]
    public void Clear_EmptiesInputAndOutput()
    {
        var sut = CreateSut();
        sut.InputText = "ABC";

        sut.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, sut.InputText);
        Assert.AreEqual(string.Empty, sut.OutputText);
        Assert.AreEqual(0, sut.Glyphs.Count);
        Assert.IsFalse(sut.HasOutput);
    }

    [TestMethod]
    public void InsertChart_AppendsCharacterToInput()
    {
        var sut = CreateSut();
        var entry = HexahueCodec.GetAlphabet().First(e => e.Character == 'Q');

        sut.InsertCommand.Execute(entry);

        Assert.AreEqual("Q", sut.InputText);
        Assert.AreEqual('Q', sut.Glyphs[0].Character);
    }

    [TestMethod]
    public void Chart_ExposesGroupedReferenceEntries()
    {
        var sut = CreateSut();

        Assert.AreEqual(26, sut.LetterChart.Count);
        Assert.AreEqual(10, sut.DigitChart.Count);
        // Period, comma and space make up the punctuation/space chart section.
        Assert.AreEqual(3, sut.PunctuationChart.Count);
    }

    [TestMethod]
    public void GlyphItem_AutomationName_IsTheDecodedCharacter()
    {
        var sut = CreateSut();
        sut.InputText = "A";

        Assert.AreEqual("A", sut.Glyphs[0].AutomationName);
    }
}
