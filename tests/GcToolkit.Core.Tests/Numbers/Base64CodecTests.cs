using System.Text;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class Base64CodecTests
{
    private readonly Base64Codec _codec = new();

    // ---- Encode: RFC 4648 known vectors ----

    [DataTestMethod]
    [DataRow("", "")]
    [DataRow("f", "Zg==")]
    [DataRow("fo", "Zm8=")]
    [DataRow("foo", "Zm9v")]
    [DataRow("foob", "Zm9vYg==")]
    [DataRow("fooba", "Zm9vYmE=")]
    [DataRow("foobar", "Zm9vYmFy")]
    [DataRow("Man", "TWFu")]
    [DataRow("Hello, World!", "SGVsbG8sIFdvcmxkIQ==")]
    public void Encode_Rfc4648Vectors_ProducesExpected(string text, string expected)
        => Assert.AreEqual(expected, _codec.Encode(text, Base64Variant.Standard, padding: true, Base64TextEncoding.Utf8));

    // ---- Encode: padding toggle ----

    [DataTestMethod]
    [DataRow("f", "Zg")]
    [DataRow("fo", "Zm8")]
    [DataRow("foo", "Zm9v")]
    [DataRow("foob", "Zm9vYg")]
    [DataRow("fooba", "Zm9vYmE")]
    public void Encode_PaddingOff_OmitsEquals(string text, string expected)
        => Assert.AreEqual(expected, _codec.Encode(text, Base64Variant.Standard, padding: false, Base64TextEncoding.Utf8));

    // ---- Encode: URL-safe alphabet ----

    [TestMethod]
    public void Encode_UrlSafe_UsesDashAndUnderscore()
    {
        // bytes [0xFB, 0xFF] -> Standard "+/8=" vs UrlSafe "-_8="
        var bytes = new byte[] { 0xFB, 0xFF };
        Assert.AreEqual("+/8=", _codec.EncodeBytes(bytes, Base64Variant.Standard, padding: true));
        Assert.AreEqual("-_8=", _codec.EncodeBytes(bytes, Base64Variant.UrlSafe, padding: true));
    }

    [TestMethod]
    public void Encode_UrlSafe_PaddingOff_OmitsEquals()
    {
        var bytes = new byte[] { 0xFB, 0xFF };
        Assert.AreEqual("-_8", _codec.EncodeBytes(bytes, Base64Variant.UrlSafe, padding: false));
    }

    // ---- Encode: MIME line wrapping ----

    [TestMethod]
    public void Encode_Mime_WrapsAt76CharactersWithCrLf()
    {
        // 60 bytes -> 80 base64 chars -> wraps once at 76.
        var input = new string('A', 60);
        var result = _codec.Encode(input, Base64Variant.Mime, padding: true, Base64TextEncoding.Ascii);

        var lines = result.Split("\r\n");
        Assert.IsTrue(lines.Length >= 2, "Expected MIME output to wrap onto multiple lines.");
        foreach (var line in lines)
        {
            Assert.IsTrue(line.Length <= 76, $"Line exceeds 76 chars: {line.Length}");
        }

        // Stripping the CRLF wrapping yields the same payload as standard.
        var standard = _codec.Encode(input, Base64Variant.Standard, padding: true, Base64TextEncoding.Ascii);
        Assert.AreEqual(standard, result.Replace("\r\n", string.Empty));
    }

    [TestMethod]
    public void Encode_Mime_ShortInput_NoTrailingNewline()
    {
        var result = _codec.Encode("foobar", Base64Variant.Mime, padding: true, Base64TextEncoding.Ascii);
        Assert.AreEqual("Zm9vYmFy", result);
    }

    // ---- Encode: text encodings ----

    [TestMethod]
    public void Encode_Utf8_HandlesMultiByteCharacters()
    {
        // "č" is U+010D -> UTF-8 0xC4 0x8D -> "xI0="
        Assert.AreEqual("xI0=", _codec.Encode("č", Base64Variant.Standard, padding: true, Base64TextEncoding.Utf8));
    }

    [TestMethod]
    public void Encode_Latin1_EncodesSingleBytePerChar()
    {
        // "é" is U+00E9 -> Latin-1 0xE9 -> "6Q=="
        Assert.AreEqual("6Q==", _codec.Encode("é", Base64Variant.Standard, padding: true, Base64TextEncoding.Latin1));
    }

    [TestMethod]
    public void Encode_Ascii_DropsNonAsciiToQuestionMark()
    {
        // ASCII encoder maps out-of-range chars to '?' (0x3F) -> "Pw=="
        Assert.AreEqual("Pw==", _codec.Encode("č", Base64Variant.Standard, padding: true, Base64TextEncoding.Ascii));
    }

    // ---- Decode: known vectors ----

    [DataTestMethod]
    [DataRow("Zg==", "f")]
    [DataRow("Zm8=", "fo")]
    [DataRow("Zm9v", "foo")]
    [DataRow("Zm9vYg==", "foob")]
    [DataRow("Zm9vYmE=", "fooba")]
    [DataRow("Zm9vYmFy", "foobar")]
    [DataRow("SGVsbG8sIFdvcmxkIQ==", "Hello, World!")]
    public void Decode_Rfc4648Vectors_ReturnsText(string base64, string expected)
    {
        Assert.IsTrue(_codec.TryDecode(base64, Base64Variant.Standard, out var result));
        Assert.AreEqual(expected, result.AsUtf8Text);
    }

    // ---- Decode: forgiving behaviors ----

    [TestMethod]
    public void Decode_IgnoresWhitespaceAndNewlines()
    {
        Assert.IsTrue(_codec.TryDecode("Zm9v\r\n YmFy", Base64Variant.Standard, out var result));
        Assert.AreEqual("foobar", result.AsUtf8Text);
    }

    [TestMethod]
    public void Decode_MissingPadding_IsAutoFixed()
    {
        Assert.IsTrue(_codec.TryDecode("Zg", Base64Variant.Standard, out var result));
        Assert.AreEqual("f", result.AsUtf8Text);
    }

    [TestMethod]
    public void Decode_StandardMode_TransparentlyAcceptsUrlSafeInput()
    {
        // "-_8=" is URL-safe; even in Standard mode the forgiving decoder accepts it.
        Assert.IsTrue(_codec.TryDecode("-_8=", Base64Variant.Standard, out var result));
        CollectionAssert.AreEqual(new byte[] { 0xFB, 0xFF }, result.Bytes);
    }

    [TestMethod]
    public void Decode_UrlSafeMode_DecodesUrlSafeInput()
    {
        Assert.IsTrue(_codec.TryDecode("-_8", Base64Variant.UrlSafe, out var result));
        CollectionAssert.AreEqual(new byte[] { 0xFB, 0xFF }, result.Bytes);
    }

    [TestMethod]
    public void Decode_ProducesHexAndByteCount()
    {
        Assert.IsTrue(_codec.TryDecode("Zm9v", Base64Variant.Standard, out var result));
        Assert.AreEqual(3, result.ByteCount);
        Assert.AreEqual("66 6F 6F", result.Hex);
    }

    [TestMethod]
    public void Decode_Latin1Rendering_DecodesHighBytesAsLatin1()
    {
        // 0xE9 -> Latin-1 "é"
        Assert.IsTrue(_codec.TryDecode("6Q==", Base64Variant.Standard, out var result));
        Assert.AreEqual("é", result.AsLatin1Text);
    }

    // ---- Decode: validation / error paths ----

    [TestMethod]
    public void Decode_Empty_SucceedsWithZeroBytes()
    {
        Assert.IsTrue(_codec.TryDecode("", Base64Variant.Standard, out var result));
        Assert.AreEqual(0, result.ByteCount);
        Assert.AreEqual(string.Empty, result.AsUtf8Text);
    }

    [DataTestMethod]
    [DataRow("Zm9v!")]
    [DataRow("@@@@")]
    [DataRow("Zg=a")]
    public void Decode_InvalidCharacters_ReturnsFalse(string base64)
        => Assert.IsFalse(_codec.TryDecode(base64, Base64Variant.Standard, out _));

    [TestMethod]
    public void Decode_WrongLength_ReturnsFalse()
    {
        // A single base64 char can never form a byte (needs at least 2).
        Assert.IsFalse(_codec.TryDecode("A", Base64Variant.Standard, out _));
    }

    [TestMethod]
    public void TryDecode_Invalid_ReportsError()
    {
        var ok = _codec.TryDecode("@@@@", Base64Variant.Standard, out _, out var error);
        Assert.IsFalse(ok);
        Assert.IsFalse(string.IsNullOrEmpty(error));
    }

    // ---- Round trips ----

    [DataTestMethod]
    [DataRow("foobar")]
    [DataRow("Hello, World!")]
    [DataRow("Příliš žluťoučký kůň úpěl ďábelské ódy")]
    [DataRow("Tady jsou souřadnice N 49 12.345 E 016 34.567")]
    public void RoundTrip_Utf8_PreservesText(string text)
    {
        var encoded = _codec.Encode(text, Base64Variant.Standard, padding: true, Base64TextEncoding.Utf8);
        Assert.IsTrue(_codec.TryDecode(encoded, Base64Variant.Standard, out var result));
        Assert.AreEqual(text, result.AsUtf8Text);
    }

    [DataTestMethod]
    [DataRow(Base64Variant.Standard)]
    [DataRow(Base64Variant.UrlSafe)]
    [DataRow(Base64Variant.Mime)]
    public void RoundTrip_AllVariants_NoPadding_PreservesBytes(Base64Variant variant)
    {
        var bytes = new byte[] { 0x00, 0x10, 0xFB, 0xFF, 0x7E, 0x3F, 0xA5 };
        var encoded = _codec.EncodeBytes(bytes, variant, padding: false);
        Assert.IsTrue(_codec.TryDecode(encoded, variant, out var result));
        CollectionAssert.AreEqual(bytes, result.Bytes);
    }

    // ---- AutoDirection ----

    [TestMethod]
    public void AutoDirection_ValidBase64_Decodes()
    {
        var op = _codec.AutoDirection("Zm9vYmFy", Base64Variant.Standard, padding: true, Base64TextEncoding.Utf8);
        Assert.AreEqual(Base64Direction.Decoded, op.Direction);
        Assert.AreEqual("foobar", op.Output);
    }

    [TestMethod]
    public void AutoDirection_PlainText_Encodes()
    {
        var op = _codec.AutoDirection("Hello, World!", Base64Variant.Standard, padding: true, Base64TextEncoding.Utf8);
        Assert.AreEqual(Base64Direction.Encoded, op.Direction);
        Assert.AreEqual("SGVsbG8sIFdvcmxkIQ==", op.Output);
    }

    [TestMethod]
    public void AutoDirection_Empty_ReturnsNone()
    {
        var op = _codec.AutoDirection("   ", Base64Variant.Standard, padding: true, Base64TextEncoding.Utf8);
        Assert.AreEqual(Base64Direction.None, op.Direction);
        Assert.AreEqual(string.Empty, op.Output);
    }

    [TestMethod]
    public void AutoDirection_DecodableButNonPrintable_FallsBackToEncode()
    {
        // "////" decodes to 0xFF 0xFF 0xFF (non-printable) — better treated as text to encode.
        var op = _codec.AutoDirection("////", Base64Variant.Standard, padding: true, Base64TextEncoding.Utf8);
        Assert.AreEqual(Base64Direction.Encoded, op.Direction);
    }

    // ---- TryAllVariants ----

    [TestMethod]
    public void TryAllVariants_ReportsEachVariantAndPrintability()
    {
        var results = _codec.TryAllVariants("Zm9vYmFy");

        Assert.AreEqual(3, results.Count);
        var standard = results.Single(r => r.Variant == Base64Variant.Standard);
        Assert.IsTrue(standard.Success);
        Assert.IsTrue(standard.IsPrintable);
        Assert.AreEqual("foobar", standard.Text);
    }

    [TestMethod]
    public void TryAllVariants_UrlSafeOnlyInput_FlagsWhichDecodeToPrintable()
    {
        // Encode printable text using URL-safe chars that differ from standard.
        var bytes = Encoding.UTF8.GetBytes("subjects?_d");
        var urlSafe = _codec.EncodeBytes(bytes, Base64Variant.UrlSafe, padding: true);

        var results = _codec.TryAllVariants(urlSafe);
        var urlResult = results.Single(r => r.Variant == Base64Variant.UrlSafe);
        Assert.IsTrue(urlResult.Success);
        Assert.AreEqual("subjects?_d", urlResult.Text);
    }
}
