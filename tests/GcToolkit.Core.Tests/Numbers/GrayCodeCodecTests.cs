using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class GrayCodeCodecTests
{
    private readonly GrayCodeCodec _codec = new();

    // ---- Encode: the canonical 3-bit Gray sequence (known vectors) ----

    [DataTestMethod]
    [DataRow(0UL, 0UL)]
    [DataRow(1UL, 1UL)]
    [DataRow(2UL, 3UL)]
    [DataRow(3UL, 2UL)]
    [DataRow(4UL, 6UL)]
    [DataRow(5UL, 7UL)]
    [DataRow(6UL, 5UL)]
    [DataRow(7UL, 4UL)]
    public void Encode_KnownVectors_MatchReflectedBinarySequence(ulong value, ulong expectedGray)
        => Assert.AreEqual(expectedGray, _codec.Encode(value));

    [DataTestMethod]
    [DataRow(0UL, "000")]
    [DataRow(1UL, "001")]
    [DataRow(2UL, "011")]
    [DataRow(3UL, "010")]
    [DataRow(4UL, "110")]
    [DataRow(5UL, "111")]
    [DataRow(6UL, "101")]
    [DataRow(7UL, "100")]
    public void EncodeToBinary_ThreeBitWidth_MatchesPuzzleTable(ulong value, string expected)
        => Assert.AreEqual(expected, _codec.EncodeToBinary(value, bitWidth: 3));

    [TestMethod]
    public void Encode_UsesXorOfValueAndShiftedValue()
    {
        // Definition: gray = value ^ (value >> 1).
        for (ulong v = 0; v < 1024; v++)
        {
            Assert.AreEqual(v ^ (v >> 1), _codec.Encode(v));
        }
    }

    // ---- Decode: inverse of encode ----

    [DataTestMethod]
    [DataRow(0UL, 0UL)]
    [DataRow(1UL, 1UL)]
    [DataRow(3UL, 2UL)]
    [DataRow(2UL, 3UL)]
    [DataRow(6UL, 4UL)]
    [DataRow(7UL, 5UL)]
    [DataRow(5UL, 6UL)]
    [DataRow(4UL, 7UL)]
    public void Decode_KnownVectors_RecoverOriginalValue(ulong gray, ulong expectedValue)
        => Assert.AreEqual(expectedValue, _codec.Decode(gray));

    // ---- Round trips ----

    [DataTestMethod]
    [DataRow(0UL)]
    [DataRow(1UL)]
    [DataRow(42UL)]
    [DataRow(255UL)]
    [DataRow(1024UL)]
    [DataRow(65535UL)]
    [DataRow(ulong.MaxValue)]
    public void RoundTrip_DecodeEncode_PreservesValue(ulong value)
    {
        Assert.AreEqual(value, _codec.Decode(_codec.Encode(value)));
        Assert.AreEqual(value, _codec.Encode(_codec.Decode(value)));
    }

    [TestMethod]
    public void RoundTrip_Range_AllValuesPreserved()
    {
        for (ulong v = 0; v < 4096; v++)
        {
            Assert.AreEqual(v, _codec.Decode(_codec.Encode(v)));
        }
    }

    [TestMethod]
    public void Encode_LargeValues_MatchDefinition()
    {
        Assert.AreEqual(255UL ^ (255UL >> 1), _codec.Encode(255));
        Assert.AreEqual(1024UL ^ (1024UL >> 1), _codec.Encode(1024));
        Assert.AreEqual(65535UL ^ (65535UL >> 1), _codec.Encode(65535));
    }

    // ---- Single-bit property: consecutive Gray codes differ by exactly one bit ----

    [TestMethod]
    public void Encode_ConsecutiveValues_DifferByExactlyOneBit()
    {
        for (ulong v = 0; v < 2048; v++)
        {
            var diff = _codec.Encode(v) ^ _codec.Encode(v + 1);
            var bitCount = (int)ulong.PopCount(diff);
            Assert.AreEqual(1, bitCount, $"Gray({v}) and Gray({v + 1}) must differ by one bit.");
        }
    }

    // ---- Parsing: decimal / binary / hex / prefixes ----

    [DataTestMethod]
    [DataRow("42", 42UL)]
    [DataRow("  42  ", 42UL)]
    [DataRow("0", 0UL)]
    [DataRow("255", 255UL)]
    public void TryParse_Decimal_Succeeds(string text, ulong expected)
    {
        Assert.IsTrue(_codec.TryParse(text, NumberRadix.Decimal, out var value));
        Assert.AreEqual(expected, value);
    }

    [DataTestMethod]
    [DataRow("101", 5UL)]
    [DataRow("0b101", 5UL)]
    [DataRow("11111111", 255UL)]
    [DataRow(" 1010 ", 10UL)]
    public void TryParse_Binary_Succeeds(string text, ulong expected)
    {
        Assert.IsTrue(_codec.TryParse(text, NumberRadix.Binary, out var value));
        Assert.AreEqual(expected, value);
    }

    [DataTestMethod]
    [DataRow("ff", 255UL)]
    [DataRow("0xFF", 255UL)]
    [DataRow("1A", 26UL)]
    public void TryParse_Hex_Succeeds(string text, ulong expected)
    {
        Assert.IsTrue(_codec.TryParse(text, NumberRadix.Hexadecimal, out var value));
        Assert.AreEqual(expected, value);
    }

    [DataTestMethod]
    [DataRow("755", 493UL)]
    [DataRow("0o17", 15UL)]
    public void TryParse_Octal_Succeeds(string text, ulong expected)
    {
        Assert.IsTrue(_codec.TryParse(text, NumberRadix.Octal, out var value));
        Assert.AreEqual(expected, value);
    }

    [DataTestMethod]
    [DataRow("", NumberRadix.Decimal)]
    [DataRow("   ", NumberRadix.Decimal)]
    [DataRow("abc", NumberRadix.Decimal)]
    [DataRow("12.5", NumberRadix.Decimal)]
    [DataRow("-5", NumberRadix.Decimal)]
    [DataRow("102", NumberRadix.Binary)]   // 2 is not a binary digit
    [DataRow("0b", NumberRadix.Binary)]    // prefix only, no digits
    [DataRow("xyz", NumberRadix.Hexadecimal)]
    [DataRow("99", NumberRadix.Octal)]     // 9 is not an octal digit
    public void TryParse_InvalidInput_ReturnsFalse(string text, NumberRadix radix)
        => Assert.IsFalse(_codec.TryParse(text, radix, out _));

    [TestMethod]
    public void TryParse_Null_ReturnsFalse()
        => Assert.IsFalse(_codec.TryParse(null, NumberRadix.Decimal, out _));

    [TestMethod]
    public void TryParse_OverflowsUInt64_ReturnsFalse()
        => Assert.IsFalse(_codec.TryParse("18446744073709551616", NumberRadix.Decimal, out _));

    // ---- Auto-detect radix via prefixes ----

    [DataTestMethod]
    [DataRow("0xFF", 255UL)]
    [DataRow("0b101", 5UL)]
    [DataRow("0o17", 15UL)]
    [DataRow("42", 42UL)]
    public void TryParseAuto_DetectsRadixFromPrefix(string text, ulong expected)
    {
        Assert.IsTrue(_codec.TryParseAuto(text, NumberRadix.Decimal, out var value, out _));
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void TryParseAuto_NoPrefix_UsesFallbackRadix()
    {
        Assert.IsTrue(_codec.TryParseAuto("101", NumberRadix.Binary, out var value, out var detected));
        Assert.AreEqual(5UL, value);
        Assert.AreEqual(NumberRadix.Binary, detected);
    }

    // ---- Formatting ----

    [DataTestMethod]
    [DataRow(5UL, NumberRadix.Decimal, "5")]
    [DataRow(5UL, NumberRadix.Binary, "101")]
    [DataRow(255UL, NumberRadix.Hexadecimal, "FF")]
    [DataRow(493UL, NumberRadix.Octal, "755")]
    public void Format_AutoWidth_ProducesExpected(ulong value, NumberRadix radix, string expected)
        => Assert.AreEqual(expected, _codec.Format(value, radix, bitWidth: 0));

    [DataTestMethod]
    [DataRow(5UL, 8, "00000101")]
    [DataRow(5UL, 4, "0101")]
    [DataRow(0UL, 5, "00000")]
    public void FormatBinary_FixedWidth_ZeroPads(ulong value, int width, string expected)
        => Assert.AreEqual(expected, _codec.Format(value, NumberRadix.Binary, width));

    [TestMethod]
    public void Format_WidthSmallerThanValue_DoesNotTruncate()
        => Assert.AreEqual("100000000", _codec.Format(256, NumberRadix.Binary, bitWidth: 4));

    // ---- XOR cascade explanation ----

    [TestMethod]
    public void ExplainEncode_ShowsValueShiftAndGrayBits()
    {
        // 6 = 110, 6 >> 1 = 011, gray = 101.
        var steps = _codec.ExplainEncode(6, bitWidth: 3);

        Assert.AreEqual("110", steps.ValueBits);
        Assert.AreEqual("011", steps.ShiftedBits);
        Assert.AreEqual("101", steps.GrayBits);
        Assert.AreEqual(6UL, steps.Value);
        Assert.AreEqual(5UL, steps.Gray);
    }

    [TestMethod]
    public void ExplainEncode_AutoWidth_UsesSignificantBitCount()
    {
        var steps = _codec.ExplainEncode(5, bitWidth: 0);
        // 5 = 101, gray = 111; auto width is 3.
        Assert.AreEqual("101", steps.ValueBits);
        Assert.AreEqual("111", steps.GrayBits);
    }

    // ---- Letter mode (A=1..Z=26 through Gray code) ----

    [TestMethod]
    public void EncodeLetter_A_GraysItsPosition()
    {
        // A -> 1 -> gray 1.
        Assert.IsTrue(_codec.TryEncodeLetter('A', out var gray));
        Assert.AreEqual(1UL, gray);
    }

    [TestMethod]
    public void DecodeLetter_RoundTripsThroughGray()
    {
        foreach (var letter in "ABCXYZ")
        {
            Assert.IsTrue(_codec.TryEncodeLetter(letter, out var gray));
            Assert.IsTrue(_codec.TryDecodeLetter(gray, out var recovered));
            Assert.AreEqual(char.ToUpperInvariant(letter), recovered);
        }
    }

    [TestMethod]
    public void TryEncodeLetter_NonLetter_ReturnsFalse()
        => Assert.IsFalse(_codec.TryEncodeLetter('5', out _));

    // ---- Letter mode letter->letter (the "Gray code hides a word" transform) ----

    [DataTestMethod]
    [DataRow('A', 'A')] // pos 1 -> gray 1 -> A (fixed point)
    [DataRow('B', 'C')] // pos 2 -> gray 3 -> C
    [DataRow('C', 'B')] // pos 3 -> gray 2 -> B
    [DataRow('D', 'F')] // pos 4 -> gray 6 -> F
    public void TryEncodeLetterToLetter_KnownPairs_TransformsNotIdentity(char input, char expected)
    {
        Assert.IsTrue(_codec.TryEncodeLetterToLetter(input, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TryEncodeLetterToLetter_IsNotIdentity_ForB()
    {
        // Regression: the encode path used to round-trip back to the input letter (identity).
        Assert.IsTrue(_codec.TryEncodeLetterToLetter('B', out var encoded));
        Assert.AreNotEqual('B', encoded);
    }

    [TestMethod]
    public void DecodeLetterToLetter_InvertsEncodeLetterToLetter_WhenInRange()
    {
        foreach (var letter in "ABCDEFGHIJKLMNOPQ")
        {
            if (_codec.TryEncodeLetterToLetter(letter, out var encoded))
            {
                Assert.IsTrue(_codec.TryDecodeLetterToLetter(encoded, out var recovered));
                Assert.AreEqual(letter, recovered);
            }
        }
    }

    [DataTestMethod]
    [DataRow('R')] // pos 18 -> gray 27 (out of A..Z)
    [DataRow('T')] // pos 20 -> gray 30
    [DataRow('U')] // pos 21 -> gray 31
    public void TryEncodeLetterToLetter_GrayPositionOutsideAlphabet_ReturnsFalse(char input)
        => Assert.IsFalse(_codec.TryEncodeLetterToLetter(input, out _));

    [TestMethod]
    public void TryEncodeLetterToLetter_NonLetter_ReturnsFalse()
        => Assert.IsFalse(_codec.TryEncodeLetterToLetter('7', out _));
}
