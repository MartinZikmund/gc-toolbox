using System.Numerics;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class NumberBaseConverterTests
{
    private readonly NumberBaseConverter _converter = new();

    // ---- Parse: a value in a source base -> BigInteger ----

    [TestMethod]
    [DataRow("255", 10, "255")]
    [DataRow("11111111", 2, "255")]
    [DataRow("377", 8, "255")]
    [DataRow("FF", 16, "255")]
    [DataRow("ff", 16, "255")]            // case-insensitive input
    [DataRow("Z", 36, "35")]
    [DataRow("z", 36, "35")]
    [DataRow("0", 2, "0")]
    [DataRow("10", 16, "16")]
    public void TryParse_ValidDigits_ReturnsValue(string text, int fromBase, string expectedDecimal)
    {
        var ok = _converter.TryParse(text, fromBase, out var value);
        Assert.IsTrue(ok);
        Assert.AreEqual(BigInteger.Parse(expectedDecimal), value);
    }

    [TestMethod]
    public void TryParse_LeadingAndTrailingWhitespace_IsTrimmed()
    {
        Assert.IsTrue(_converter.TryParse("  FF  ", 16, out var value));
        Assert.AreEqual((BigInteger)255, value);
    }

    [TestMethod]
    public void TryParse_NegativeValue_ParsesWithLeadingMinus()
    {
        Assert.IsTrue(_converter.TryParse("-10", 16, out var value));
        Assert.AreEqual((BigInteger)(-16), value);
    }

    [TestMethod]
    public void TryParse_PlusSign_IsAccepted()
    {
        Assert.IsTrue(_converter.TryParse("+FF", 16, out var value));
        Assert.AreEqual((BigInteger)255, value);
    }

    // ---- Parse: invalid digit for the base -> failure (no throw) ----

    [TestMethod]
    [DataRow("2", 2)]      // '2' is not a binary digit
    [DataRow("8", 8)]      // '8' is not an octal digit
    [DataRow("G", 16)]     // 'G' >= base 16
    [DataRow("FG", 16)]    // one bad digit anywhere fails the whole token
    [DataRow("!", 10)]     // punctuation
    [DataRow(" ", 10)]     // only whitespace
    [DataRow("", 10)]      // empty
    [DataRow(null, 10)]    // null
    [DataRow("-", 10)]     // sign with no digits
    public void TryParse_InvalidDigit_ReturnsFalse(string? text, int fromBase)
        => Assert.IsFalse(_converter.TryParse(text, fromBase, out _));

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    [DataRow(63)]
    [DataRow(-5)]
    public void TryParse_BaseOutOfRange_ReturnsFalse(int fromBase)
        => Assert.IsFalse(_converter.TryParse("1", fromBase, out _));

    // ---- Bases 37-62: 0-9, a-z (10-35), A-Z (36-61), always case-sensitive ----

    [TestMethod]
    [DataRow("a", 62, "10")]
    [DataRow("z", 62, "35")]
    [DataRow("A", 62, "36")]
    [DataRow("Z", 62, "61")]
    [DataRow("47", 62, "255")]
    [DataRow("10", 62, "62")]
    public void TryParse_ExtendedBase_UsesBothLetterCasesAsDistinctDigits(string text, int fromBase, string expectedDecimal)
    {
        Assert.IsTrue(_converter.TryParse(text, fromBase, out var value));
        Assert.AreEqual(BigInteger.Parse(expectedDecimal), value);
    }

    [TestMethod]
    public void TryParse_ExtendedBase_RejectsGlyphsAboveTheRadix()
    {
        // 'A' is digit 36 — the last digit of base 37; 'B' (37) is already out of range.
        Assert.IsTrue(_converter.TryParse("A", 37, out var value));
        Assert.AreEqual((BigInteger)36, value);
        Assert.IsFalse(_converter.TryParse("B", 37, out _));
        Assert.IsTrue(_converter.TryParse("B", 38, out _));
    }

    [TestMethod]
    [DataRow(36, false)]
    [DataRow(37, true)]
    [DataRow(62, true)]
    public void RequiresCaseSensitivity_OnlyAboveBase36(int radix, bool expected)
        => Assert.AreEqual(expected, NumberBaseConverter.RequiresCaseSensitivity(radix));

    // ---- Case sensitivity switch (bases 2-36) ----

    [TestMethod]
    public void TryParse_CaseSensitive_RejectsLowerCaseDigits()
    {
        Assert.IsFalse(_converter.TryParse("ff", 16, caseSensitive: true, out _));
        Assert.IsTrue(_converter.TryParse("FF", 16, caseSensitive: true, out var value));
        Assert.AreEqual((BigInteger)255, value);
    }

    [TestMethod]
    public void TryParse_CaseSensitive_IsIgnoredAboveBase36()
    {
        // The base-62 alphabet already distinguishes the cases, so the flag can't change the reading.
        Assert.IsTrue(_converter.TryParse("aA", 62, caseSensitive: false, out var insensitive));
        Assert.IsTrue(_converter.TryParse("aA", 62, caseSensitive: true, out var sensitive));
        Assert.AreEqual(insensitive, sensitive);
        Assert.AreEqual((BigInteger)((10 * 62) + 36), sensitive);
    }

    // ---- Digit reference ----

    [TestMethod]
    public void DigitsFor_ReturnsTheOrderedGlyphsOfTheBase()
    {
        Assert.AreEqual("01", NumberBaseConverter.DigitsFor(2));
        Assert.AreEqual("0123456789ABCDEF", NumberBaseConverter.DigitsFor(16));
        Assert.AreEqual(62, NumberBaseConverter.DigitsFor(62).Length);
        Assert.AreEqual("0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", NumberBaseConverter.DigitsFor(62));
        Assert.AreEqual(string.Empty, NumberBaseConverter.DigitsFor(63));
    }

    // ---- Format: BigInteger -> a value in a target base ----

    [TestMethod]
    [DataRow("255", 2, "11111111")]
    [DataRow("255", 8, "377")]
    [DataRow("255", 10, "255")]
    [DataRow("255", 16, "FF")]           // upper-case digits by convention
    [DataRow("35", 36, "Z")]
    [DataRow("0", 2, "0")]
    [DataRow("0", 16, "0")]
    [DataRow("16", 16, "10")]
    public void Format_Value_ProducesDigitsForBase(string decimalValue, int toBase, string expected)
        => Assert.AreEqual(expected, _converter.Format(BigInteger.Parse(decimalValue), toBase));

    [TestMethod]
    public void Format_NegativeValue_HasLeadingMinus()
        => Assert.AreEqual("-FF", _converter.Format((BigInteger)(-255), 16));

    [TestMethod]
    [DataRow("255", 62, "47")]
    [DataRow("10", 62, "a")]
    [DataRow("35", 62, "z")]
    [DataRow("36", 62, "A")]
    [DataRow("61", 62, "Z")]
    public void Format_ExtendedBase_UsesTheSixtyTwoGlyphAlphabet(string decimalValue, int toBase, string expected)
        => Assert.AreEqual(expected, _converter.Format(BigInteger.Parse(decimalValue), toBase));

    [TestMethod]
    [DataRow(1)]
    [DataRow(63)]
    public void Format_BaseOutOfRange_Throws(int toBase)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _converter.Format(BigInteger.One, toBase));

    // ---- The classic 255 example across the common bases ----

    [TestMethod]
    public void RoundTrip_255Decimal_AcrossCommonBases()
    {
        Assert.IsTrue(_converter.TryParse("255", 10, out var value));
        Assert.AreEqual("11111111", _converter.Format(value, 2));
        Assert.AreEqual("377", _converter.Format(value, 8));
        Assert.AreEqual("255", _converter.Format(value, 10));
        Assert.AreEqual("FF", _converter.Format(value, 16));
    }

    [TestMethod]
    public void RoundTrip_EveryBase_PreservesValue()
    {
        var value = (BigInteger)123456789;
        for (var b = NumberBaseConverter.MinBase; b <= NumberBaseConverter.MaxBase; b++)
        {
            var text = _converter.Format(value, b);
            Assert.IsTrue(_converter.TryParse(text, b, out var back), $"base {b}");
            Assert.AreEqual(value, back, $"base {b}");
        }
    }

    [TestMethod]
    public void RoundTrip_LargeBigInteger_BeyondInt64()
    {
        // Far beyond Int64/UInt64 — proves arbitrary precision (beyond parity).
        var value = BigInteger.Pow(2, 256) + 1;
        var hex = _converter.Format(value, 16);
        Assert.IsTrue(_converter.TryParse(hex, 16, out var back));
        Assert.AreEqual(value, back);
    }

    [TestMethod]
    public void RoundTrip_NegativeLargeBigInteger()
    {
        var value = -BigInteger.Pow(10, 40);
        var text = _converter.Format(value, 2);
        Assert.IsTrue(_converter.TryParse(text, 2, out var back));
        Assert.AreEqual(value, back);
    }

    // ---- ToCommonBases convenience ----

    [TestMethod]
    public void ToCommonBases_255_ReturnsBinOctDecHex()
    {
        Assert.IsTrue(_converter.TryParse("FF", 16, out var value));
        var all = _converter.ToCommonBases(value);

        Assert.AreEqual("11111111", all.Binary);
        Assert.AreEqual("377", all.Octal);
        Assert.AreEqual("255", all.Decimal);
        Assert.AreEqual("FF", all.Hexadecimal);
    }

    // ---- "Show all bases" ----

    [TestMethod]
    public void ToAllBases_CoversEveryRadixFromTwoToSixtyTwo()
    {
        Assert.IsTrue(_converter.TryParse("255", 10, out var value));
        var all = _converter.ToAllBases(value);

        Assert.AreEqual(61, all.Count);
        Assert.AreEqual(2, all[0].Radix);
        Assert.AreEqual("11111111", all[0].Text);
        Assert.AreEqual(62, all[^1].Radix);
        Assert.AreEqual("47", all[^1].Text);
        Assert.AreEqual("FF", all.Single(r => r.Radix == 16).Text);
    }

    // ---- Batch: whitespace-separated values, unknown ones skipped ----

    [TestMethod]
    public void ConvertBatch_ConvertsEachTokenIndependently()
    {
        var results = _converter.ConvertBatch("FF 10 A", 16, 10);

        Assert.AreEqual(3, results.Count);
        Assert.AreEqual("255", results[0].Output);
        Assert.AreEqual("16", results[1].Output);
        Assert.AreEqual("10", results[2].Output);
        Assert.IsTrue(results.All(r => r.IsValid));
    }

    [TestMethod]
    public void ConvertBatch_InvalidToken_IsFlaggedWithoutHaltingTheBatch()
    {
        var results = _converter.ConvertBatch("FF ZZ 10", 16, 10);

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].IsValid);
        Assert.IsFalse(results[1].IsValid);
        Assert.AreEqual("ZZ", results[1].Input);
        Assert.AreEqual(string.Empty, results[1].Output);
        Assert.IsTrue(results[2].IsValid);
        Assert.AreEqual("16", results[2].Output);
    }

    [TestMethod]
    [DataRow("1 10  11")]
    [DataRow("1\n10\t11")]
    public void Tokenize_SplitsOnAnyWhitespaceRun(string text)
        => CollectionAssert.AreEqual(new[] { "1", "10", "11" }, NumberBaseConverter.Tokenize(text));

    [TestMethod]
    public void Tokenize_EmptyInput_ReturnsNoTokens()
    {
        Assert.AreEqual(0, NumberBaseConverter.Tokenize(null).Length);
        Assert.AreEqual(0, NumberBaseConverter.Tokenize("   ").Length);
    }

    // ---- Manual mode: user-defined alphabets ----

    [TestMethod]
    public void IsValidAlphabet_RejectsShortOrRepeatingGlyphSets()
    {
        Assert.IsFalse(NumberBaseConverter.IsValidAlphabet(null));
        Assert.IsFalse(NumberBaseConverter.IsValidAlphabet("A"));
        Assert.IsFalse(NumberBaseConverter.IsValidAlphabet("ABA"));
        Assert.IsTrue(NumberBaseConverter.IsValidAlphabet("AB"));
        Assert.IsTrue(NumberBaseConverter.IsValidAlphabet("0123456789ABCDEFGHJKMNPQRTVWXYZ"));
    }

    [TestMethod]
    public void CanFoldCase_OnlyWhenFoldingKeepsGlyphsDistinct()
    {
        Assert.IsTrue(NumberBaseConverter.CanFoldCase("0123456789ABCDEF"));
        Assert.IsTrue(NumberBaseConverter.CanFoldCase("abcXYZ"));
        Assert.IsFalse(NumberBaseConverter.CanFoldCase("aAbB"));
        Assert.IsFalse(NumberBaseConverter.CanFoldCase(NumberBaseConverter.DigitsFor(62)));
    }

    [TestMethod]
    public void TryParse_CustomAlphabet_ReadsDigitsInGlyphOrder()
    {
        // GC codes: base 31 over 0-9 A-Z minus I, L, O, S, U.
        const string GcBase31 = "0123456789ABCDEFGHJKMNPQRTVWXYZ";

        Assert.IsTrue(_converter.TryParse("16XYD", GcBase31, caseSensitive: false, out var value));
        Assert.AreEqual("16XYD", _converter.Format(value, GcBase31));
    }

    [TestMethod]
    public void TryParse_CustomAlphabet_UnicodeGlyphsAreJustDigits()
    {
        const string Runes = "▲■●◆";

        Assert.IsTrue(_converter.TryParse("■●", Runes, caseSensitive: true, out var value));
        Assert.AreEqual((BigInteger)((1 * 4) + 2), value);
        Assert.AreEqual("■●", _converter.Format(value, Runes));
        Assert.AreEqual("▲", _converter.Format(BigInteger.Zero, Runes));
    }

    [TestMethod]
    public void TryParse_CustomAlphabet_RejectsGlyphsOutsideIt()
        => Assert.IsFalse(_converter.TryParse("XY", "ABCD", caseSensitive: true, out _));

    [TestMethod]
    public void TryParse_CustomAlphabet_InvalidAlphabet_ReturnsFalse()
    {
        Assert.IsFalse(_converter.TryParse("AB", "ABA", caseSensitive: true, out _));
        Assert.IsFalse(_converter.TryParse("A", "A", caseSensitive: true, out _));
    }

    [TestMethod]
    public void TryParse_CustomAlphabet_ClaimingTheMinusGlyph_TreatsItAsADigit()
    {
        // '-' is the alphabet's zero here, so it must not be swallowed as a sign.
        Assert.IsTrue(_converter.TryParse("-+", "-+", caseSensitive: true, out var value));
        Assert.AreEqual(BigInteger.One, value);
    }

    [TestMethod]
    public void Format_InvalidAlphabet_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() => _converter.Format(BigInteger.One, "AA"));

    [TestMethod]
    public void ConvertBatch_CustomAlphabets_SkipsTokensOutsideTheSourceGlyphs()
    {
        var results = _converter.ConvertBatch("AB XY BA", "AB", "01", caseSensitive: true);

        Assert.AreEqual(3, results.Count);
        Assert.AreEqual("1", results[0].Output);    // "AB" = 1, rendered without a leading zero
        Assert.IsFalse(results[1].IsValid);
        Assert.AreEqual("10", results[2].Output);   // "BA" = 2
    }

    // ---- Bounds ----

    [TestMethod]
    public void IsValidBase_AcceptsTwoThroughSixtyTwoOnly()
    {
        Assert.IsFalse(NumberBaseConverter.IsValidBase(1));
        Assert.IsTrue(NumberBaseConverter.IsValidBase(2));
        Assert.IsTrue(NumberBaseConverter.IsValidBase(36));
        Assert.IsTrue(NumberBaseConverter.IsValidBase(62));
        Assert.IsFalse(NumberBaseConverter.IsValidBase(63));
    }
}
