using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class BeghilosCodecTests
{
    private readonly BeghilosCodec _codec = new();

    // ---- Encode (word -> number) ----

    [TestMethod]
    public void Encode_Hello_ProducesReversedDigits()
    {
        // H4 E3 L7 L7 O0 = "43770"; reversed for the flipped display = "07734".
        var result = _codec.Encode("HELLO");
        Assert.AreEqual("07734", result.Result);
        Assert.IsFalse(result.HasUnsupported);
    }

    [TestMethod]
    public void Encode_IsCaseInsensitive()
        => Assert.AreEqual("07734", _codec.Encode("hello").Result);

    [TestMethod]
    [DataRow("BEGHILOSZ", "250714638")] // 836417052 reversed
    [DataRow("SHELL", "77345")]         // S5 H4 E3 L7 L7 = 54377 -> reversed 77345
    [DataRow("BOOBIES", "5318008")]     // classic: B8 O0 O0 B8 I1 E3 S5 = 8008135 -> reversed 5318008
    public void Encode_KnownWords_MatchExpected(string word, string expected)
        => Assert.AreEqual(expected, _codec.Encode(word).Result);

    [TestMethod]
    public void Encode_UnsupportedLetter_IsReportedNotDropped()
    {
        // 'A' has no seven-segment equivalent.
        var result = _codec.Encode("AS");
        Assert.AreEqual("5", result.Result); // only S=5 converts
        Assert.IsTrue(result.HasUnsupported);
        Assert.AreEqual(1, result.UnsupportedCount);
        CollectionAssert.AreEqual(new[] { 'A' }, result.UnsupportedCharacters.ToArray());
    }

    [TestMethod]
    public void Encode_DistinctUnsupported_ReportedInFirstSeenOrder()
    {
        var result = _codec.Encode("CAT CAT");
        // C, A, T are unsupported; each reported once, in order; whitespace ignored.
        CollectionAssert.AreEqual(new[] { 'C', 'A', 'T' }, result.UnsupportedCharacters.ToArray());
        Assert.AreEqual(3, result.UnsupportedCount);
        Assert.AreEqual(string.Empty, result.Result);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encode_NullOrEmpty_ReturnsEmpty(string? input)
    {
        var result = _codec.Encode(input);
        Assert.AreEqual(string.Empty, result.Result);
        Assert.IsFalse(result.HasUnsupported);
    }

    // ---- Decode (number -> word) ----

    [TestMethod]
    public void Decode_FormattedHello_StripsFormattingAndReadsWord()
    {
        // "0.7734" -> strip '.' -> "07734" -> reverse "43770" -> H E L L O.
        var result = _codec.Decode("0.7734");
        Assert.AreEqual("HELLO", result.Result);
    }

    [TestMethod]
    [DataRow("07734", "HELLO")]
    [DataRow("5318008", "BOOBIES")]
    [DataRow("250714638", "BEGHILOSZ")]
    public void Decode_KnownNumbers_MatchExpected(string number, string expected)
        => Assert.AreEqual(expected, _codec.Decode(number).Result);

    [TestMethod]
    public void Decode_NonDigit_IsReportedAsUnsupported()
    {
        // The '.' formatting char is stripped (whitespace-like, ignored), but 'x' is reported.
        var result = _codec.Decode("0x7734");
        Assert.AreEqual("HELLO", result.Result);
        Assert.IsTrue(result.HasUnsupported);
        CollectionAssert.AreEqual(new[] { 'x' }, result.UnsupportedCharacters.ToArray());
    }

    [TestMethod]
    public void Decode_Nine_StrictProfile_IsUnsupported()
    {
        var result = _codec.Decode("9", BeghilosProfile.Strict);
        Assert.AreEqual(string.Empty, result.Result);
        Assert.IsTrue(result.HasUnsupported);
        CollectionAssert.AreEqual(new[] { '9' }, result.UnsupportedCharacters.ToArray());
    }

    [TestMethod]
    public void Decode_Nine_ExtendedProfile_DecodesAsG()
    {
        var result = _codec.Decode("9", BeghilosProfile.Extended);
        Assert.AreEqual("G", result.Result);
        Assert.IsFalse(result.HasUnsupported);
    }

    [TestMethod]
    public void Decode_UnsupportedReportedInOriginalOrder_NotReversed()
    {
        var result = _codec.Decode("a0b7");
        // 'a' then 'b' in original left-to-right order, despite the reverse during decoding.
        CollectionAssert.AreEqual(new[] { 'a', 'b' }, result.UnsupportedCharacters.ToArray());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Decode_NullOrEmpty_ReturnsEmpty(string? input)
        => Assert.AreEqual(string.Empty, _codec.Decode(input).Result);

    // ---- Round trips ----

    [TestMethod]
    [DataRow("HELLO")]
    [DataRow("BEGHILOSZ")]
    [DataRow("BOOBIES")]
    [DataRow("SHELL")]
    [DataRow("GIGGLES")]
    public void RoundTrip_EncodeThenDecode_PreservesUpperCaseWord(string word)
    {
        var number = _codec.Encode(word).Result;
        Assert.AreEqual(word, _codec.Decode(number).Result);
    }

    [TestMethod]
    [DataRow("07734")]
    [DataRow("5318008")]
    [DataRow("250714638")]
    public void RoundTrip_DecodeThenEncode_PreservesNumber(string number)
    {
        var word = _codec.Decode(number).Result;
        Assert.AreEqual(number, _codec.Encode(word).Result);
    }

    // ---- Auto-direction detection ----

    [TestMethod]
    [DataRow("0.7734", BeghilosDirection.Decode)]
    [DataRow("12345", BeghilosDirection.Decode)]
    [DataRow("HELLO", BeghilosDirection.Encode)]
    [DataRow("SHELL OIL", BeghilosDirection.Encode)]
    [DataRow("h3llo", BeghilosDirection.Encode)] // any letter present -> encode
    [DataRow("", BeghilosDirection.Encode)]
    [DataRow(null, BeghilosDirection.Encode)]
    public void DetectDirection_GuessesFromContent(string? input, BeghilosDirection expected)
        => Assert.AreEqual(expected, BeghilosCodec.DetectDirection(input));

    [TestMethod]
    public void Convert_AutoDirection_DecodesNumberInput()
        => Assert.AreEqual("HELLO", _codec.Convert("0.7734").Result);

    [TestMethod]
    public void Convert_AutoDirection_EncodesWordInput()
        => Assert.AreEqual("07734", _codec.Convert("HELLO").Result);

    [TestMethod]
    public void Convert_ExplicitDirection_OverridesAutoDetection()
    {
        // "07734" auto-detects as decode, but forcing encode treats it as a (digit) word.
        var encoded = _codec.Convert("07734", BeghilosDirection.Encode);
        // Digits are not letters, so all are unsupported on encode.
        Assert.AreEqual(string.Empty, encoded.Result);
        Assert.IsTrue(encoded.HasUnsupported);
    }

    // ---- Mapping reference ----

    [TestMethod]
    public void SupportedLetters_AreTheCanonicalNineLetters()
        => CollectionAssert.AreEqual("BEGHILOSZ".ToCharArray(), BeghilosCodec.SupportedLetters.ToArray());

    [TestMethod]
    public void Mapping_PairsLettersToCanonicalDigits()
    {
        var digits = string.Concat(BeghilosCodec.Mapping.Select(p => p.Digit));
        Assert.AreEqual("836417052", digits);
    }
}
