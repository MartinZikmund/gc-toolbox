using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class RomanNumeralCodecTests
{
    private const char Bar = '̅'; // combining overline (vinculum)

    private readonly RomanNumeralCodec _codec = new();

    /// <summary>Returns <paramref name="symbols"/> with a combining overline after each character.</summary>
    private static string Over(string symbols) => string.Concat(symbols.Select(c => $"{c}{Bar}"));

    // ---- Encode: canonical 1–3999 ----

    [DataTestMethod]
    [DataRow(1, "I")]
    [DataRow(4, "IV")]
    [DataRow(9, "IX")]
    [DataRow(40, "XL")]
    [DataRow(90, "XC")]
    [DataRow(400, "CD")]
    [DataRow(900, "CM")]
    [DataRow(1994, "MCMXCIV")]
    [DataRow(2023, "MMXXIII")]
    [DataRow(3999, "MMMCMXCIX")]
    public void Encode_StandardRange_ProducesCanonicalNumeral(int value, string expected)
        => Assert.AreEqual(expected, _codec.Encode(value));

    // ---- Encode: vinculum 4000+ ----

    [TestMethod]
    public void Encode_4000_OverlinesTheThousandsBlock()
        => Assert.AreEqual(Over("IV"), _codec.Encode(4000));

    [TestMethod]
    public void Encode_5000_IsOverlinedV()
        => Assert.AreEqual(Over("V"), _codec.Encode(5000));

    [TestMethod]
    public void Encode_OneMillion_IsOverlinedM()
        => Assert.AreEqual(Over("M"), _codec.Encode(1_000_000));

    [TestMethod]
    public void Encode_4999_OverlinesThousandsThenAppendsRemainder()
        => Assert.AreEqual(Over("IV") + "CMXCIX", _codec.Encode(4999));

    [TestMethod]
    public void Encode_Max_ProducesFullyOverlinedThousandsPlusRemainder()
        => Assert.AreEqual(Over("MMMCMXCIX") + "CMXCIX", _codec.Encode(3_999_999));

    [TestMethod]
    public void Encode_JustBelowVinculum_StaysNonOverlined()
    {
        // 3999 has no vinculum; 2500 is MMD (the M=1000 symbol, not overlined).
        Assert.AreEqual("MMD", _codec.Encode(2500));
        Assert.IsFalse(_codec.Encode(3999).Contains(Bar));
    }

    // ---- Encode: range guard ----

    [DataTestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    [DataRow(4_000_000L)]
    public void Encode_OutOfRange_Throws(long value)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _codec.Encode(value));

    // ---- Decode: canonical ----

    [DataTestMethod]
    [DataRow("I", 1)]
    [DataRow("IV", 4)]
    [DataRow("MCMXCIV", 1994)]
    [DataRow("MMMCMXCIX", 3999)]
    public void Decode_CanonicalNumeral_ReturnsValueAndIsCanonical(string text, int expected)
    {
        Assert.IsTrue(_codec.TryDecode(text, out var value, out var isCanonical));
        Assert.AreEqual(expected, value);
        Assert.IsTrue(isCanonical);
    }

    [TestMethod]
    public void Decode_IsCaseInsensitive()
    {
        Assert.IsTrue(_codec.TryDecode("mcmxciv", out var value, out _));
        Assert.AreEqual(1994L, value);
    }

    [TestMethod]
    public void Decode_TrimsSurroundingWhitespace()
    {
        Assert.IsTrue(_codec.TryDecode("  IV  ", out var value, out _));
        Assert.AreEqual(4L, value);
    }

    // ---- Decode: vinculum ----

    [TestMethod]
    public void Decode_OverlinedV_Is5000()
    {
        Assert.IsTrue(_codec.TryDecode(Over("V"), out var value, out var isCanonical));
        Assert.AreEqual(5000L, value);
        Assert.IsTrue(isCanonical);
    }

    [TestMethod]
    public void Decode_OverlinedIV_Is4000()
    {
        Assert.IsTrue(_codec.TryDecode(Over("IV"), out var value, out var isCanonical));
        Assert.AreEqual(4000L, value);
        Assert.IsTrue(isCanonical);
    }

    // ---- Decode: lenient + canonicality flag ----

    [DataTestMethod]
    [DataRow("IIII", 4)]
    [DataRow("VIIII", 9)]
    [DataRow("XXXXX", 50)]
    public void Decode_NonStandardAdditiveForm_ComputesValueButFlagsNonCanonical(string text, int expected)
    {
        Assert.IsTrue(_codec.TryDecode(text, out var value, out var isCanonical));
        Assert.AreEqual(expected, value);
        Assert.IsFalse(isCanonical);
    }

    [TestMethod]
    public void Decode_NonStandardVinculumSubtractive_ComputesButFlagsNonCanonical()
    {
        // M (1000) before V-bar (5000) computes 4000, but canonical 4000 is I̅V̅, so it is flagged.
        Assert.IsTrue(_codec.TryDecode("M" + Over("V"), out var value, out var isCanonical));
        Assert.AreEqual(4000L, value);
        Assert.IsFalse(isCanonical);
    }

    // ---- Decode: invalid ----

    [DataTestMethod]
    [DataRow("ABC")]
    [DataRow("IVX!")]
    [DataRow("I V")]   // internal whitespace is not a valid single numeral
    [DataRow("")]
    [DataRow("   ")]
    public void Decode_InvalidInput_ReturnsFalse(string text)
        => Assert.IsFalse(_codec.TryDecode(text, out _, out _));

    [TestMethod]
    public void Decode_Null_ReturnsFalse()
        => Assert.IsFalse(_codec.TryDecode(null, out _, out _));

    [TestMethod]
    public void Decode_StrayOverline_ReturnsFalse()
        => Assert.IsFalse(_codec.TryDecode(Bar.ToString(), out _, out _));

    // ---- Round trip ----

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(49)]
    [DataRow(1994)]
    [DataRow(3999)]
    [DataRow(4000)]
    [DataRow(5000)]
    [DataRow(123456)]
    [DataRow(1_000_000)]
    [DataRow(3_999_999)]
    public void RoundTrip_EncodeThenDecode_PreservesValueAndIsCanonical(int value)
    {
        Assert.IsTrue(_codec.TryDecode(_codec.Encode(value), out var decoded, out var isCanonical));
        Assert.AreEqual((long)value, decoded);
        Assert.IsTrue(isCanonical);
    }

    // ---- Explain ----

    [TestMethod]
    public void Explain_DecomposesIntoOrderedAdditiveParts()
    {
        var parts = _codec.Explain(1994);

        CollectionAssert.AreEqual(
            new[]
            {
                new RomanNumeralPart("M", 1000),
                new RomanNumeralPart("CM", 900),
                new RomanNumeralPart("XC", 90),
                new RomanNumeralPart("IV", 4),
            },
            parts.ToArray());
    }

    [TestMethod]
    public void Explain_RepeatedSymbols_AreSeparateParts()
    {
        var parts = _codec.Explain(2023);

        CollectionAssert.AreEqual(
            new[]
            {
                new RomanNumeralPart("M", 1000),
                new RomanNumeralPart("M", 1000),
                new RomanNumeralPart("X", 10),
                new RomanNumeralPart("X", 10),
                new RomanNumeralPart("I", 1),
                new RomanNumeralPart("I", 1),
                new RomanNumeralPart("I", 1),
            },
            parts.ToArray());
    }

    [TestMethod]
    public void Explain_VinculumValue_UsesOverlinedSymbols()
    {
        var parts = _codec.Explain(4500);

        CollectionAssert.AreEqual(
            new[]
            {
                new RomanNumeralPart(Over("IV"), 4000),
                new RomanNumeralPart("D", 500),
            },
            parts.ToArray());
    }

    [TestMethod]
    public void Explain_PartsConcatenateToTheEncodedNumeral()
    {
        foreach (var value in new[] { 1, 1994, 4000, 4500, 3_999_999 })
        {
            Assert.AreEqual(_codec.Encode(value), string.Concat(_codec.Explain(value).Select(p => p.Symbol)));
        }
    }
}
