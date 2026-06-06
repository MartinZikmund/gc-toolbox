using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class AsciiConverterTests
{
    private readonly AsciiConverter _converter = new();

    // ---- Encode: a single character across all four bases ----

    [TestMethod]
    [DataRow(AsciiNumberBase.Decimal, "65")]
    [DataRow(AsciiNumberBase.Hexadecimal, "41")]
    [DataRow(AsciiNumberBase.Octal, "101")]
    [DataRow(AsciiNumberBase.Binary, "1000001")]
    public void Encode_SingleLetterA_ProducesExpectedCode(AsciiNumberBase numberBase, string expected)
        => Assert.AreEqual(expected, _converter.Encode("A", numberBase));

    [TestMethod]
    public void Encode_TwoLetters_JoinsWithDefaultSpaceSeparator()
        => Assert.AreEqual("65 66", _converter.Encode("AB", AsciiNumberBase.Decimal));

    [TestMethod]
    public void Encode_Hex_TwoLetters()
        => Assert.AreEqual("48 69", _converter.Encode("Hi", AsciiNumberBase.Hexadecimal));

    [TestMethod]
    public void Encode_UsesCustomSeparator()
        => Assert.AreEqual("65,66,67", _converter.Encode("ABC", AsciiNumberBase.Decimal, ","));

    [TestMethod]
    public void Encode_EmptySeparator_ConcatenatesCodes()
        => Assert.AreEqual("656667", _converter.Encode("ABC", AsciiNumberBase.Decimal, ""));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encode_NullOrEmpty_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _converter.Encode(text, AsciiNumberBase.Decimal));

    // ---- Encode: full Unicode code points (beyond 0..127) ----

    [TestMethod]
    public void Encode_EuroSign_ProducesDecimal8364()
        => Assert.AreEqual("8364", _converter.Encode("€", AsciiNumberBase.Decimal));

    [TestMethod]
    public void Encode_EuroSign_ProducesHex20AC()
        => Assert.AreEqual("20AC", _converter.Encode("€", AsciiNumberBase.Hexadecimal));

    [TestMethod]
    public void Encode_AstralEmoji_IsOneCodePointNotTwoSurrogates()
    {
        // U+1F600 grinning face — a single code point of 128512, not two UTF-16 surrogate halves.
        Assert.AreEqual("128512", _converter.Encode("\U0001F600", AsciiNumberBase.Decimal));
    }

    [TestMethod]
    public void Encode_PreservesSpacesAsCodes()
        => Assert.AreEqual("72 105 32 65", _converter.Encode("Hi A", AsciiNumberBase.Decimal));

    // ---- Decode: codes back to text ----

    [TestMethod]
    public void TryDecode_Decimal_ProducesText()
    {
        Assert.IsTrue(_converter.TryDecode("72 105", AsciiNumberBase.Decimal, out var text));
        Assert.AreEqual("Hi", text);
    }

    [TestMethod]
    public void TryDecode_Hex_ProducesText()
    {
        Assert.IsTrue(_converter.TryDecode("48 69", AsciiNumberBase.Hexadecimal, out var text));
        Assert.AreEqual("Hi", text);
    }

    [TestMethod]
    public void TryDecode_Octal_ProducesText()
    {
        Assert.IsTrue(_converter.TryDecode("110 151", AsciiNumberBase.Octal, out var text));
        Assert.AreEqual("Hi", text);
    }

    [TestMethod]
    public void TryDecode_Binary_ProducesText()
    {
        Assert.IsTrue(_converter.TryDecode("1001000 1101001", AsciiNumberBase.Binary, out var text));
        Assert.AreEqual("Hi", text);
    }

    [TestMethod]
    public void TryDecode_EuroSignDecimal_ProducesEuroChar()
    {
        Assert.IsTrue(_converter.TryDecode("8364", AsciiNumberBase.Decimal, out var text));
        Assert.AreEqual("€", text);
    }

    [TestMethod]
    public void TryDecode_AstralCodePoint_ProducesEmoji()
    {
        Assert.IsTrue(_converter.TryDecode("128512", AsciiNumberBase.Decimal, out var text));
        Assert.AreEqual("\U0001F600", text);
    }

    [TestMethod]
    public void TryDecode_HexWith0xPrefix_IsTolerated()
    {
        Assert.IsTrue(_converter.TryDecode("0x48 0x69", AsciiNumberBase.Hexadecimal, out var text));
        Assert.AreEqual("Hi", text);
    }

    [TestMethod]
    public void TryDecode_RoundTripsArbitraryUnicodeText()
    {
        const string original = "Héllo, Wörld! ☺ €";
        var encoded = _converter.Encode(original, AsciiNumberBase.Decimal);
        Assert.IsTrue(_converter.TryDecode(encoded, AsciiNumberBase.Decimal, out var text));
        Assert.AreEqual(original, text);
    }

    [TestMethod]
    public void TryDecode_TolerantOfAnyWhitespaceRuns()
    {
        Assert.IsTrue(_converter.TryDecode("  72\t105\r\n  ", AsciiNumberBase.Decimal, out var text));
        Assert.AreEqual("Hi", text);
    }

    [TestMethod]
    public void TryDecode_AcceptsCommaSeparatedCodes()
    {
        Assert.IsTrue(_converter.TryDecode("72,105", AsciiNumberBase.Decimal, out var text));
        Assert.AreEqual("Hi", text);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void TryDecode_NullOrBlank_ReturnsTrueWithEmpty(string? codes)
    {
        Assert.IsTrue(_converter.TryDecode(codes, AsciiNumberBase.Decimal, out var text));
        Assert.AreEqual(string.Empty, text);
    }

    [TestMethod]
    public void TryDecode_InvalidTokenForBase_ReturnsFalse()
    {
        // 'abc' is not decimal; '4G' is not hexadecimal.
        Assert.IsFalse(_converter.TryDecode("abc", AsciiNumberBase.Decimal, out _));
        Assert.IsFalse(_converter.TryDecode("4G", AsciiNumberBase.Hexadecimal, out _));
    }

    [TestMethod]
    public void TryDecode_OutOfUnicodeRange_ReturnsFalse()
    {
        // Beyond U+10FFFF — not a valid code point.
        Assert.IsFalse(_converter.TryDecode("1114112", AsciiNumberBase.Decimal, out _));
    }

    // ---- Auto-detect base ----

    [TestMethod]
    [DataRow("72 105", AsciiNumberBase.Decimal)]
    [DataRow("1001000 1101001", AsciiNumberBase.Binary)]
    public void TryDecodeAuto_DetectsBaseAndDecodes(string codes, AsciiNumberBase expectedBase)
    {
        Assert.IsTrue(_converter.TryDecodeAuto(codes, out var text, out var detected));
        Assert.AreEqual(expectedBase, detected);
        Assert.AreEqual("Hi", text);
    }

    [TestMethod]
    public void TryDecodeAuto_HexLetters_DetectsHex()
    {
        // 'C' and '6C' are only valid as hexadecimal.
        Assert.IsTrue(_converter.TryDecodeAuto("48 65 6C 6C 6F", out var text, out var detected));
        Assert.AreEqual(AsciiNumberBase.Hexadecimal, detected);
        Assert.AreEqual("Hello", text);
    }

    [TestMethod]
    public void TryDecodeAuto_Garbage_ReturnsFalse()
        => Assert.IsFalse(_converter.TryDecodeAuto("hello world", out _, out _));

    // ---- Describe: all four bases at once (beyond parity) ----

    [TestMethod]
    public void Describe_SingleChar_ReturnsAllFourBases()
    {
        var rows = _converter.Describe("A");
        Assert.AreEqual(1, rows.Count);
        var row = rows[0];
        Assert.AreEqual("A", row.Character);
        Assert.AreEqual(65, row.CodePoint);
        Assert.AreEqual("65", row.Decimal);
        Assert.AreEqual("41", row.Hexadecimal);
        Assert.AreEqual("101", row.Octal);
        Assert.AreEqual("1000001", row.Binary);
    }

    [TestMethod]
    public void Describe_CountsCodePointsNotUtf16Units()
    {
        // The astral emoji is one code point, so one row.
        var rows = _converter.Describe("A\U0001F600");
        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual(128512, rows[1].CodePoint);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Describe_NullOrEmpty_ReturnsEmpty(string? text)
        => Assert.AreEqual(0, _converter.Describe(text).Count);

    [TestMethod]
    public void EncodeAll_ReturnsEveryBaseForText()
    {
        var all = _converter.EncodeAll("AB");
        Assert.AreEqual("65 66", all.Decimal);
        Assert.AreEqual("41 42", all.Hexadecimal);
        Assert.AreEqual("101 102", all.Octal);
        Assert.AreEqual("1000001 1000010", all.Binary);
    }
}
