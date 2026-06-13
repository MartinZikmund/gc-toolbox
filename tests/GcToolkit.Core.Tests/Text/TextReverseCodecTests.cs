using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class TextReverseCodecTests
{
    private readonly TextReverseCodec _codec = new();

    // ---- Mode: Reverse text (whole string) ----

    [DataTestMethod]
    [DataRow("abc def", "fed cba")]
    [DataRow("Hello", "olleH")]
    [DataRow("", "")]
    public void Transform_ReverseText_ReversesWholeString(string input, string expected)
        => Assert.AreEqual(expected, _codec.Transform(input, new TextReverseOptions(TextReverseMode.ReverseText)));

    [TestMethod]
    public void Transform_ReverseText_ReversesAcrossLinesAsOneString()
    {
        // The whole string reverses, so the last line's content surfaces first (newline included).
        var result = _codec.Transform("ab\ncd", new TextReverseOptions(TextReverseMode.ReverseText));
        Assert.AreEqual("dc\nba", result);
    }

    // ---- Mode: Reverse text per line ----

    [TestMethod]
    public void Transform_ReverseTextPerLine_ReversesEachLineIndependently()
    {
        var result = _codec.Transform("abc\ndef", new TextReverseOptions(TextReverseMode.ReverseTextPerLine));
        Assert.AreEqual("cba\nfed", result);
    }

    [TestMethod]
    public void Transform_ReverseTextPerLine_PreservesCrLfNewlines()
    {
        var result = _codec.Transform("abc\r\ndef", new TextReverseOptions(TextReverseMode.ReverseTextPerLine));
        Assert.AreEqual("cba\r\nfed", result);
    }

    // ---- Mode: Reverse word order ----

    [DataTestMethod]
    [DataRow("one two three", "three two one")]
    [DataRow("abc def", "def abc")]
    public void Transform_ReverseWordOrder_ReversesWords(string input, string expected)
        => Assert.AreEqual(expected, _codec.Transform(input, new TextReverseOptions(TextReverseMode.ReverseWordOrder)));

    [TestMethod]
    public void Transform_ReverseWordOrder_CollapsesAcrossLines()
    {
        // Whole-string word order: words from later lines come first, single-space joined.
        var result = _codec.Transform("a b\nc d", new TextReverseOptions(TextReverseMode.ReverseWordOrder));
        Assert.AreEqual("d c b a", result);
    }

    // ---- Mode: Reverse word order per line ----

    [TestMethod]
    public void Transform_ReverseWordOrderPerLine_ReversesWordsWithinEachLine()
    {
        var result = _codec.Transform("a b\nc d", new TextReverseOptions(TextReverseMode.ReverseWordOrderPerLine));
        Assert.AreEqual("b a\nd c", result);
    }

    // ---- Mode: Reverse character order per word ----

    [DataTestMethod]
    [DataRow("abc def", "cba fed")]
    [DataRow("Hello World", "olleH dlroW")]
    public void Transform_ReverseCharactersPerWord_ReversesEachWord(string input, string expected)
        => Assert.AreEqual(expected, _codec.Transform(input, new TextReverseOptions(TextReverseMode.ReverseCharactersPerWord)));

    [TestMethod]
    public void Transform_ReverseCharactersPerWord_KeepsWordOrderAndWhitespace()
    {
        var result = _codec.Transform("one two\nthree", new TextReverseOptions(TextReverseMode.ReverseCharactersPerWord));
        Assert.AreEqual("eno owt\neerht", result);
    }

    // ---- Mode: Random character order per word (seeded, deterministic) ----

    [TestMethod]
    public void Transform_RandomCharactersPerWord_WithSeed_IsDeterministic()
    {
        var options = new TextReverseOptions(TextReverseMode.RandomCharactersPerWord, Seed: 42);
        var first = _codec.Transform("geocaching", options);
        var second = _codec.Transform("geocaching", options);
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Transform_RandomCharactersPerWord_IsAnagramOfEachWord()
    {
        var result = _codec.Transform("geocaching cache", new TextReverseOptions(TextReverseMode.RandomCharactersPerWord, Seed: 7));
        var words = result.Split(' ');
        CollectionAssert.AreEquivalent("geocaching".ToCharArray(), words[0].ToCharArray());
        CollectionAssert.AreEquivalent("cache".ToCharArray(), words[1].ToCharArray());
    }

    [TestMethod]
    public void Transform_RandomCharactersPerWord_KeepFirstAndLast_FixesEnds()
    {
        // Typoglycemia: first and last characters stay; only the middle shuffles.
        var options = new TextReverseOptions(
            TextReverseMode.RandomCharactersPerWord,
            Modifiers: TextReverseModifiers.KeepFirstAndLastCharacter,
            Seed: 123);
        var result = _codec.Transform("geocaching", options);
        Assert.AreEqual('g', result[0]);
        Assert.AreEqual('g', result[^1]);
        CollectionAssert.AreEquivalent("geocaching".ToCharArray(), result.ToCharArray());
    }

    // ---- Mode: Upside down + reverse sub-option ----

    [TestMethod]
    public void Transform_UpsideDown_FlipsLettersInPlace()
    {
        // Default upside-down keeps reading order; each letter is replaced by its rotated glyph.
        var result = _codec.Transform("abc", new TextReverseOptions(TextReverseMode.UpsideDown));
        Assert.AreEqual("ɐqɔ", result);
    }

    [TestMethod]
    public void Transform_UpsideDown_WithReverse_ReadsCorrectlyWhenFlipped()
    {
        // The "reverse text" sub-option also reverses, so a phone rotated 180° reads it normally.
        var flipped = _codec.Transform("abc", new TextReverseOptions(TextReverseMode.UpsideDown, ReverseUpsideDown: true));
        Assert.AreEqual("ɔqɐ", flipped);
    }

    [TestMethod]
    public void Transform_UpsideDown_RoundTripsThroughDeFlip()
    {
        // Flipping twice (with reversal both ways) returns the original lowercased text.
        const string original = "geocache";
        var flipped = _codec.Transform(original, new TextReverseOptions(TextReverseMode.UpsideDown, ReverseUpsideDown: true));
        var back = _codec.Transform(flipped, new TextReverseOptions(TextReverseMode.UpsideDown, ReverseUpsideDown: true));
        Assert.AreEqual(original, back);
    }

    // ---- Grapheme correctness ----

    [TestMethod]
    public void Transform_ReverseText_KeepsMultiCodepointEmojiIntact()
    {
        // Family emoji (ZWJ sequence) is a single grapheme and must not be torn apart.
        const string family = "\U0001F468‍\U0001F469‍\U0001F467";
        var input = $"a{family}b";
        var result = _codec.Transform(input, new TextReverseOptions(TextReverseMode.ReverseText));
        Assert.AreEqual($"b{family}a", result);
    }

    [TestMethod]
    public void Transform_ReverseText_KeepsCombiningDiacriticIntact()
    {
        // "é" as e + combining acute (U+0301) is one grapheme; it must stay attached to its base.
        const string eAcute = "é";
        var result = _codec.Transform($"a{eAcute}b", new TextReverseOptions(TextReverseMode.ReverseText));
        Assert.AreEqual($"b{eAcute}a", result);
    }

    // ---- Modifier: Keep upper case location ----

    [TestMethod]
    public void Transform_KeepUpperCaseLocation_ReappliesOriginalCasePositions()
    {
        // "AbC" reversed is "CbA"; keeping upper-case slots 0 and 2 yields "CbA" -> positions 0,2 upper.
        var options = new TextReverseOptions(
            TextReverseMode.ReverseText,
            Modifiers: TextReverseModifiers.KeepUpperCaseLocation);
        var result = _codec.Transform("AbC", options);
        Assert.AreEqual("CbA", result);
    }

    [TestMethod]
    public void Transform_KeepUpperCaseLocation_MovesCaseBackToOriginalSlots()
    {
        // "Hello" -> reversed "olleH"; original uppercase at index 0 -> reapply: "Olleh".
        var options = new TextReverseOptions(
            TextReverseMode.ReverseText,
            Modifiers: TextReverseModifiers.KeepUpperCaseLocation);
        var result = _codec.Transform("Hello", options);
        Assert.AreEqual("Olleh", result);
    }

    // ---- Modifier: Change upper and lower case (swap) ----

    [TestMethod]
    public void Transform_SwapCase_InvertsEachLetterCase()
    {
        var options = new TextReverseOptions(TextReverseMode.ReverseText, Modifiers: TextReverseModifiers.SwapCase);
        // "AbC" reversed is "CbA"; swapped is "cBa".
        Assert.AreEqual("cBa", _codec.Transform("AbC", options));
    }

    // ---- Modifier: Convert to upper / lower ----

    [TestMethod]
    public void Transform_ToUpperCase_UppercasesResult()
    {
        var options = new TextReverseOptions(TextReverseMode.ReverseText, Modifiers: TextReverseModifiers.ToUpperCase);
        Assert.AreEqual("FED CBA", _codec.Transform("abc def", options));
    }

    [TestMethod]
    public void Transform_ToLowerCase_LowercasesResult()
    {
        var options = new TextReverseOptions(TextReverseMode.ReverseText, Modifiers: TextReverseModifiers.ToLowerCase);
        Assert.AreEqual("fed cba", _codec.Transform("ABC DEF", options));
    }

    // ---- Modifier: Remove unknown characters ----

    [TestMethod]
    public void Transform_RemoveUnknown_StripsUnmappableUpsideDownGlyphs()
    {
        // The upside-down map has no entry for the section sign "§"; remove-unknown drops it.
        var options = new TextReverseOptions(
            TextReverseMode.UpsideDown,
            Modifiers: TextReverseModifiers.RemoveUnknownCharacters);
        var result = _codec.Transform("a§b", options);
        Assert.AreEqual("ɐq", result);
    }

    [TestMethod]
    public void Transform_WithoutRemoveUnknown_KeepsUnmappableGlyph()
    {
        // Without the modifier, an unmappable glyph passes through unchanged.
        var result = _codec.Transform("a§b", new TextReverseOptions(TextReverseMode.UpsideDown));
        Assert.AreEqual("ɐ§q", result);
    }
}
