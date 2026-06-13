using System.Globalization;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class AngleConverterTests
{
    private const double Tolerance = 1e-9;

    private readonly AngleConverter _converter = new();

    private static double Convert(double value, AngleUnit from, AngleUnit to)
        => new AngleConverter().Convert(value, from, to);

    // ---- All units present (parity: 10 site units + 2 beyond-parity) ----

    [TestMethod]
    public void Units_ContainsEverySiteUnit()
    {
        var units = AngleConverter.Units;

        CollectionAssert.Contains(units.ToArray(), AngleUnit.Degrees);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.Radians);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.MilsNato);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.MilsMilliradian);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.MilsSoviet);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.MilsSweden);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.Gradians);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.Turns);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.Points);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.HourAngles);
        // Beyond parity:
        CollectionAssert.Contains(units.ToArray(), AngleUnit.Arcminutes);
        CollectionAssert.Contains(units.ToArray(), AngleUnit.Arcseconds);
    }

    // ---- Anchor: one full turn equals every unit's per-turn count ----

    [DataTestMethod]
    [DataRow(AngleUnit.Degrees, 360.0)]
    [DataRow(AngleUnit.Gradians, 400.0)]
    [DataRow(AngleUnit.MilsNato, 6400.0)]
    [DataRow(AngleUnit.MilsSoviet, 6000.0)]
    [DataRow(AngleUnit.MilsSweden, 6300.0)]
    [DataRow(AngleUnit.Points, 32.0)]
    [DataRow(AngleUnit.HourAngles, 24.0)]
    [DataRow(AngleUnit.Arcminutes, 21600.0)]
    [DataRow(AngleUnit.Arcseconds, 1296000.0)]
    public void Convert_OneTurnToUnit_EqualsUnitsPerTurn(AngleUnit to, double expected)
        => Assert.AreEqual(expected, Convert(1.0, AngleUnit.Turns, to), Tolerance);

    [TestMethod]
    public void Convert_OneTurnToRadians_IsTwoPi()
        => Assert.AreEqual(2 * Math.PI, Convert(1.0, AngleUnit.Turns, AngleUnit.Radians), Tolerance);

    [TestMethod]
    public void Convert_OneTurnToMilliradian_Is2000Pi()
        => Assert.AreEqual(2000 * Math.PI, Convert(1.0, AngleUnit.Turns, AngleUnit.MilsMilliradian), Tolerance);

    // ---- Anchor: 360 degrees expressed in every unit ----

    [TestMethod]
    public void Convert_360DegreesToTurns_IsOne()
        => Assert.AreEqual(1.0, Convert(360, AngleUnit.Degrees, AngleUnit.Turns), Tolerance);

    [TestMethod]
    public void Convert_360DegreesToRadians_IsTwoPi()
        => Assert.AreEqual(2 * Math.PI, Convert(360, AngleUnit.Degrees, AngleUnit.Radians), Tolerance);

    [TestMethod]
    public void Convert_360DegreesToGradians_Is400()
        => Assert.AreEqual(400, Convert(360, AngleUnit.Degrees, AngleUnit.Gradians), Tolerance);

    [TestMethod]
    public void Convert_360DegreesToNatoMils_Is6400()
        => Assert.AreEqual(6400, Convert(360, AngleUnit.Degrees, AngleUnit.MilsNato), Tolerance);

    [TestMethod]
    public void Convert_360DegreesToSovietMils_Is6000()
        => Assert.AreEqual(6000, Convert(360, AngleUnit.Degrees, AngleUnit.MilsSoviet), Tolerance);

    [TestMethod]
    public void Convert_360DegreesToSwedenMils_Is6300()
        => Assert.AreEqual(6300, Convert(360, AngleUnit.Degrees, AngleUnit.MilsSweden), Tolerance);

    [TestMethod]
    public void Convert_360DegreesToPoints_Is32()
        => Assert.AreEqual(32, Convert(360, AngleUnit.Degrees, AngleUnit.Points), Tolerance);

    [TestMethod]
    public void Convert_360DegreesToHourAngles_Is24()
        => Assert.AreEqual(24, Convert(360, AngleUnit.Degrees, AngleUnit.HourAngles), Tolerance);

    // ---- Anchor: 90 degrees ----

    [DataTestMethod]
    [DataRow(AngleUnit.Turns, 0.25)]
    [DataRow(AngleUnit.Gradians, 100.0)]
    [DataRow(AngleUnit.MilsNato, 1600.0)]
    [DataRow(AngleUnit.Points, 8.0)]
    [DataRow(AngleUnit.HourAngles, 6.0)]
    public void Convert_90DegreesToUnit_MatchesQuarterTurn(AngleUnit to, double expected)
        => Assert.AreEqual(expected, Convert(90, AngleUnit.Degrees, to), Tolerance);

    [TestMethod]
    public void Convert_90DegreesToRadians_IsHalfPi()
        => Assert.AreEqual(Math.PI / 2, Convert(90, AngleUnit.Degrees, AngleUnit.Radians), Tolerance);

    // ---- Anchor: 1 compass point = 11.25 degrees ----

    [TestMethod]
    public void Convert_OnePointToDegrees_Is11Point25()
        => Assert.AreEqual(11.25, Convert(1, AngleUnit.Points, AngleUnit.Degrees), Tolerance);

    [TestMethod]
    public void Convert_11Point25DegreesToPoints_IsOne()
        => Assert.AreEqual(1.0, Convert(11.25, AngleUnit.Degrees, AngleUnit.Points), Tolerance);

    // ---- Identity ----

    [DataTestMethod]
    [DataRow(AngleUnit.Degrees)]
    [DataRow(AngleUnit.Radians)]
    [DataRow(AngleUnit.Turns)]
    [DataRow(AngleUnit.Gradians)]
    public void Convert_SameUnit_ReturnsInput(AngleUnit unit)
        => Assert.AreEqual(123.456, Convert(123.456, unit, unit), Tolerance);

    // ---- Round trips: any unit -> any unit -> back ----

    [TestMethod]
    public void Convert_RoundTripThroughEveryUnitPair_PreservesValue()
    {
        const double original = 137.0;
        foreach (var from in AngleConverter.Units)
        {
            foreach (var to in AngleConverter.Units)
            {
                var forward = Convert(original, from, to);
                var back = Convert(forward, to, from);
                Assert.AreEqual(original, back, 1e-6, $"{from} -> {to} -> {from}");
            }
        }
    }

    // ---- Zero ----

    [TestMethod]
    public void Convert_Zero_IsZeroInEveryUnit()
    {
        foreach (var to in AngleConverter.Units)
        {
            Assert.AreEqual(0.0, Convert(0, AngleUnit.Degrees, to), Tolerance);
        }
    }

    // ---- Negative ----

    [TestMethod]
    public void Convert_NegativeDegrees_NegatesProportionally()
        => Assert.AreEqual(-200, Convert(-180, AngleUnit.Degrees, AngleUnit.Gradians), Tolerance);

    // ---- DMS parse ----

    [TestMethod]
    public void TryParseDegrees_PlainInteger_ReturnsValue()
    {
        Assert.IsTrue(AngleConverter.TryParseDegrees("45", out var degrees));
        Assert.AreEqual(45.0, degrees, Tolerance);
    }

    [TestMethod]
    public void TryParseDegrees_DotDecimal_ReturnsValue()
    {
        Assert.IsTrue(AngleConverter.TryParseDegrees("45.5", out var degrees));
        Assert.AreEqual(45.5, degrees, Tolerance);
    }

    [TestMethod]
    public void TryParseDegrees_CommaDecimal_ReturnsValue()
    {
        Assert.IsTrue(AngleConverter.TryParseDegrees("45,5", out var degrees));
        Assert.AreEqual(45.5, degrees, Tolerance);
    }

    [TestMethod]
    public void TryParseDegrees_FullDms_ReturnsDecimalDegrees()
    {
        Assert.IsTrue(AngleConverter.TryParseDegrees("45°30'15\"", out var degrees));
        Assert.AreEqual(45 + 30.0 / 60 + 15.0 / 3600, degrees, 1e-6);
    }

    [TestMethod]
    public void TryParseDegrees_DmsWithSpaces_IsLenient()
    {
        Assert.IsTrue(AngleConverter.TryParseDegrees("  45 ° 30 ' 15 \"  ", out var degrees));
        Assert.AreEqual(45 + 30.0 / 60 + 15.0 / 3600, degrees, 1e-6);
    }

    [TestMethod]
    public void TryParseDegrees_DegreesAndMinutesOnly_Works()
    {
        Assert.IsTrue(AngleConverter.TryParseDegrees("45°30'", out var degrees));
        Assert.AreEqual(45.5, degrees, 1e-6);
    }

    [TestMethod]
    public void TryParseDegrees_NegativeDms_IsNegative()
    {
        Assert.IsTrue(AngleConverter.TryParseDegrees("-45°30'", out var degrees));
        Assert.AreEqual(-45.5, degrees, 1e-6);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("abc")]
    [DataRow("45x30")]
    public void TryParseDegrees_InvalidInput_ReturnsFalse(string text)
        => Assert.IsFalse(AngleConverter.TryParseDegrees(text, out _));

    [TestMethod]
    public void TryParseDegrees_Null_ReturnsFalse()
        => Assert.IsFalse(AngleConverter.TryParseDegrees(null, out _));

    // ---- DMS format ----

    [TestMethod]
    public void FormatDms_DecimalDegrees_ProducesDmsString()
    {
        var dms = AngleConverter.FormatDms(45 + 30.0 / 60 + 15.0 / 3600);
        Assert.AreEqual("45°30'15\"", dms);
    }

    [TestMethod]
    public void FormatDms_NegativeValue_KeepsSign()
    {
        var dms = AngleConverter.FormatDms(-(45 + 30.0 / 60 + 15.0 / 3600));
        Assert.AreEqual("-45°30'15\"", dms);
    }

    [TestMethod]
    public void FormatDms_WholeDegrees_ProducesZeroMinutesSeconds()
        => Assert.AreEqual("45°0'0\"", AngleConverter.FormatDms(45));

    [TestMethod]
    public void FormatDms_RoundTripWithParse_PreservesValue()
    {
        const double original = 12 + 34.0 / 60 + 56.0 / 3600;
        var dms = AngleConverter.FormatDms(original);
        Assert.IsTrue(AngleConverter.TryParseDegrees(dms, out var parsed));
        Assert.AreEqual(original, parsed, 1e-6);
    }
}
