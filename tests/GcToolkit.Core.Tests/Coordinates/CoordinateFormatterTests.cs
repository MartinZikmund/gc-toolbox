using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class CoordinateFormatterTests
{
    // Golden vector: 49.205750, 16.576117
    private static readonly GeoCoordinate Golden = new(49.205750, 16.576117);

    [TestMethod]
    public void Format_DecimalDegrees_UsesHemisphereAndPaddedLongitude()
        => Assert.AreEqual("N 49.205750° E 016.576117°", CoordinateFormatter.Format(Golden, CoordinateFormat.DecimalDegrees));

    [TestMethod]
    public void Format_DegreesDecimalMinutes_MatchesGeocachingConvention()
        => Assert.AreEqual("N 49° 12.345' E 016° 34.567'", CoordinateFormatter.Format(Golden, CoordinateFormat.DegreesDecimalMinutes));

    [TestMethod]
    public void Format_DegreesMinutesSeconds_MatchesGeocachingConvention()
        => Assert.AreEqual("N 49° 12' 20.70\" E 016° 34' 34.02\"", CoordinateFormatter.Format(Golden, CoordinateFormat.DegreesMinutesSeconds));

    [TestMethod]
    public void Format_SouthWest_UsesSAndWHemispheres()
    {
        var c = new GeoCoordinate(-33.8568, -151.2153);
        var ddm = CoordinateFormatter.Format(c, CoordinateFormat.DegreesDecimalMinutes);
        StringAssert.StartsWith(ddm, "S 33°");
        StringAssert.Contains(ddm, "W 151°");
    }

    [TestMethod]
    public void Format_LongitudeDegrees_AlwaysThreeDigitsPadded()
    {
        var c = new GeoCoordinate(5.0, 6.0);
        StringAssert.Contains(CoordinateFormatter.Format(c, CoordinateFormat.DecimalDegrees), "E 006.");
    }

    [TestMethod]
    public void Format_LatitudeDegrees_AlwaysTwoDigitsPadded()
    {
        var c = new GeoCoordinate(5.0, 6.0);
        StringAssert.StartsWith(CoordinateFormatter.Format(c, CoordinateFormat.DecimalDegrees), "N 05.");
    }

    [TestMethod]
    public void Format_Utm_RendersZoneEastingNorthing()
    {
        // lat=0, lon=3 -> 31N 500000 0
        var c = new GeoCoordinate(0.0, 3.0);
        var utm = CoordinateFormatter.Format(c, CoordinateFormat.Utm);
        StringAssert.StartsWith(utm, "31");
        StringAssert.Contains(utm, "500000");
    }

    [TestMethod]
    public void Format_Mgrs_RendersGrid()
    {
        // Washington Monument -> 18S UJ 23408 06479
        var c = new GeoCoordinate(38.8894477, -77.0361063);
        var mgrs = CoordinateFormatter.Format(c, CoordinateFormat.Mgrs);
        StringAssert.StartsWith(mgrs, "18S");
        StringAssert.Contains(mgrs, "UJ");
    }
}
