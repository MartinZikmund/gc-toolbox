using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class NumberWordsCodecTests
{
    private readonly NumberWordsCodec _codec = new();

    // ---- Encode: known vectors (cardinal, English) ----

    [DataTestMethod]
    [DataRow(0L, "ZERO")]
    [DataRow(1L, "ONE")]
    [DataRow(7L, "SEVEN")]
    [DataRow(10L, "TEN")]
    [DataRow(13L, "THIRTEEN")]
    [DataRow(20L, "TWENTY")]
    [DataRow(21L, "TWENTY-ONE")]
    [DataRow(99L, "NINETY-NINE")]
    [DataRow(100L, "ONE HUNDRED")]
    [DataRow(101L, "ONE HUNDRED ONE")]
    [DataRow(354L, "THREE HUNDRED FIFTY-FOUR")]
    [DataRow(1000L, "ONE THOUSAND")]
    [DataRow(1984L, "ONE THOUSAND NINE HUNDRED EIGHTY-FOUR")]
    [DataRow(1000000L, "ONE MILLION")]
    [DataRow(354000000000000L, "THREE HUNDRED FIFTY-FOUR TRILLION")]
    public void Encode_KnownCardinals_ProducesEnglishWords(long value, string expected)
        => Assert.AreEqual(expected, _codec.Encode(value));

    [TestMethod]
    public void Encode_Negative_PrependsMinus()
        => Assert.AreEqual("MINUS FORTY-TWO", _codec.Encode(-42));

    // ---- Encode: short-scale magnitudes up to nonillion (10^30) ----

    [DataTestMethod]
    [DataRow("1000000000", "ONE BILLION")]
    [DataRow("1000000000000", "ONE TRILLION")]
    [DataRow("1000000000000000", "ONE QUADRILLION")]
    [DataRow("1000000000000000000", "ONE QUINTILLION")]
    [DataRow("1000000000000000000000", "ONE SEXTILLION")]
    [DataRow("1000000000000000000000000", "ONE SEPTILLION")]
    [DataRow("1000000000000000000000000000", "ONE OCTILLION")]
    [DataRow("1000000000000000000000000000000", "ONE NONILLION")]
    public void EncodeBig_ShortScaleMagnitudes_NameTheCorrectScale(string value, string expected)
        => Assert.AreEqual(expected, _codec.EncodeBig(System.Numerics.BigInteger.Parse(value)));

    [TestMethod]
    public void EncodeBig_NonillionOverflow_ReturnsFalseFromTryEncode()
    {
        // 10^33 exceeds the nonillion (10^30) scale and has no named group.
        var tooBig = System.Numerics.BigInteger.Pow(10, 33);
        Assert.IsFalse(_codec.TryEncodeBig(tooBig, NumberWordsOptions.Default, out _));
    }

    // ---- Encode: "and" styles (British vs American) ----

    [TestMethod]
    public void Encode_BritishAndStyle_InsertsAndBeforeTensInFinalGroup()
    {
        var options = NumberWordsOptions.Default with { UseAnd = true };
        Assert.AreEqual("ONE HUNDRED AND ONE", _codec.Encode(101, options));
        Assert.AreEqual("THREE HUNDRED AND FIFTY-FOUR", _codec.Encode(354, options));
        Assert.AreEqual("ONE THOUSAND AND ONE", _codec.Encode(1001, options));
    }

    [TestMethod]
    public void Encode_BritishAndStyle_NoAndWhenNoRemainderUnderHundred()
        => Assert.AreEqual("ONE HUNDRED", _codec.Encode(100, NumberWordsOptions.Default with { UseAnd = true }));

    // ---- Encode: ordinals ----

    [DataTestMethod]
    [DataRow(1L, "FIRST")]
    [DataRow(2L, "SECOND")]
    [DataRow(3L, "THIRD")]
    [DataRow(5L, "FIFTH")]
    [DataRow(8L, "EIGHTH")]
    [DataRow(9L, "NINTH")]
    [DataRow(12L, "TWELFTH")]
    [DataRow(20L, "TWENTIETH")]
    [DataRow(21L, "TWENTY-FIRST")]
    [DataRow(23L, "TWENTY-THIRD")]
    [DataRow(100L, "ONE HUNDREDTH")]
    [DataRow(1000000L, "ONE MILLIONTH")]
    public void Encode_OrdinalMode_ProducesOrdinalWords(long value, string expected)
        => Assert.AreEqual(expected, _codec.Encode(value, NumberWordsOptions.Default with { Mode = NumberWordsMode.Ordinal }));

    // ---- Encode: year reading ----

    [DataTestMethod]
    [DataRow(1984L, "NINETEEN EIGHTY-FOUR")]
    [DataRow(1900L, "NINETEEN HUNDRED")]
    [DataRow(2000L, "TWO THOUSAND")]
    [DataRow(2007L, "TWENTY OH-SEVEN")]
    [DataRow(2023L, "TWENTY TWENTY-THREE")]
    [DataRow(1066L, "TEN SIXTY-SIX")]
    [DataRow(1805L, "EIGHTEEN OH-FIVE")]
    public void Encode_YearMode_ReadsAsPairedTens(long value, string expected)
        => Assert.AreEqual(expected, _codec.Encode(value, NumberWordsOptions.Default with { Mode = NumberWordsMode.Year }));

    // ---- Encode: decimals ----

    [TestMethod]
    public void EncodeDecimal_FractionalDigitsReadIndividually()
    {
        Assert.AreEqual("THREE POINT ONE FOUR", _codec.EncodeDecimal(3.14m));
        Assert.AreEqual("ZERO POINT FIVE", _codec.EncodeDecimal(0.5m));
        Assert.AreEqual("MINUS TWO POINT ZERO ONE", _codec.EncodeDecimal(-2.01m));
    }

    [TestMethod]
    public void EncodeDecimal_WholeNumber_HasNoPoint()
        => Assert.AreEqual("FORTY-TWO", _codec.EncodeDecimal(42m));

    // ---- Czech spelling ----

    [DataTestMethod]
    [DataRow(0L, "NULA")]
    [DataRow(1L, "JEDNA")]
    [DataRow(15L, "PATNÁCT")]
    [DataRow(21L, "DVACET JEDNA")]
    [DataRow(100L, "STO")]
    [DataRow(354L, "TŘI STA PADESÁT ČTYŘI")]
    [DataRow(1000L, "TISÍC")]
    [DataRow(2000L, "DVA TISÍCE")]
    [DataRow(5000L, "PĚT TISÍC")]
    [DataRow(1000000L, "MILION")]
    public void Encode_CzechSpelling_ProducesCzechWords(long value, string expected)
        => Assert.AreEqual(expected, _codec.Encode(value, NumberWordsOptions.Default with { Language = NumberWordsLanguage.Czech }));

    [TestMethod]
    public void Encode_CzechNegative_PrependsMinus()
        => Assert.AreEqual("MÍNUS PĚT", _codec.Encode(-5, NumberWordsOptions.Default with { Language = NumberWordsLanguage.Czech }));

    // ---- Decode: words -> number ----

    [DataTestMethod]
    [DataRow("zero", 0L)]
    [DataRow("twenty-one", 21L)]
    [DataRow("one hundred", 100L)]
    [DataRow("one thousand", 1000L)]
    [DataRow("three hundred fifty-four", 354L)]
    [DataRow("THREE HUNDRED FIFTY-FOUR TRILLION", 354000000000000L)]
    public void TryDecode_EnglishWords_ReturnsNumber(string words, long expected)
    {
        Assert.IsTrue(_codec.TryDecode(words, out var value));
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void TryDecode_LenientIgnoresAndCommasAndHyphens()
    {
        Assert.IsTrue(_codec.TryDecode("three hundred and fifty-four", out var value));
        Assert.AreEqual(354L, value);

        Assert.IsTrue(_codec.TryDecode("one thousand, two hundred and thirty-four", out var withCommas));
        Assert.AreEqual(1234L, withCommas);
    }

    [TestMethod]
    public void TryDecode_Negative_ReturnsNegativeNumber()
    {
        Assert.IsTrue(_codec.TryDecode("minus forty-two", out var value));
        Assert.AreEqual(-42L, value);
        Assert.IsTrue(_codec.TryDecode("negative seven", out var negative));
        Assert.AreEqual(-7L, negative);
    }

    [TestMethod]
    public void TryDecode_Ordinals_ParseToTheirCardinalValue()
    {
        Assert.IsTrue(_codec.TryDecode("twenty-third", out var value));
        Assert.AreEqual(23L, value);
        Assert.IsTrue(_codec.TryDecode("first", out var first));
        Assert.AreEqual(1L, first);
    }

    [DataTestMethod]
    [DataRow("banana")]
    [DataRow("hundred frog")]
    [DataRow("")]
    [DataRow("   ")]
    public void TryDecode_InvalidWords_ReturnsFalse(string words)
        => Assert.IsFalse(_codec.TryDecode(words, out _));

    [TestMethod]
    public void TryDecode_Null_ReturnsFalse()
        => Assert.IsFalse(_codec.TryDecode(null, out _));

    [DataTestMethod]
    [DataRow("one sextillion")]
    [DataRow("one septillion")]
    [DataRow("one octillion")]
    [DataRow("one nonillion")]
    [DataRow("two sextillion three")]
    public void TryDecode_ScalesAboveLongRange_ReturnFalseNotGarbage(string words)
    {
        // These exceed long.MaxValue; decode must fail cleanly rather than overflow to a wrong value.
        Assert.IsFalse(_codec.TryDecode(words, out var value));
        Assert.AreEqual(0L, value);
    }

    // ---- Round trip ----

    [DataTestMethod]
    [DataRow(0L)]
    [DataRow(7L)]
    [DataRow(21L)]
    [DataRow(100L)]
    [DataRow(354L)]
    [DataRow(1000L)]
    [DataRow(1984L)]
    [DataRow(1234567L)]
    [DataRow(354000000000000L)]
    [DataRow(-42L)]
    public void RoundTrip_EncodeThenDecode_PreservesValue(long value)
    {
        Assert.IsTrue(_codec.TryDecode(_codec.Encode(value), out var decoded));
        Assert.AreEqual(value, decoded);
    }

    // ---- Auto-detect direction ----

    [TestMethod]
    public void Convert_DigitsInput_ProducesWords()
    {
        var result = _codec.Convert("354", NumberWordsOptions.Default);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("THREE HUNDRED FIFTY-FOUR", result.Text);
        Assert.AreEqual(NumberWordsDirection.NumberToWords, result.Direction);
    }

    [TestMethod]
    public void Convert_WordsInput_ProducesNumber()
    {
        var result = _codec.Convert("three hundred fifty-four", NumberWordsOptions.Default);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("354", result.Text);
        Assert.AreEqual(NumberWordsDirection.WordsToNumber, result.Direction);
    }

    [TestMethod]
    public void Convert_InvalidInput_ReportsInvalid()
    {
        var result = _codec.Convert("not a number", NumberWordsOptions.Default);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void Convert_NegativeDigits_ProducesWords()
    {
        var result = _codec.Convert("-42", NumberWordsOptions.Default);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("MINUS FORTY-TWO", result.Text);
    }

    [TestMethod]
    public void Convert_DecimalDigits_ProducesWords()
    {
        var result = _codec.Convert("3.14", NumberWordsOptions.Default);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("THREE POINT ONE FOUR", result.Text);
    }

    [TestMethod]
    public void Convert_DigitsWithGroupingCommas_ParseAsOneNumber()
    {
        var result = _codec.Convert("1,234", NumberWordsOptions.Default);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("ONE THOUSAND TWO HUNDRED THIRTY-FOUR", result.Text);
    }

    // ---- Solver helpers ----

    [TestMethod]
    public void LetterCounts_ReturnCountPerWord_IgnoringNonLetters()
    {
        // "TWENTY-ONE" -> TWENTY (6) + ONE (3); hyphen splits into words.
        var counts = NumberWordsCodec.LetterCountsPerWord("TWENTY-ONE");
        CollectionAssert.AreEqual(new[] { 6, 3 }, counts.ToArray());
    }

    [TestMethod]
    public void TotalLetterCount_CountsOnlyLetters()
        => Assert.AreEqual(9, NumberWordsCodec.TotalLetterCount("TWENTY-ONE"));

    [TestMethod]
    public void FirstLetters_ExtractsInitialOfEachWord()
        => Assert.AreEqual("THFF", NumberWordsCodec.FirstLetters("THREE HUNDRED FIFTY-FOUR"));

    // ---- Batch ----

    [TestMethod]
    public void ConvertBatch_PerLine_ConvertsEachIndependently()
    {
        var lines = _codec.ConvertBatch("1\n21\none hundred", NumberWordsOptions.Default);
        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual("ONE", lines[0].Text);
        Assert.AreEqual("TWENTY-ONE", lines[1].Text);
        Assert.AreEqual("100", lines[2].Text);
    }

    [TestMethod]
    public void ConvertBatch_InvalidLine_FlaggedButOthersSucceed()
    {
        var lines = _codec.ConvertBatch("5\nbogus\nten", NumberWordsOptions.Default);
        Assert.AreEqual(3, lines.Count);
        Assert.IsTrue(lines[0].IsValid);
        Assert.IsFalse(lines[1].IsValid);
        Assert.IsTrue(lines[2].IsValid);
    }
}
