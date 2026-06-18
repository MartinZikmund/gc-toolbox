using System.Text;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class Base32CodecTests
{
    private readonly Base32Codec _codec = new();

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    // ---- Encode: RFC 4648 known vectors (section 10) ----

    [DataTestMethod]
    [DataRow("", "")]
    [DataRow("f", "MY======")]
    [DataRow("fo", "MZXQ====")]
    [DataRow("foo", "MZXW6===")]
    [DataRow("foob", "MZXW6YQ=")]
    [DataRow("fooba", "MZXW6YTB")]
    [DataRow("foobar", "MZXW6YTBOI======")]
    public void Encode_Rfc4648_MatchesKnownVectors(string input, string expected)
        => Assert.AreEqual(expected, _codec.Encode(Ascii(input), Base32Variant.Rfc4648));

    [TestMethod]
    public void Encode_Base32Hex_Foobar_MatchesKnownVector()
        => Assert.AreEqual("CPNMUOJ1E8======", _codec.Encode(Ascii("foobar"), Base32Variant.Base32Hex));

    // ---- Encode: padding toggle ----

    [TestMethod]
    public void Encode_WithoutPadding_OmitsEqualsSigns()
        => Assert.AreEqual("MY", _codec.Encode(Ascii("f"), Base32Variant.Rfc4648, padding: false));

    [TestMethod]
    public void Encode_FullBlock_HasNoPaddingRegardlessOfFlag()
        => Assert.AreEqual("MZXW6YTB", _codec.Encode(Ascii("fooba"), Base32Variant.Rfc4648, padding: true));

    [TestMethod]
    public void Encode_EmptyInput_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, _codec.Encode([], Base32Variant.Rfc4648));

    // ---- Encode: other variants produce valid charset ----

    [TestMethod]
    public void Encode_Crockford_UsesCrockfordAlphabet()
    {
        // "foobar" -> RFC bits map onto Crockford alphabet 0-9 A-Z (no I L O U), no padding by default.
        var result = _codec.Encode(Ascii("foobar"), Base32Variant.Crockford);
        Assert.AreEqual("CSQPYRK1E8", result);
    }

    [TestMethod]
    public void Encode_ZBase32_UsesZBaseAlphabet()
    {
        // z-base-32 is unpadded by convention.
        var result = _codec.Encode(Ascii("foobar"), Base32Variant.ZBase32);
        Assert.IsFalse(result.Contains('='));
        Assert.AreEqual(_codec.Encode(Ascii("foobar"), Base32Variant.Rfc4648, padding: false).Length, result.Length);
    }

    // ---- Decode: round trip every variant ----

    [DataTestMethod]
    [DataRow(Base32Variant.Rfc4648)]
    [DataRow(Base32Variant.Base32Hex)]
    [DataRow(Base32Variant.Crockford)]
    [DataRow(Base32Variant.ZBase32)]
    public void RoundTrip_EncodeThenDecode_PreservesBytes(Base32Variant variant)
    {
        foreach (var sample in new[] { "", "f", "fo", "foo", "foob", "fooba", "foobar", "geocaching!" })
        {
            var bytes = Ascii(sample);
            var encoded = _codec.Encode(bytes, variant);
            Assert.IsTrue(_codec.TryDecode(encoded, variant, out var decoded, out _, out _), $"decode failed for '{sample}' / {variant}");
            CollectionAssert.AreEqual(bytes, decoded, $"round trip mismatch for '{sample}' / {variant}");
        }
    }

    [TestMethod]
    public void RoundTrip_Utf8Czech_PreservesBytes()
    {
        var bytes = Encoding.UTF8.GetBytes("Příšerně žluťoučký kůň");
        var encoded = _codec.Encode(bytes, Base32Variant.Rfc4648);
        Assert.IsTrue(_codec.TryDecode(encoded, Base32Variant.Rfc4648, out var decoded, out _, out _));
        CollectionAssert.AreEqual(bytes, decoded);
    }

    // ---- Decode: known vectors ----

    [DataTestMethod]
    [DataRow("MY======", "f")]
    [DataRow("MZXW6===", "foo")]
    [DataRow("MZXW6YTBOI======", "foobar")]
    public void Decode_Rfc4648KnownVector_ReturnsBytes(string text, string expected)
    {
        Assert.IsTrue(_codec.TryDecode(text, Base32Variant.Rfc4648, out var decoded, out _, out _));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(decoded));
    }

    // ---- Decode: lenient ----

    [TestMethod]
    public void Decode_LowercaseAndSpaces_StillDecodes()
    {
        Assert.IsTrue(_codec.TryDecode("mzxw 6yt boi", Base32Variant.Rfc4648, out var decoded, out _, out _));
        Assert.AreEqual("foobar", Encoding.ASCII.GetString(decoded));
    }

    [TestMethod]
    public void Decode_UnpaddedInput_StillDecodes()
    {
        Assert.IsTrue(_codec.TryDecode("MZXW6", Base32Variant.Rfc4648, out var decoded, out _, out _));
        Assert.AreEqual("foo", Encoding.ASCII.GetString(decoded));
    }

    [TestMethod]
    public void Decode_NewlinesAndHyphens_AreIgnored()
    {
        Assert.IsTrue(_codec.TryDecode("MZXW6\nYTB-OI", Base32Variant.Rfc4648, out var decoded, out _, out _));
        Assert.AreEqual("foobar", Encoding.ASCII.GetString(decoded));
    }

    [TestMethod]
    public void Decode_CrockfordAmbiguousLookAlikes_AreNormalized()
    {
        // o -> 0, i/l -> 1 on Crockford decode; hyphens ignored; lowercase accepted.
        var encoded = _codec.Encode(Ascii("foobar"), Base32Variant.Crockford);
        var messed = encoded.ToLowerInvariant().Replace("1", "I"); // re-introduce an ambiguous look-alike
        Assert.IsTrue(_codec.TryDecode(messed, Base32Variant.Crockford, out var decoded, out _, out _));
        Assert.AreEqual("foobar", Encoding.ASCII.GetString(decoded));
    }

    // ---- Decode: invalid char reporting ----

    [TestMethod]
    public void Decode_InvalidChar_ReportsFirstInvalidAndPosition()
    {
        // '0' and '1' are not in the RFC 4648 alphabet (A-Z 2-7); the '1' is the first invalid char.
        Assert.IsFalse(_codec.TryDecode("MZ1XW6", Base32Variant.Rfc4648, out _, out var error, out _));
        Assert.IsNotNull(error);
        Assert.AreEqual('1', error!.Value.InvalidChar);
        Assert.AreEqual(2, error.Value.Position);
    }

    [TestMethod]
    public void Decode_InvalidChar_PositionCountsOriginalIndex()
    {
        // Leading space is skipped but still counted in the reported position.
        Assert.IsFalse(_codec.TryDecode("MZ XW8", Base32Variant.Rfc4648, out _, out var error, out _));
        Assert.IsNotNull(error);
        Assert.AreEqual('8', error!.Value.InvalidChar);
        Assert.AreEqual(5, error.Value.Position);
    }

    [TestMethod]
    public void Decode_EmptyInput_SucceedsWithEmptyBytes()
    {
        Assert.IsTrue(_codec.TryDecode("", Base32Variant.Rfc4648, out var decoded, out var error, out _));
        Assert.AreEqual(0, decoded.Length);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void Decode_OnlySeparators_SucceedsWithEmptyBytes()
    {
        Assert.IsTrue(_codec.TryDecode("  --  \n", Base32Variant.Rfc4648, out var decoded, out _, out _));
        Assert.AreEqual(0, decoded.Length);
    }

    // ---- Custom alphabet ----

    [TestMethod]
    public void EncodeCustom_RoundTrips()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; // same symbols as RFC, valid 32-distinct
        var bytes = Ascii("foobar");
        var encoded = _codec.EncodeCustom(bytes, alphabet, padding: false);
        Assert.IsTrue(_codec.TryDecodeCustom(encoded, alphabet, out var decoded, out _));
        CollectionAssert.AreEqual(bytes, decoded);
    }

    [DataTestMethod]
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYZ23456")]   // 31 symbols
    [DataRow("AABCDEFGHIJKLMNOPQRSTUVWXYZ23456")]  // 32 but duplicate 'A'
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYZ2345678")] // 33 symbols
    public void ValidateCustomAlphabet_WrongShapeOrDuplicates_ReturnsFalse(string alphabet)
        => Assert.IsFalse(Base32Codec.IsValidCustomAlphabet(alphabet));

    [TestMethod]
    public void ValidateCustomAlphabet_Exactly32Distinct_ReturnsTrue()
        => Assert.IsTrue(Base32Codec.IsValidCustomAlphabet("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));

    // ---- AutoDetect / TryAllVariants ----

    [TestMethod]
    public void AutoDetect_Base32HexSpecificChars_PicksBase32Hex()
    {
        // base32hex alphabet includes 0/1 and excludes W-Z; '0' rules out RFC 4648.
        var encoded = _codec.Encode(Ascii("foobar"), Base32Variant.Base32Hex); // "CPNMUOJ1E8======"
        Assert.AreEqual(Base32Variant.Base32Hex, _codec.AutoDetect(encoded));
    }

    [TestMethod]
    public void AutoDetect_PlainUppercaseLetters_DefaultsToRfc4648()
        => Assert.AreEqual(Base32Variant.Rfc4648, _codec.AutoDetect("MZXW6YTB"));

    [TestMethod]
    public void TryAllVariants_ReturnsOneResultPerVariant()
    {
        var encoded = _codec.Encode(Ascii("foobar"), Base32Variant.Rfc4648);
        var results = _codec.TryAllVariants(encoded);

        Assert.AreEqual(4, results.Count);
        var rfc = results.First(r => r.Variant == Base32Variant.Rfc4648);
        Assert.IsTrue(rfc.Success);
        Assert.AreEqual("foobar", Encoding.ASCII.GetString(rfc.Bytes));
    }
}
