using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class ResistorCodeTests
{
    private readonly ResistorCode _codec = new();

    // ---- Decode: 4-band ----

    [TestMethod]
    public void Decode_FourBand_BrownBlackRedGold_Is1000OhmsAtFivePercent()
    {
        var result = _codec.Decode([ResistorColor.Brown, ResistorColor.Black, ResistorColor.Red, ResistorColor.Gold]);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1000m, result.Value!.Resistance);
        Assert.AreEqual(5m, result.Value!.TolerancePercent);
        Assert.IsNull(result.Value!.TemperatureCoefficient);
    }

    [TestMethod]
    public void Decode_FourBand_YellowVioletBrownNone_DigitsAndMultiplierGive470Ohms()
    {
        // yellow,violet = 47; brown multiplier = x10 => 470 ohm; none tolerance = +-20%.
        var result = _codec.Decode([ResistorColor.Yellow, ResistorColor.Violet, ResistorColor.Brown, ResistorColor.None]);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(470m, result.Value!.Resistance);
        Assert.AreEqual(20m, result.Value!.TolerancePercent);
    }

    [TestMethod]
    public void Decode_FourBand_GoldMultiplier_AppliesTenthScale()
    {
        // brown,black = 10; gold multiplier = x0.1 => 1 ohm; gold tolerance = +-5%.
        var result = _codec.Decode([ResistorColor.Brown, ResistorColor.Black, ResistorColor.Gold, ResistorColor.Gold]);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1m, result.Value!.Resistance);
        Assert.AreEqual(5m, result.Value!.TolerancePercent);
    }

    // ---- Decode: 5-band ----

    [TestMethod]
    public void Decode_FiveBand_GreenBlueBlackBrownBrown_Is5600OhmsAtOnePercent()
    {
        var result = _codec.Decode([
            ResistorColor.Green, ResistorColor.Blue, ResistorColor.Black, ResistorColor.Brown, ResistorColor.Brown
        ]);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(5600m, result.Value!.Resistance);
        Assert.AreEqual(1m, result.Value!.TolerancePercent);
        Assert.IsNull(result.Value!.TemperatureCoefficient);
    }

    // ---- Decode: 6-band ----

    [TestMethod]
    public void Decode_SixBand_AddsTemperatureCoefficient()
    {
        // digits 1,0,0 = 100; brown multiplier x10 => 1000 ohm; brown tolerance +-1%; red tempco = 50 ppm/K.
        var result = _codec.Decode([
            ResistorColor.Brown, ResistorColor.Black, ResistorColor.Black,
            ResistorColor.Brown, ResistorColor.Brown, ResistorColor.Red
        ]);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1000m, result.Value!.Resistance);
        Assert.AreEqual(1m, result.Value!.TolerancePercent);
        Assert.AreEqual(50, result.Value!.TemperatureCoefficient);
    }

    // ---- Decode: validation / errors ----

    [TestMethod]
    public void Decode_WrongBandCount_Fails()
    {
        var result = _codec.Decode([ResistorColor.Brown, ResistorColor.Black]);
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.Value);
    }

    [TestMethod]
    public void Decode_DigitBandWithGold_IsInvalid()
    {
        // Gold is not a valid significant-digit colour.
        var result = _codec.Decode([ResistorColor.Gold, ResistorColor.Black, ResistorColor.Red, ResistorColor.Gold]);
        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public void Decode_ToleranceBandWithOrange_IsInvalid()
    {
        // Orange is not a valid tolerance colour.
        var result = _codec.Decode([ResistorColor.Brown, ResistorColor.Black, ResistorColor.Red, ResistorColor.Orange]);
        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public void Decode_TempCoBandWithGold_IsInvalid()
    {
        var result = _codec.Decode([
            ResistorColor.Brown, ResistorColor.Black, ResistorColor.Black,
            ResistorColor.Brown, ResistorColor.Brown, ResistorColor.Gold
        ]);
        Assert.IsFalse(result.Success);
    }

    // ---- Decode: SI formatting ----

    [TestMethod]
    [DataRow(0.47, "0.47 Ω")]
    [DataRow(4.7, "4.7 Ω")]
    [DataRow(470.0, "470 Ω")]
    [DataRow(4700.0, "4.7 kΩ")]
    [DataRow(1000.0, "1 kΩ")]
    [DataRow(56000.0, "56 kΩ")]
    [DataRow(1000000.0, "1 MΩ")]
    [DataRow(2200000.0, "2.2 MΩ")]
    [DataRow(1000000000.0, "1 GΩ")]
    public void FormatResistance_UsesSiPrefix(double ohms, string expected)
        => Assert.AreEqual(expected, ResistorCode.FormatResistance((decimal)ohms));

    // ---- Encode ----

    [TestMethod]
    public void Encode_4700Ohms_FivePercent_FourBand_IsYellowVioletRedGold()
    {
        var result = _codec.Encode(4700m, ResistorBandCount.Four, tolerancePercent: 5m);

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(
            new[] { ResistorColor.Yellow, ResistorColor.Violet, ResistorColor.Red, ResistorColor.Gold },
            result.Bands!.ToArray());
    }

    [TestMethod]
    public void Encode_1000Ohms_FivePercent_FourBand_IsBrownBlackRedGold()
    {
        var result = _codec.Encode(1000m, ResistorBandCount.Four, tolerancePercent: 5m);

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(
            new[] { ResistorColor.Brown, ResistorColor.Black, ResistorColor.Red, ResistorColor.Gold },
            result.Bands!.ToArray());
    }

    [TestMethod]
    public void Encode_5600Ohms_OnePercent_FiveBand_IsGreenBlueBlackBrownBrown()
    {
        var result = _codec.Encode(5600m, ResistorBandCount.Five, tolerancePercent: 1m);

        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(
            new[] { ResistorColor.Green, ResistorColor.Blue, ResistorColor.Black, ResistorColor.Brown, ResistorColor.Brown },
            result.Bands!.ToArray());
    }

    [TestMethod]
    public void Encode_SixBand_IncludesTempCoBand()
    {
        var result = _codec.Encode(1000m, ResistorBandCount.Six, tolerancePercent: 1m, temperatureCoefficient: 50);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(6, result.Bands!.Count);
        Assert.AreEqual(ResistorColor.Red, result.Bands![5]); // red = 50 ppm/K
    }

    [TestMethod]
    public void Encode_RoundTripsWithDecode()
    {
        var encoded = _codec.Encode(2200000m, ResistorBandCount.Five, tolerancePercent: 2m);
        Assert.IsTrue(encoded.Success);

        var decoded = _codec.Decode(encoded.Bands!);
        Assert.IsTrue(decoded.Success);
        Assert.AreEqual(2200000m, decoded.Value!.Resistance);
        Assert.AreEqual(2m, decoded.Value!.TolerancePercent);
    }

    [TestMethod]
    public void Encode_NegativeOrZero_Fails()
    {
        Assert.IsFalse(_codec.Encode(0m, ResistorBandCount.Four, 5m).Success);
        Assert.IsFalse(_codec.Encode(-100m, ResistorBandCount.Four, 5m).Success);
    }

    [TestMethod]
    public void Encode_UnrepresentableTolerance_Fails()
    {
        // 3% is not a standard tolerance colour.
        Assert.IsFalse(_codec.Encode(1000m, ResistorBandCount.Four, tolerancePercent: 3m).Success);
    }

    [TestMethod]
    public void Encode_ValueTooLargeForMultiplier_Fails()
    {
        // Beyond white (x10^9) for the available significant digits.
        Assert.IsFalse(_codec.Encode(999_000_000_000m, ResistorBandCount.Four, tolerancePercent: 5m).Success);
    }

    [TestMethod]
    public void Encode_ValueNeedingMoreDigits_RoundsAndFlagsApproximate()
    {
        // 1234 needs 4 significant digits; a 5-band resistor carries 3 -> nearest is 1230.
        var result = _codec.Encode(1234m, ResistorBandCount.Five, tolerancePercent: 1m);

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.IsApproximate);
        Assert.AreEqual(1230m, _codec.Decode(result.Bands!).Value!.Resistance);
    }

    [TestMethod]
    public void Encode_ExactValue_IsNotFlaggedApproximate()
    {
        var result = _codec.Encode(4700m, ResistorBandCount.Four, tolerancePercent: 5m);

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.IsApproximate);
    }

    // ---- Digit string (geocaching puzzles want the digits, not the ohm value) ----

    [TestMethod]
    public void DigitString_FourBand_ReturnsTheTwoSignificantDigits()
        => Assert.AreEqual("10", ResistorCode.DigitString([ResistorColor.Brown, ResistorColor.Black, ResistorColor.Red, ResistorColor.Gold]));

    [TestMethod]
    public void DigitString_FiveBand_ReturnsTheThreeSignificantDigits()
        => Assert.AreEqual(
            "560",
            ResistorCode.DigitString([ResistorColor.Green, ResistorColor.Blue, ResistorColor.Black, ResistorColor.Brown, ResistorColor.Brown]));

    [TestMethod]
    public void DigitString_InvalidBands_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, ResistorCode.DigitString([ResistorColor.Gold, ResistorColor.Black, ResistorColor.Red, ResistorColor.Gold]));

    // ---- Colour metadata tables (parity verification) ----

    [TestMethod]
    [DataRow(ResistorColor.Black, 0)]
    [DataRow(ResistorColor.Brown, 1)]
    [DataRow(ResistorColor.Red, 2)]
    [DataRow(ResistorColor.Orange, 3)]
    [DataRow(ResistorColor.Yellow, 4)]
    [DataRow(ResistorColor.Green, 5)]
    [DataRow(ResistorColor.Blue, 6)]
    [DataRow(ResistorColor.Violet, 7)]
    [DataRow(ResistorColor.Grey, 8)]
    [DataRow(ResistorColor.White, 9)]
    public void DigitValue_MatchesStandardTable(ResistorColor color, int digit)
        => Assert.AreEqual(digit, ResistorCode.DigitValue(color));

    [TestMethod]
    [DataRow(ResistorColor.Brown, 1.0)]
    [DataRow(ResistorColor.Red, 2.0)]
    [DataRow(ResistorColor.Green, 0.5)]
    [DataRow(ResistorColor.Blue, 0.25)]
    [DataRow(ResistorColor.Violet, 0.1)]
    [DataRow(ResistorColor.Grey, 0.05)]
    [DataRow(ResistorColor.Gold, 5.0)]
    [DataRow(ResistorColor.Silver, 10.0)]
    [DataRow(ResistorColor.None, 20.0)]
    public void ToleranceValue_MatchesStandardTable(ResistorColor color, double tolerance)
        => Assert.AreEqual((decimal)tolerance, ResistorCode.ToleranceValue(color));

    [TestMethod]
    [DataRow(ResistorColor.Black, 250)]
    [DataRow(ResistorColor.Brown, 100)]
    [DataRow(ResistorColor.Red, 50)]
    [DataRow(ResistorColor.Orange, 15)]
    [DataRow(ResistorColor.Yellow, 25)]
    [DataRow(ResistorColor.Green, 20)]
    [DataRow(ResistorColor.Blue, 10)]
    [DataRow(ResistorColor.Violet, 5)]
    [DataRow(ResistorColor.Grey, 1)]
    public void TemperatureCoefficientValue_MatchesStandardTable(ResistorColor color, int ppm)
        => Assert.AreEqual(ppm, ResistorCode.TemperatureCoefficientValue(color));

    [TestMethod]
    [DataRow(ResistorColor.Silver, 0.01)]
    [DataRow(ResistorColor.Gold, 0.1)]
    [DataRow(ResistorColor.Black, 1.0)]
    [DataRow(ResistorColor.Red, 100.0)]
    [DataRow(ResistorColor.Green, 100000.0)]
    [DataRow(ResistorColor.White, 1000000000.0)]
    public void MultiplierValue_MatchesStandardTable(ResistorColor color, double multiplier)
        => Assert.AreEqual((decimal)multiplier, ResistorCode.MultiplierValue(color));
}
