using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class Base64CodecTests
{
    /// <summary>"Příliš žluťoučký kůň" — the Czech pangram, 29 UTF-8 bytes from 20 characters.</summary>
    private const string CzechText = "Příliš žluťoučký kůň";

    private const string CzechBase64 = "UMWZw61sacWhIMW+bHXFpW91xI1rw70ga8WvxYg=";

    private readonly Base64Codec _codec = new();

    // ---- Encoding ----

    [DataTestMethod]
    [DataRow("", "")]
    [DataRow("f", "Zg==")]
    [DataRow("fo", "Zm8=")]
    [DataRow("foo", "Zm9v")]
    [DataRow("foob", "Zm9vYg==")]
    [DataRow("fooba", "Zm9vYmE=")]
    [DataRow("foobar", "Zm9vYmFy")]
    [DataRow("Hello", "SGVsbG8=")]
    public void Encode_Rfc4648Vectors_ProducesStandardAlphabet(string input, string expected)
        => Assert.AreEqual(expected, _codec.Encode(input));

    [TestMethod]
    public void Encode_NullInput_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, _codec.Encode(null));

    [TestMethod]
    public void Encode_NonAsciiText_UsesUtf8()
        => Assert.AreEqual(CzechBase64, _codec.Encode(CzechText));

    [DataTestMethod]
    [DataRow("f", "Zg")]
    [DataRow("fo", "Zm8")]
    [DataRow("foo", "Zm9v")]
    public void Encode_WithoutPadding_OmitsEqualsSigns(string input, string expected)
        => Assert.AreEqual(expected, _codec.Encode(input, includePadding: false));

    // ---- Decoding: the happy path ----

    [DataTestMethod]
    [DataRow("Zg==", "f")]
    [DataRow("Zm8=", "fo")]
    [DataRow("Zm9v", "foo")]
    [DataRow("Zm9vYmFy", "foobar")]
    [DataRow("SGVsbG8=", "Hello")]
    public void Decode_StandardInput_ReturnsText(string input, string expected)
    {
        var result = _codec.Decode(input);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(expected, result.Text);
        Assert.IsFalse(result.WasRepaired, "Canonical input needs no repair.");
    }

    [TestMethod]
    public void Decode_NonAsciiPayload_RoundTripsUtf8()
    {
        var result = _codec.Decode(CzechBase64);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(CzechText, result.Text);
        Assert.AreEqual(29, result.ByteCount);
        Assert.IsFalse(result.IsBinary);
    }

    [TestMethod]
    public void EncodeThenDecode_NonAsciiText_RoundTrips()
        => Assert.AreEqual(CzechText, _codec.Decode(_codec.Encode(CzechText)).Text);

    // ---- Decoding: forgiveness (the whole point in the field) ----

    [DataTestMethod]
    [DataRow("SGVsbG8")]
    [DataRow("SGVsbG8=")]
    [DataRow("SGVsbG8==")]
    [DataRow("SGVsbG8====")]
    public void Decode_AnyPaddingVariant_ReturnsText(string input)
        => Assert.AreEqual("Hello", _codec.Decode(input).Text);

    [DataTestMethod]
    [DataRow("SGVs\nbG8=")]
    [DataRow("SGVs\r\nbG8=")]
    [DataRow("SGVs bG8 =")]
    [DataRow("  SGVsbG8=  ")]
    [DataRow("SGV\tsbG8")]
    public void Decode_EmbeddedWhitespace_IsIgnored(string input)
    {
        var result = _codec.Decode(input);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("Hello", result.Text);
        Assert.IsTrue(result.WasRepaired, "Repaired input should be reported so the UI can say so.");
    }

    [DataTestMethod]
    [DataRow("fn5-", "~~~")]
    [DataRow("Pz8_", "???")]
    public void Decode_UrlSafeAlphabet_IsAccepted(string input, string expected)
        => Assert.AreEqual(expected, _codec.Decode(input).Text);

    [TestMethod]
    public void Decode_BytesThatAreNotUtf8_SucceedsAndFlagsBinary()
    {
        // "//79" is 0xFF 0xFE 0xFD — a valid Base64 payload that is not valid UTF-8 text.
        var result = _codec.Decode("//79");

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.IsBinary);
        Assert.AreEqual(3, result.ByteCount);
        Assert.AreEqual("ÿþý", result.Text);
    }

    // ---- Decoding: genuine errors ----

    [DataTestMethod]
    [DataRow("SGVsbG8*", "*")]
    [DataRow("Hello world!", "!")]
    [DataRow("SGVs#bG8=", "#")]
    public void Decode_CharacterOutsideAlphabet_ReportsInvalidCharacter(string input, string offender)
    {
        var result = _codec.Decode(input);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(Base64DecodeError.InvalidCharacter, result.Error);
        Assert.AreEqual(offender, result.InvalidCharacter);
        Assert.AreEqual(string.Empty, result.Text);
    }

    [TestMethod]
    public void Decode_DataAfterPadding_ReportsInvalidCharacter()
    {
        var result = _codec.Decode("SGVs=bG8");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(Base64DecodeError.InvalidCharacter, result.Error);
    }

    [DataTestMethod]
    [DataRow("SGVsbG8yZ")]
    [DataRow("A")]
    [DataRow("SGVsb")]
    public void Decode_LeftoverCharacterInLastGroup_ReportsInvalidLength(string input)
    {
        var result = _codec.Decode(input);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(Base64DecodeError.InvalidLength, result.Error);
    }

    // ---- Decoding: empty input ----

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   \r\n ")]
    [DataRow("===")]
    public void Decode_EmptyOrPaddingOnlyInput_SucceedsWithEmptyText(string input)
    {
        var result = _codec.Decode(input);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(string.Empty, result.Text);
        Assert.AreEqual(0, result.ByteCount);
        Assert.AreEqual(Base64DecodeError.None, result.Error);
    }

    [TestMethod]
    public void Decode_NullInput_SucceedsWithEmptyText()
        => Assert.IsTrue(_codec.Decode(null).Success);

    // ---- Direction auto-detection ----

    [DataTestMethod]
    [DataRow("SGVsbG8=")]
    [DataRow("SGVsbG8")]
    [DataRow("Zm9vYmFy")]
    [DataRow("SGVs\nbG8=")]
    [DataRow(CzechBase64)]
    public void LooksLikeBase64_DecodableToReadableText_ReturnsTrue(string input)
        => Assert.IsTrue(Base64Codec.LooksLikeBase64(input));

    [DataTestMethod]
    [DataRow("")]
    [DataRow("Ahoj")]                       // valid Base64 characters, but decodes to control bytes
    [DataRow("test")]                       // decodes to bytes that are not valid UTF-8
    [DataRow("Hello world!")]               // '!' is outside the alphabet
    [DataRow("abc")]                        // too short to be a meaningful payload
    [DataRow("GC12345")]
    [DataRow("N 49 12.345 E 014 25.678")]
    [DataRow(CzechText)]
    public void LooksLikeBase64_PlainText_ReturnsFalse(string input)
        => Assert.IsFalse(Base64Codec.LooksLikeBase64(input));

    [TestMethod]
    public void LooksLikeBase64_NullInput_ReturnsFalse()
        => Assert.IsFalse(Base64Codec.LooksLikeBase64(null));
}
