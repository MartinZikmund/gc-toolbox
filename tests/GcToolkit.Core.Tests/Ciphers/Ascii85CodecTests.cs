using System.Text;
using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class Ascii85CodecTests
{
    private readonly Ascii85Codec _codec = new();

    private static byte[] Bytes(params int[] values) => values.Select(v => (byte)v).ToArray();

    // ---- Z85 official vector (ZeroMQ RFC 32) ----

    [TestMethod]
    public void Encode_Z85_OfficialVector_ProducesHelloWorld()
    {
        var input = Bytes(0x86, 0x4F, 0xD2, 0x6F, 0xB5, 0x59, 0xF7, 0x5B);
        Assert.AreEqual("HelloWorld", _codec.EncodeBytes(input, Ascii85Variant.Z85));
    }

    [TestMethod]
    public void Decode_Z85_OfficialVector_RoundTripsToBytes()
    {
        var expected = Bytes(0x86, 0x4F, 0xD2, 0x6F, 0xB5, 0x59, 0xF7, 0x5B);
        Assert.IsTrue(_codec.TryDecodeToBytes("HelloWorld", Ascii85Variant.Z85, out var bytes, out var error));
        Assert.IsNull(error);
        CollectionAssert.AreEqual(expected, bytes);
    }

    // ---- Adobe / btoa: zero group abbreviation ----

    [TestMethod]
    public void Encode_Adobe_FourZeroBytes_AbbreviatesToZ()
        => Assert.AreEqual("z", _codec.EncodeBytes(Bytes(0, 0, 0, 0), Ascii85Variant.Adobe));

    [TestMethod]
    public void Encode_Adobe_FourZeroBytes_WithoutAbbreviation_IsFiveBangs()
    {
        var options = new Ascii85Options { AbbreviateZeroGroup = false };
        Assert.AreEqual("!!!!!", _codec.EncodeBytes(Bytes(0, 0, 0, 0), Ascii85Variant.Adobe, options));
    }

    [TestMethod]
    public void Encode_Btoa_FourSpaceBytes_AbbreviatesToY()
        => Assert.AreEqual("y", _codec.EncodeBytes(Bytes(0x20, 0x20, 0x20, 0x20), Ascii85Variant.Btoa));

    [TestMethod]
    public void Encode_Adobe_FourSpaceBytes_DoesNotAbbreviateToY()
    {
        // 'y' abbreviation is a btoa-only extension; Adobe must encode spaces literally.
        var encoded = _codec.EncodeBytes(Bytes(0x20, 0x20, 0x20, 0x20), Ascii85Variant.Adobe);
        Assert.AreNotEqual("y", encoded);
    }

    // ---- Adobe known reference vector ("Man " -> "9jqo^") ----

    [TestMethod]
    public void Encode_Adobe_KnownAsciiVector()
    {
        var input = Encoding.ASCII.GetBytes("Man ");
        Assert.AreEqual("9jqo^", _codec.EncodeBytes(input, Ascii85Variant.Adobe));
    }

    [TestMethod]
    public void Encode_Adobe_Leviathan_MatchesReference()
    {
        // Classic Wikipedia ASCII-85 sample.
        var text = "Man is distinguished, not only by his reason, but by this singular passion from "
            + "other animals, which is a lust of the mind, that by a perseverance of delight in the "
            + "continued and indefatigable generation of knowledge, exceeds the short vehemence of any "
            + "carnal pleasure.";
        var encoded = _codec.EncodeBytes(Encoding.ASCII.GetBytes(text), Ascii85Variant.Adobe);
        Assert.IsTrue(encoded.StartsWith("9jqo^BlbD-BleB1DJ+*+F(f", StringComparison.Ordinal));
        Assert.IsTrue(_codec.TryDecodeToBytes(encoded, Ascii85Variant.Adobe, out var bytes, out _));
        Assert.AreEqual(text, Encoding.ASCII.GetString(bytes));
    }

    // ---- Adobe delimiters ----

    [TestMethod]
    public void Encode_Adobe_WithDelimiters_WrapsOutput()
    {
        var options = new Ascii85Options { UseAdobeDelimiters = true };
        var encoded = _codec.EncodeBytes(Bytes(0, 0, 0, 0), Ascii85Variant.Adobe, options);
        Assert.AreEqual("<~z~>", encoded);
    }

    [TestMethod]
    public void Decode_Adobe_StripsDelimiters()
    {
        Assert.IsTrue(_codec.TryDecodeToBytes("<~z~>", Ascii85Variant.Adobe, out var bytes, out _));
        CollectionAssert.AreEqual(Bytes(0, 0, 0, 0), bytes);
    }

    // ---- Whitespace tolerance ----

    [TestMethod]
    public void Decode_Adobe_IgnoresWhitespace()
    {
        var clean = _codec.EncodeText("Hello, World!", Ascii85Variant.Adobe);
        var spaced = string.Join(" \n\t", clean.ToCharArray());
        Assert.IsTrue(_codec.TryDecodeToText(spaced, Ascii85Variant.Adobe, out var text, out var error));
        Assert.IsNull(error);
        Assert.AreEqual("Hello, World!", text);
    }

    // ---- Illegal character reporting ----

    [TestMethod]
    public void Decode_Adobe_IllegalCharacter_ReportsFirstPosition()
    {
        // 'v' (0x76) is above Adobe's 'u' ceiling.
        Assert.IsFalse(_codec.TryDecodeToBytes("9jqv", Ascii85Variant.Adobe, out _, out var error));
        Assert.IsNotNull(error);
        Assert.AreEqual('v', error!.Character);
        Assert.AreEqual(3, error.Position);
    }

    [TestMethod]
    public void Decode_Z85_IllegalCharacter_ReportsFirstPosition()
    {
        // Space is not in the Z85 alphabet and is not stripped as whitespace inside decode? It IS stripped.
        // Use a quote character which is not whitespace and not in Z85 alphabet.
        Assert.IsFalse(_codec.TryDecodeToBytes("Hell\"World", Ascii85Variant.Z85, out _, out var error));
        Assert.IsNotNull(error);
        Assert.AreEqual('"', error!.Character);
    }

    // ---- Z85 padding round-trip for arbitrary text ----

    [DataTestMethod]
    [DataRow("A")]
    [DataRow("AB")]
    [DataRow("ABC")]
    [DataRow("ABCD")]
    [DataRow("Hello, World!")]
    [DataRow("Geocaching rocks")]
    public void RoundTrip_Z85_ArbitraryText_PreservesText(string text)
    {
        var encoded = _codec.EncodeText(text, Ascii85Variant.Z85);
        Assert.IsTrue(_codec.TryDecodeToText(encoded, Ascii85Variant.Z85, out var decoded, out var error));
        Assert.IsNull(error);
        Assert.AreEqual(text, decoded);
    }

    // ---- Round-trip property across all variants, ASCII + UTF-8 ----

    [DataTestMethod]
    [DataRow("")]
    [DataRow("A")]
    [DataRow("Hi")]
    [DataRow("cat")]
    [DataRow("Hello, World!")]
    [DataRow("The quick brown fox jumps over the lazy dog.")]
    [DataRow("Příliš žluťoučký kůň úpěl ďábelské ódy")]
    [DataRow("日本語 éèê")]
    public void RoundTrip_AllVariants_PreservesText(string text)
    {
        foreach (var variant in Enum.GetValues<Ascii85Variant>())
        {
            var encoded = _codec.EncodeText(text, variant);
            Assert.IsTrue(
                _codec.TryDecodeToText(encoded, variant, out var decoded, out var error),
                $"Decode failed for {variant}: {error?.Message}");
            Assert.AreEqual(text, decoded, $"Round-trip mismatch for {variant}.");
        }
    }

    [TestMethod]
    public void RoundTrip_AllVariants_ZeroAndSpaceAbbreviations()
    {
        // Exercises the z/y abbreviation paths and their decode.
        var input = Bytes(0, 0, 0, 0, 0x20, 0x20, 0x20, 0x20, 1, 2, 3, 4);
        foreach (var variant in Enum.GetValues<Ascii85Variant>())
        {
            var encoded = _codec.EncodeBytes(input, variant);
            Assert.IsTrue(_codec.TryDecodeToBytes(encoded, variant, out var bytes, out var error), $"{variant}: {error?.Message}");
            CollectionAssert.AreEqual(input, bytes, $"Byte round-trip failed for {variant}.");
        }
    }

    // ---- Partial final group (Adobe truncation rule) ----

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    public void RoundTrip_Adobe_PartialFinalGroup_PreservesBytes(int length)
    {
        var input = Enumerable.Range(1, length).Select(i => (byte)(i * 37 % 256)).ToArray();
        var encoded = _codec.EncodeBytes(input, Ascii85Variant.Adobe);
        Assert.IsTrue(_codec.TryDecodeToBytes(encoded, Ascii85Variant.Adobe, out var bytes, out _));
        CollectionAssert.AreEqual(input, bytes);
    }

    // ---- Empty input ----

    [TestMethod]
    public void Encode_EmptyBytes_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, _codec.EncodeBytes([], Ascii85Variant.Adobe));

    [TestMethod]
    public void Decode_EmptyInput_ReturnsEmptyBytes()
    {
        Assert.IsTrue(_codec.TryDecodeToBytes("", Ascii85Variant.Adobe, out var bytes, out var error));
        Assert.IsNull(error);
        Assert.AreEqual(0, bytes.Length);
    }

    [TestMethod]
    public void Decode_OnlyWhitespace_ReturnsEmptyBytes()
    {
        Assert.IsTrue(_codec.TryDecodeToBytes("  \n\t ", Ascii85Variant.Adobe, out var bytes, out _));
        Assert.AreEqual(0, bytes.Length);
    }

    // ---- Malformed final group (single trailing char is invalid in 5/4 schemes) ----

    [TestMethod]
    public void Decode_Adobe_SingleTrailingChar_ReportsError()
    {
        // A lone leftover character cannot represent any bytes.
        Assert.IsFalse(_codec.TryDecodeToBytes("9jqo^9", Ascii85Variant.Adobe, out _, out var error));
        Assert.IsNotNull(error);
    }

    // ---- RFC 1924 alphabet sanity ----

    [TestMethod]
    public void RoundTrip_Rfc1924_PreservesText()
    {
        var encoded = _codec.EncodeText("Coordinates", Ascii85Variant.Rfc1924);
        Assert.IsTrue(_codec.TryDecodeToText(encoded, Ascii85Variant.Rfc1924, out var decoded, out _));
        Assert.AreEqual("Coordinates", decoded);
    }

    // ---- Hex view ----

    [TestMethod]
    public void ToHex_FormatsBytesAsSpacedUppercaseHex()
        => Assert.AreEqual("00 0F 7F FF", Ascii85Codec.ToHex(Bytes(0x00, 0x0F, 0x7F, 0xFF)));

    [TestMethod]
    public void ToHex_Empty_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, Ascii85Codec.ToHex([]));

    // ---- Auto-detect ----

    [TestMethod]
    public void DetectVariant_AdobeDelimiters_PicksAdobe()
        => Assert.AreEqual(Ascii85Variant.Adobe, _codec.DetectVariant("<~9jqo^~>"));

    [TestMethod]
    public void DetectVariant_Z85OfficialVector_PicksZ85()
    {
        // "HelloWorld" decodes cleanly only as Z85 (contains lowercase+uppercase, length multiple of 5).
        Assert.AreEqual(Ascii85Variant.Z85, _codec.DetectVariant("HelloWorld"));
    }

    [TestMethod]
    public void DetectVariant_YAbbreviation_PicksBtoa()
    {
        // 'y' only appears in btoa output.
        Assert.AreEqual(Ascii85Variant.Btoa, _codec.DetectVariant("y"));
    }

    // ---- Try all variants ----

    [TestMethod]
    public void TryAllVariants_ReturnsOneEntryPerVariant()
    {
        var encoded = _codec.EncodeText("Hello", Ascii85Variant.Adobe);
        var results = _codec.TryAllVariants(encoded);
        Assert.AreEqual(Enum.GetValues<Ascii85Variant>().Length, results.Count);
        // The Adobe entry must decode back to the original text.
        var adobe = results.First(r => r.Variant == Ascii85Variant.Adobe);
        Assert.IsTrue(adobe.Success);
        Assert.AreEqual("Hello", adobe.Text);
    }
}
