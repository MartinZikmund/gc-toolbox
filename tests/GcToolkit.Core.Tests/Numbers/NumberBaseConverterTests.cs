using System.Numerics;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class NumberBaseConverterTests
{
    private readonly NumberBaseConverter _converter = new();

    // ---- Parse: a value in a source base -> BigInteger ----

    [DataTestMethod]
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

    [DataTestMethod]
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

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(0)]
    [DataRow(37)]
    [DataRow(-5)]
    public void TryParse_BaseOutOfRange_ReturnsFalse(int fromBase)
        => Assert.IsFalse(_converter.TryParse("1", fromBase, out _));

    // ---- Format: BigInteger -> a value in a target base ----

    [DataTestMethod]
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

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(37)]
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
        for (var b = 2; b <= 36; b++)
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

    // ---- Bounds ----

    [TestMethod]
    public void MinAndMaxBase_AreTwoAndThirtySix()
    {
        Assert.AreEqual(2, NumberBaseConverter.MinBase);
        Assert.AreEqual(36, NumberBaseConverter.MaxBase);
    }
}
