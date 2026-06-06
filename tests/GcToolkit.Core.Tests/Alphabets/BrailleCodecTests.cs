using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class BrailleCodecTests
{
    private readonly BrailleCodec _codec = new();

    // ---- Letters: lowercase a–z map to the standard six-dot cells ----

    [DataTestMethod]
    [DataRow("a", "⠁")]
    [DataRow("b", "⠃")]
    [DataRow("c", "⠉")]
    [DataRow("z", "⠵")]            // dots 1-3-5-6
    [DataRow("abc", "⠁⠃⠉")]
    public void Encode_Letters_MapsToBrailleCells(string text, string expected)
        => Assert.AreEqual(expected, _codec.Encode(text));

    [TestMethod]
    public void Encode_Letters_IsCaseFoldedWithCapitalSign()
        // "A" = capital sign (dot 6, U+2820) + a (U+2801).
        => Assert.AreEqual("⠠⠁", _codec.Encode("A"));

    [TestMethod]
    public void Encode_Cache_CapitalisesCorrectly()
    {
        // Capital sign precedes only the capitalised letter, then the rest are plain.
        var result = _codec.Encode("Cache");
        Assert.AreEqual("⠠⠉⠁⠉⠓⠑", result);
    }

    // ---- Digits: number sign then a–j = 1–0 ----

    [TestMethod]
    public void Encode_SingleDigit_EmitsNumberSignThenLetter()
        // "1" = number sign (dots 3-4-5-6, U+283C) + a (U+2801).
        => Assert.AreEqual("⠼⠁", _codec.Encode("1"));

    [TestMethod]
    public void Encode_Zero_MapsToJ()
        // "0" = number sign + j (dots 2-4-5 = U+281A).
        => Assert.AreEqual("⠼⠚", _codec.Encode("0"));

    [TestMethod]
    public void Encode_DigitRun_EmitsOneNumberSignForTheRun()
        // A run of digits takes a single leading number sign: "123" -> # a b c.
        => Assert.AreEqual("⠼⠁⠃⠉", _codec.Encode("123"));

    [TestMethod]
    public void Encode_DigitsSeparatedBySpace_RestartTheNumberMode()
    {
        // Space ends number mode, so the second run needs its own number sign.
        var result = _codec.Encode("1 2");
        Assert.AreEqual("⠼⠁⠀⠼⠃", result);
    }

    // ---- Space ----

    [TestMethod]
    public void Encode_Space_MapsToBlankCell()
        => Assert.AreEqual("⠀", _codec.Encode(" "));

    // ---- Punctuation ----

    [TestMethod]
    public void Encode_Period_MapsToLiteraryCell()
        // Period = dots 2-5-6 = U+2832.
        => Assert.AreEqual("⠲", _codec.Encode("."));

    [DataTestMethod]
    [DataRow(",", "⠂")]            // dot 2
    [DataRow(";", "⠆")]            // dots 2-3
    [DataRow(":", "⠒")]            // dots 2-5
    [DataRow("?", "⠦")]            // dots 2-3-6
    [DataRow("!", "⠖")]            // dots 2-3-5
    [DataRow("'", "⠄")]            // dot 3
    [DataRow("-", "⠤")]            // dots 3-6
    public void Encode_Punctuation_MapsToLiteraryCells(string text, string expected)
        => Assert.AreEqual(expected, _codec.Encode(text));

    [TestMethod]
    public void Encode_Sentence_WithPunctuation()
    {
        // "Hi!" -> capital sign + h + i + exclamation.
        var result = _codec.Encode("Hi!");
        Assert.AreEqual("⠠⠓⠊⠖", result);
    }

    // ---- Decode reverses the indicators ----

    [TestMethod]
    public void Decode_CapitalSign_ProducesUpperCaseLetter()
        => Assert.AreEqual("A", _codec.Decode("⠠⠁"));

    [TestMethod]
    public void Decode_NumberSign_ProducesDigits()
        => Assert.AreEqual("123", _codec.Decode("⠼⠁⠃⠉"));

    [TestMethod]
    public void Decode_NumberRunEndsAtSpace()
        // After the blank cell, a-c are letters again, not digits.
        => Assert.AreEqual("1 abc", _codec.Decode("⠼⠁⠀⠁⠃⠉"));

    [TestMethod]
    public void Decode_PlainCells_ProducesLowercaseText()
        => Assert.AreEqual("abc", _codec.Decode("⠁⠃⠉"));

    [TestMethod]
    public void Decode_UnknownCharacters_PassThroughUnchanged()
        // A non-braille char in a decode stream is preserved (best-effort, like the encode side).
        => Assert.AreEqual("a§b", _codec.Decode("⠁§⠃"));

    // ---- Round-trips ----

    [TestMethod]
    public void RoundTrip_LettersDigitsAndSpaces_IsLossless()
    {
        const string plain = "the quick brown fox 42 and 0";
        Assert.AreEqual(plain, _codec.Decode(_codec.Encode(plain)));
    }

    [TestMethod]
    public void RoundTrip_MixedCaseSentence_IsLossless()
    {
        const string plain = "Cache GC1234 found";
        Assert.AreEqual(plain, _codec.Decode(_codec.Encode(plain)));
    }

    [DataTestMethod]
    [DataRow(".")]
    [DataRow(",")]
    [DataRow(";")]
    [DataRow(":")]
    [DataRow("!")]
    [DataRow("-")]
    [DataRow("'")]
    public void RoundTrip_Punctuation_IsLossless(string punctuation)
    {
        var plain = $"go{punctuation} now";
        Assert.AreEqual(plain, _codec.Decode(_codec.Encode(plain)));
    }

    // ---- Empty / null ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encode_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _codec.Encode(text));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Decode_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _codec.Decode(text));

    // ---- Dot-number display (beyond-parity feature) ----

    [TestMethod]
    public void DescribeDots_Letter_ListsDotNumbers()
        => Assert.AreEqual("1-3-4", _codec.DescribeDots("m"));

    [TestMethod]
    public void DescribeDots_BlankCell_IsEmptyMarker()
        // The blank cell (space) has no raised dots.
        => Assert.AreEqual(BrailleCodec.BlankDots, _codec.DescribeDots(" "));

    [TestMethod]
    public void DescribeDots_NumberOne_ShowsNumberSignThenA()
    {
        // "1" renders the number sign cell (3-4-5-6) then a (1).
        var result = _codec.DescribeDots("1");
        Assert.AreEqual("3-4-5-6 1", result);
    }

    [TestMethod]
    public void DescribeDots_NonBrailleInput_DescribesEncodedForm()
    {
        // Describe operates on the encoded braille, so capitals expand to capital-sign + letter.
        var result = _codec.DescribeDots("A");
        Assert.AreEqual("6 1", result);
    }
}
