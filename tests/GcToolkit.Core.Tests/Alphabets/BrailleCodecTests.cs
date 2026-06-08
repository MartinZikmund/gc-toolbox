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

    // ---- Dot-grid model (BrailleDots) for XAML-shape rendering ----

    [TestMethod]
    public void BrailleDots_FromCell_A_HasOnlyDotOne()
    {
        var dots = BrailleDots.FromCell('⠁'); // letter a = dot 1
        Assert.IsTrue(dots.Dot1);
        Assert.IsFalse(dots.Dot2 || dots.Dot3 || dots.Dot4 || dots.Dot5 || dots.Dot6);
    }

    [TestMethod]
    public void BrailleDots_FromCell_M_HasDots134()
    {
        var dots = BrailleDots.FromCell('⠍'); // m = dots 1-3-4
        Assert.IsTrue(dots.Dot1 && dots.Dot3 && dots.Dot4);
        Assert.IsFalse(dots.Dot2 || dots.Dot5 || dots.Dot6);
    }

    [TestMethod]
    public void BrailleDots_FromCell_NumberSign_HasDots3456()
    {
        var dots = BrailleDots.FromCell(BrailleCodec.NumberSign); // dots 3-4-5-6
        Assert.IsTrue(dots.Dot3 && dots.Dot4 && dots.Dot5 && dots.Dot6);
        Assert.IsFalse(dots.Dot1 || dots.Dot2);
    }

    [TestMethod]
    public void BrailleDots_FromCell_BlankCell_HasNoDots()
    {
        var dots = BrailleDots.FromCell(BrailleCodec.Blank);
        Assert.IsFalse(dots.Dot1 || dots.Dot2 || dots.Dot3 || dots.Dot4 || dots.Dot5 || dots.Dot6);
    }

    [TestMethod]
    public void BrailleDots_FromCell_NonBraille_HasNoDots()
    {
        var dots = BrailleDots.FromCell('x');
        Assert.IsFalse(dots.Dot1 || dots.Dot2 || dots.Dot3 || dots.Dot4 || dots.Dot5 || dots.Dot6);
    }

    // ---- ToGlyphs: one dot-grid glyph per output cell ----

    [TestMethod]
    public void ToGlyphs_EncodedText_YieldsOneGlyphPerCell()
    {
        var glyphs = BrailleCodec.ToGlyphs(_codec.Encode("ab")); // ⠁⠃
        Assert.AreEqual(2, glyphs.Count);
        Assert.AreEqual('⠁', glyphs[0].Cell);
        Assert.IsTrue(glyphs[0].Dots.Dot1);
        Assert.AreEqual("1", glyphs[0].DotNumbers);
        Assert.AreEqual("1-2", glyphs[1].DotNumbers);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ToGlyphs_EmptyOrNull_YieldsNoGlyphs(string? braille)
        => Assert.AreEqual(0, BrailleCodec.ToGlyphs(braille).Count);

    // ---- GetAlphabet: the clickable reference chart (geocachingtoolbox parity) ----

    [TestMethod]
    public void GetAlphabet_HasAllThirtyNineEntries()
        // 26 letters + capital + number + space + 10 punctuation marks = the 39 chart cells.
        => Assert.AreEqual(39, BrailleCodec.GetAlphabet().Count);

    [TestMethod]
    public void GetAlphabet_Letters_LabelDigitsForAtoJ()
    {
        var alphabet = BrailleCodec.GetAlphabet();
        Assert.AreEqual("a / 1", alphabet.First(e => e.Id == "a").Label);
        Assert.AreEqual("j / 0", alphabet.First(e => e.Id == "j").Label);
        Assert.AreEqual("k", alphabet.First(e => e.Id == "k").Label);
    }

    [TestMethod]
    public void GetAlphabet_Letter_CarriesTextCellAndDots()
    {
        var m = BrailleCodec.GetAlphabet().First(e => e.Id == "m");
        Assert.AreEqual("m", m.Text);       // typed into text→braille input
        Assert.AreEqual("⠍", m.Cell);       // typed into braille→text input
        Assert.IsTrue(m.Dots.Dot1 && m.Dots.Dot3 && m.Dots.Dot4);
    }

    [TestMethod]
    public void GetAlphabet_Indicators_UseBrailleCells()
    {
        var alphabet = BrailleCodec.GetAlphabet();
        var capital = alphabet.First(e => e.Id == "Capital");
        var number = alphabet.First(e => e.Id == "Number");
        var space = alphabet.First(e => e.Id == "Space");
        Assert.AreEqual(BrailleCodec.CapitalSign.ToString(), capital.Cell);
        Assert.AreEqual(BrailleCodec.NumberSign.ToString(), number.Cell);
        Assert.AreEqual(BrailleCodec.Blank.ToString(), space.Cell);
        Assert.AreEqual(" ", space.Text);
        // Capital/Number have no plain-text equivalent, so they only type as braille cells.
        Assert.AreEqual(string.Empty, capital.Text);
        Assert.AreEqual(string.Empty, number.Text);
    }

    [TestMethod]
    public void GetAlphabet_Indicators_CarryLocalizationKeys()
    {
        var alphabet = BrailleCodec.GetAlphabet();
        Assert.AreEqual("BrailleCapital", alphabet.First(e => e.Id == "Capital").LabelKey);
        Assert.AreEqual("BrailleNumber", alphabet.First(e => e.Id == "Number").LabelKey);
        Assert.AreEqual("BrailleSpace", alphabet.First(e => e.Id == "Space").LabelKey);
        // Letters/punctuation use their literal label, so no key.
        Assert.IsNull(alphabet.First(e => e.Id == "a").LabelKey);
    }

    [TestMethod]
    public void GetAlphabet_IncludesBothQuotesAsDistinctCells()
    {
        var alphabet = BrailleCodec.GetAlphabet();
        var open = alphabet.First(e => e.Id == "QuoteOpen");
        var close = alphabet.First(e => e.Id == "QuoteClose");
        Assert.AreEqual("⠦", open.Cell);  // dots 2-3-6 (shared with '?')
        Assert.AreEqual("⠴", close.Cell); // dots 3-5-6
    }

    // ---- Curly-quote encode aliases (chart uses “ ”) ----

    [DataTestMethod]
    [DataRow("“", "⠦")] // “ opening quote -> dots 2-3-6
    [DataRow("”", "⠴")] // ” closing quote -> dots 3-5-6
    public void Encode_CurlyQuotes_MapToQuoteCells(string text, string expected)
        => Assert.AreEqual(expected, _codec.Encode(text));
}
