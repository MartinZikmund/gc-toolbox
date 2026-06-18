using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class SegmentDisplayCodecTests
{
    private readonly SegmentDisplayCodec _codec = new();

    // ---- Segment label tables ----

    [DataTestMethod]
    [DataRow(SegmentDisplayType.SevenSegment, 7)]
    [DataRow(SegmentDisplayType.NineSegment, 9)]
    [DataRow(SegmentDisplayType.FourteenSegment, 14)]
    [DataRow(SegmentDisplayType.SixteenSegment, 16)]
    public void SegmentCount_PerType_MatchesFamilyName(SegmentDisplayType type, int expected)
        => Assert.AreEqual(expected, SegmentDisplayCodec.SegmentCount(type));

    [TestMethod]
    public void LabelsFor_SevenSegment_AreAtoG()
        => CollectionAssert.AreEqual(
            new[] { "a", "b", "c", "d", "e", "f", "g" },
            SegmentDisplayCodec.LabelsFor(SegmentDisplayType.SevenSegment).ToArray());

    // ---- Known 7-segment glyph vectors ----

    [TestMethod]
    public void Encode_SevenSegment_Eight_LightsAllSegments()
    {
        var glyph = Single(_codec.Encode("8", SegmentDisplayType.SevenSegment));
        CollectionAssert.AreEqual(
            new[] { "a", "b", "c", "d", "e", "f", "g" },
            glyph.Labels.ToArray());
        Assert.AreEqual(SegmentDisplayCodec.FullMask(SegmentDisplayType.SevenSegment), glyph.Mask);
    }

    [TestMethod]
    public void Encode_SevenSegment_One_LightsBandC()
    {
        var glyph = Single(_codec.Encode("1", SegmentDisplayType.SevenSegment));
        CollectionAssert.AreEqual(new[] { "b", "c" }, glyph.Labels.ToArray());
    }

    [TestMethod]
    public void Encode_SevenSegment_Zero_LightsAthroughF()
    {
        var glyph = Single(_codec.Encode("0", SegmentDisplayType.SevenSegment));
        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e", "f" }, glyph.Labels.ToArray());
        // g (bit 6) is off, so the decimal value is 0b0111111 = 63.
        Assert.AreEqual(63, glyph.Decimal);
    }

    [TestMethod]
    public void Encode_SevenSegment_A_LightsAbcefg()
    {
        var glyph = Single(_codec.Encode("A", SegmentDisplayType.SevenSegment));
        CollectionAssert.AreEqual(new[] { "a", "b", "c", "e", "f", "g" }, glyph.Labels.ToArray());
    }

    [TestMethod]
    public void Encode_SevenSegment_Dash_LightsOnlyMiddle()
    {
        var glyph = Single(_codec.Encode("-", SegmentDisplayType.SevenSegment));
        CollectionAssert.AreEqual(new[] { "g" }, glyph.Labels.ToArray());
    }

    [TestMethod]
    public void Encode_Space_ProducesEmptyMask()
    {
        var glyph = Single(_codec.Encode(" ", SegmentDisplayType.SevenSegment));
        Assert.AreEqual(0, glyph.Mask);
        Assert.AreEqual(' ', glyph.Character);
    }

    [TestMethod]
    public void Encode_IsCaseInsensitive()
    {
        var lower = Single(_codec.Encode("a", SegmentDisplayType.SevenSegment));
        var upper = Single(_codec.Encode("A", SegmentDisplayType.SevenSegment));
        Assert.AreEqual(upper.Mask, lower.Mask);
    }

    [TestMethod]
    public void Encode_UnknownCharacter_YieldsUnknownGlyphNotDropped()
    {
        var glyphs = _codec.Encode("@", SegmentDisplayType.SevenSegment);
        Assert.AreEqual(1, glyphs.Count);
        Assert.AreEqual(SegmentDisplayCodec.Unknown, glyphs[0].Character);
        Assert.AreEqual(0, glyphs[0].Mask);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encode_EmptyOrNull_YieldsNoGlyphs(string? text)
        => Assert.AreEqual(0, _codec.Encode(text, SegmentDisplayType.SevenSegment).Count);

    // ---- Decode of explicit masks ----

    [TestMethod]
    public void DecodeMask_FullSeven_IsEight()
    {
        var glyph = _codec.DecodeMask(0b1111111, SegmentDisplayType.SevenSegment);
        Assert.AreEqual('8', glyph.Character);
        Assert.IsTrue(glyph.IsExact);
        Assert.AreEqual(0, glyph.Distance);
    }

    [TestMethod]
    public void DecodeMask_BandC_IsOne()
        => Assert.AreEqual('1', _codec.DecodeMask(0b0000110, SegmentDisplayType.SevenSegment).Character);

    [TestMethod]
    public void DecodeMask_Zero_IsSpace()
    {
        var glyph = _codec.DecodeMask(0, SegmentDisplayType.SevenSegment);
        Assert.AreEqual(' ', glyph.Character);
        Assert.IsTrue(glyph.IsExact);
    }

    // ---- Round-trip: Decode(Encode(x)) == x across types ----

    [DataTestMethod]
    [DataRow(SegmentDisplayType.SevenSegment)]
    [DataRow(SegmentDisplayType.NineSegment)]
    [DataRow(SegmentDisplayType.FourteenSegment)]
    [DataRow(SegmentDisplayType.SixteenSegment)]
    public void RoundTrip_AllDigits_IsLossless(SegmentDisplayType type)
    {
        const string digits = "0123456789";
        var encoded = _codec.Encode(digits, type);
        var codes = string.Join(", ", encoded.Select(g => g.Decimal));
        var decoded = _codec.Decode(codes, type, SegmentNotation.Decimal);
        Assert.AreEqual(digits, decoded.Text);
    }

    [TestMethod]
    public void RoundTrip_FourteenSegmentAlphabet_IsLossless()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var encoded = _codec.Encode(alphabet, SegmentDisplayType.FourteenSegment);
        var codes = string.Join(", ", encoded.Select(g => g.Decimal));
        var decoded = _codec.Decode(codes, SegmentDisplayType.FourteenSegment, SegmentNotation.Decimal);
        Assert.AreEqual(alphabet, decoded.Text);
    }

    [TestMethod]
    public void FourteenSegment_AllLetters_HaveDistinctMasks()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var masks = _codec.Encode(alphabet, SegmentDisplayType.FourteenSegment)
            .Select(g => g.Mask)
            .ToList();
        Assert.AreEqual(alphabet.Length, masks.Distinct().Count(), "letter glyphs must be unambiguous");
    }

    // ---- Notations ----

    [TestMethod]
    public void Format_Labels_JoinsWithSpaces()
    {
        var mask = Single(_codec.Encode("0", SegmentDisplayType.SevenSegment)).Mask;
        Assert.AreEqual("a b c d e f", SegmentDisplayCodec.Format(mask, SegmentDisplayType.SevenSegment, SegmentNotation.Labels));
    }

    [TestMethod]
    public void Format_Binary_IsFixedWidthMsbFirst()
    {
        var mask = Single(_codec.Encode("0", SegmentDisplayType.SevenSegment)).Mask;
        // g (bit 6, leftmost) off, a–f on -> "0111111".
        Assert.AreEqual("0111111", SegmentDisplayCodec.Format(mask, SegmentDisplayType.SevenSegment, SegmentNotation.Binary));
    }

    [TestMethod]
    public void Format_Decimal_IsMaskValue()
    {
        var mask = Single(_codec.Encode("1", SegmentDisplayType.SevenSegment)).Mask;
        Assert.AreEqual("6", SegmentDisplayCodec.Format(mask, SegmentDisplayType.SevenSegment, SegmentNotation.Decimal));
    }

    [TestMethod]
    public void Decode_LabelNotation_RecoversCharacter()
    {
        var decoded = _codec.Decode("a b c d e f", SegmentDisplayType.SevenSegment, SegmentNotation.Labels);
        Assert.AreEqual("0", decoded.Text);
    }

    [TestMethod]
    public void Decode_BinaryNotation_RecoversCharacter()
    {
        var decoded = _codec.Decode("0111111", SegmentDisplayType.SevenSegment, SegmentNotation.Binary);
        Assert.AreEqual("0", decoded.Text);
    }

    [TestMethod]
    public void Decode_MultipleCodes_CommaSeparated()
    {
        var decoded = _codec.Decode("6, 6, 63", SegmentDisplayType.SevenSegment, SegmentNotation.Decimal);
        Assert.AreEqual("110", decoded.Text); // 6=mask for '1', 63='0'
    }

    // ---- Auto-detect ----

    [DataTestMethod]
    [DataRow("a b c", SegmentNotation.Labels)]
    [DataRow("0111111", SegmentNotation.Binary)]
    [DataRow("63", SegmentNotation.Decimal)]
    [DataRow("6", SegmentNotation.Decimal)]
    public void DetectNotation_Classifies(string token, SegmentNotation expected)
        => Assert.AreEqual(expected, SegmentDisplayCodec.DetectNotation(token, SegmentDisplayType.SevenSegment));

    [TestMethod]
    public void Decode_AutoDetect_HandlesMixedNotations()
    {
        // First code is labels, second decimal — auto-detect resolves each independently.
        var decoded = _codec.Decode("b c, 63", SegmentDisplayType.SevenSegment, notation: null);
        Assert.AreEqual("10", decoded.Text);
    }

    // ---- Bit-order toggle ----

    [TestMethod]
    public void Decode_ReverseBits_FlipsBitOrder()
    {
        // '1' is b,c = 0b0000110. Reversed across 7 bits -> 0b0110000 = lit e,f = 'I'.
        var normal = _codec.Decode("0000110", SegmentDisplayType.SevenSegment, SegmentNotation.Binary, reverseBits: false);
        var reversed = _codec.Decode("0000110", SegmentDisplayType.SevenSegment, SegmentNotation.Binary, reverseBits: true);
        Assert.AreEqual("1", normal.Text);
        Assert.AreEqual('I', reversed.Glyphs[0].Character);
    }

    // ---- Common-anode / cathode inversion ----

    [TestMethod]
    public void Encode_Inverted_FlipsTheMask()
    {
        var normal = Single(_codec.Encode("8", SegmentDisplayType.SevenSegment, invert: false));
        var inverted = Single(_codec.Encode("8", SegmentDisplayType.SevenSegment, invert: true));
        Assert.AreEqual(SegmentDisplayCodec.FullMask(SegmentDisplayType.SevenSegment), normal.Mask);
        Assert.AreEqual(0, inverted.Mask); // 8 lights everything, so its inverse lights nothing.
    }

    [TestMethod]
    public void Decode_Inverted_RecoversCharacterFromInvertedMask()
    {
        // '1' (b,c = 6); its inverted 7-bit mask is 0b1111001 = 121.
        var decoded = _codec.Decode("121", SegmentDisplayType.SevenSegment, SegmentNotation.Decimal, invert: true);
        Assert.AreEqual("1", decoded.Text);
    }

    [TestMethod]
    public void EncodeThenDecode_BothInverted_RoundTrips()
    {
        var encoded = _codec.Encode("5", SegmentDisplayType.SevenSegment, invert: true);
        var codes = string.Join(", ", encoded.Select(g => g.Decimal));
        var decoded = _codec.Decode(codes, SegmentDisplayType.SevenSegment, SegmentNotation.Decimal, invert: true);
        Assert.AreEqual("5", decoded.Text);
    }

    // ---- Closest match ----

    [TestMethod]
    public void DecodeMask_OffByOne_ReturnsNearestCharacterFlaggedInexact()
    {
        // '8' is the full mask; drop segment g -> that is exactly '0', so use a mask that is no exact entry:
        // full mask minus 'a' (bit 0) = b c d e f g, which is not a standard glyph; nearest is '8' (distance 1).
        var mask = SegmentDisplayCodec.FullMask(SegmentDisplayType.SevenSegment) & ~0b0000001;
        var glyph = _codec.DecodeMask(mask, SegmentDisplayType.SevenSegment);
        Assert.IsFalse(glyph.IsExact);
        Assert.AreEqual('8', glyph.Character);
        Assert.AreEqual(1, glyph.Distance);
    }

    [TestMethod]
    public void ClosestMatches_RanksByHammingDistance()
    {
        var mask = SegmentDisplayCodec.FullMask(SegmentDisplayType.SevenSegment) & ~0b0000001; // off-by-one from 8
        var matches = _codec.ClosestMatches(mask, SegmentDisplayType.SevenSegment, 3);
        Assert.AreEqual('8', matches[0].Character);
        Assert.AreEqual(1, matches[0].Distance);
        Assert.IsTrue(matches[1].Distance >= matches[0].Distance, "results are ordered by distance");
    }

    // ---- Validation / error paths ----

    [TestMethod]
    public void Decode_UnknownLabel_YieldsUnknownNotDrop()
    {
        var decoded = _codec.Decode("z", SegmentDisplayType.SevenSegment, SegmentNotation.Labels);
        Assert.AreEqual(SegmentDisplayCodec.Unknown.ToString(), decoded.Text);
    }

    [TestMethod]
    public void Decode_OutOfRangeDecimal_YieldsUnknown()
    {
        // Max 7-seg mask is 127; 9999 is out of range.
        var decoded = _codec.Decode("9999", SegmentDisplayType.SevenSegment, SegmentNotation.Decimal);
        Assert.AreEqual(SegmentDisplayCodec.Unknown.ToString(), decoded.Text);
    }

    [TestMethod]
    public void TryParseMask_NegativeDecimal_Fails()
        => Assert.IsFalse(_codec.TryParseMask("-5", SegmentDisplayType.SevenSegment, SegmentNotation.Decimal, reverseBits: false, out _));

    [TestMethod]
    public void TryParseMask_TooWideBinary_Fails()
        => Assert.IsFalse(_codec.TryParseMask("11111111", SegmentDisplayType.SevenSegment, SegmentNotation.Binary, reverseBits: false, out _));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Decode_EmptyOrWhitespace_IsEmpty(string? codes)
        => Assert.AreEqual(string.Empty, _codec.Decode(codes, SegmentDisplayType.SevenSegment).Text);

    // ---- Reference chart ----

    [TestMethod]
    public void GetChart_SevenSegment_StartsWithDigitsInOrder()
    {
        var chart = _codec.GetChart(SegmentDisplayType.SevenSegment);
        Assert.IsTrue(chart.Count >= 10);
        for (var d = 0; d < 10; d++)
        {
            Assert.AreEqual((char)('0' + d), chart[d].Character, $"digit {d} should sort first");
        }
    }

    [TestMethod]
    public void GetChart_EntriesAreExact()
        => Assert.IsTrue(_codec.GetChart(SegmentDisplayType.FourteenSegment).All(g => g.IsExact));

    // ---- SegmentStates rendering model ----

    [TestMethod]
    public void SegmentStates_One_MarksOnlyBandCLit()
    {
        var mask = Single(_codec.Encode("1", SegmentDisplayType.SevenSegment)).Mask;
        var states = SegmentDisplayCodec.SegmentStates(mask, SegmentDisplayType.SevenSegment);
        Assert.AreEqual(7, states.Count);
        Assert.IsTrue(states.Single(s => s.Label == "b").Lit);
        Assert.IsTrue(states.Single(s => s.Label == "c").Lit);
        Assert.IsFalse(states.Single(s => s.Label == "a").Lit);
    }

    private static SegmentGlyph Single(IReadOnlyList<SegmentGlyph> glyphs)
    {
        Assert.AreEqual(1, glyphs.Count);
        return glyphs[0];
    }
}
