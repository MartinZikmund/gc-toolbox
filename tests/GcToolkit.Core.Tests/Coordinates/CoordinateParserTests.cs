using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class CoordinateParserTests
{
    private const double Tol = 1e-5;

    // ---- Decimal Degrees ----

    [DataTestMethod]
    [DataRow("49.20575 16.576117", 49.20575, 16.576117)]
    [DataRow("N49.20575 E16.576117", 49.20575, 16.576117)]
    [DataRow("-33.8568 151.2153", -33.8568, 151.2153)]
    [DataRow("N 49.20575° E 16.576117°", 49.20575, 16.576117)]
    [DataRow("S33.8568 E151.2153", -33.8568, 151.2153)]
    [DataRow("N12.345 E6.789", 12.345, 6.789)]
    [DataRow("49.20575, 16.576117", 49.20575, 16.576117)] // comma separated
    public void TryParse_DecimalDegrees_DetectsAndParses(string text, double lat, double lon)
    {
        Assert.IsTrue(CoordinateParser.TryParse(text, out var c, out var format), text);
        Assert.AreEqual(CoordinateFormat.DecimalDegrees, format);
        Assert.AreEqual(lat, c.Latitude, Tol);
        Assert.AreEqual(lon, c.Longitude, Tol);
    }

    // ---- Degrees Decimal Minutes ----

    [DataTestMethod]
    [DataRow("N 49 12.345 E 016 34.567", 49.20575, 16.576117)]
    [DataRow("N49°12.345 E16°34.567", 49.20575, 16.576117)]
    [DataRow("-12 34.567 12 56.789", -12.576117, 12.946483)]
    [DataRow("S 12 34.567 W 012 56.789", -12.576117, -12.946483)]
    public void TryParse_DegreesDecimalMinutes_DetectsAndParses(string text, double lat, double lon)
    {
        Assert.IsTrue(CoordinateParser.TryParse(text, out var c, out var format), text);
        Assert.AreEqual(CoordinateFormat.DegreesDecimalMinutes, format);
        Assert.AreEqual(lat, c.Latitude, Tol);
        Assert.AreEqual(lon, c.Longitude, Tol);
    }

    // ---- Degrees Minutes Seconds ----

    [DataTestMethod]
    [DataRow("N 49 12 20.7 E 016 34 34.02", 49.20575, 16.576117)]
    [DataRow("N 49° 12' 20.70\" E 016° 34' 34.02\"", 49.20575, 16.576117)]
    [DataRow("S12 34 12.567 W12 56 12.789", -12.570158, -12.936886)]
    public void TryParse_DegreesMinutesSeconds_DetectsAndParses(string text, double lat, double lon)
    {
        Assert.IsTrue(CoordinateParser.TryParse(text, out var c, out var format), text);
        Assert.AreEqual(CoordinateFormat.DegreesMinutesSeconds, format);
        Assert.AreEqual(lat, c.Latitude, Tol);
        Assert.AreEqual(lon, c.Longitude, Tol);
    }

    // ---- UTM ----

    [DataTestMethod]
    [DataRow("33U 705083 5644673")]
    [DataRow("33 U 705083 5644673")]
    public void TryParse_Utm_DetectsAsUtm(string text)
    {
        Assert.IsTrue(CoordinateParser.TryParse(text, out var c, out var format), text);
        Assert.AreEqual(CoordinateFormat.Utm, format);
        // Round-trips: this UTM is ~50.917N 17.918E
        Assert.AreEqual(50.91721, c.Latitude, 1e-3);
        Assert.AreEqual(17.91774, c.Longitude, 1e-3);
    }

    // ---- MGRS ----

    [DataTestMethod]
    [DataRow("33UVR0508344673")]
    [DataRow("33U VR 05083 44673")]
    public void TryParse_Mgrs_DetectsAsMgrs(string text)
    {
        Assert.IsTrue(CoordinateParser.TryParse(text, out _, out var format), text);
        Assert.AreEqual(CoordinateFormat.Mgrs, format);
    }

    // ---- Explicit-format overload ----

    [TestMethod]
    public void TryParse_ExplicitDecimalDegrees_Parses()
    {
        Assert.IsTrue(CoordinateParser.TryParse("49.20575 16.576117", CoordinateFormat.DecimalDegrees, out var c));
        Assert.AreEqual(49.20575, c.Latitude, Tol);
    }

    [TestMethod]
    public void TryParse_ExplicitFormatMismatch_ReturnsFalse()
        => Assert.IsFalse(CoordinateParser.TryParse("not a coordinate", CoordinateFormat.DecimalDegrees, out _));

    // ---- Failure cases ----

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("hello world")]
    [DataRow("91.0 0.0")]      // latitude out of range
    [DataRow("0.0 181.0")]     // longitude out of range
    [DataRow("49.20575")]      // only one number
    public void TryParse_Unparseable_ReturnsFalseNotThrows(string text)
        => Assert.IsFalse(CoordinateParser.TryParse(text, out _, out _));

    [TestMethod]
    public void TryParse_Null_ReturnsFalse()
        => Assert.IsFalse(CoordinateParser.TryParse(null!, out _, out _));

    // ---- Round-trip: parse(format(x)) ~= x for each format ----

    [DataTestMethod]
    [DataRow(CoordinateFormat.DecimalDegrees)]
    [DataRow(CoordinateFormat.DegreesDecimalMinutes)]
    [DataRow(CoordinateFormat.DegreesMinutesSeconds)]
    [DataRow(CoordinateFormat.Utm)]
    [DataRow(CoordinateFormat.Mgrs)]
    public void ParseFormat_RoundTrips(CoordinateFormat format)
    {
        var original = new GeoCoordinate(49.205750, 16.576117);
        var text = CoordinateFormatter.Format(original, format);
        Assert.IsTrue(CoordinateParser.TryParse(text, format, out var parsed), text);
        Assert.AreEqual(original.Latitude, parsed.Latitude, 1e-3, $"lat for '{text}'");
        Assert.AreEqual(original.Longitude, parsed.Longitude, 1e-3, $"lon for '{text}'");
    }

    [DataTestMethod]
    [DataRow(-33.8568, 151.2153)]
    [DataRow(-12.570158, -12.936886)]
    [DataRow(38.8894477, -77.0361063)]
    public void ParseFormat_RoundTripsAcrossHemispheres(double lat, double lon)
    {
        var original = new GeoCoordinate(lat, lon);
        foreach (var format in new[]
        {
            CoordinateFormat.DecimalDegrees,
            CoordinateFormat.DegreesDecimalMinutes,
            CoordinateFormat.DegreesMinutesSeconds,
        })
        {
            var text = CoordinateFormatter.Format(original, format);
            Assert.IsTrue(CoordinateParser.TryParse(text, format, out var parsed), $"{format}: {text}");
            Assert.AreEqual(original.Latitude, parsed.Latitude, 1e-4, $"{format} lat '{text}'");
            Assert.AreEqual(original.Longitude, parsed.Longitude, 1e-4, $"{format} lon '{text}'");
        }
    }
}
