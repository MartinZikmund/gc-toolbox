using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class AntipodeTests
{
    [TestMethod]
    public void Of_KnownVector_MatchesGeocachingToolbox()
    {
        // geocachingtoolbox worked example: N50 25.123 E005 45.123 -> S50 25.123 W174 14.877.
        // E005 45.123' = 5.75205°; antipode lon = 5.75205 - 180 = -174.24795° = W174° 14.877'.
        Assert.IsTrue(CoordinateParser.TryParse("N50 25.123 E005 45.123", out var source, out _));

        var antipode = Antipode.Of(source);

        Assert.AreEqual(-50.41871666666667, antipode.Latitude, 1e-9, "lat");
        Assert.AreEqual(-174.24795, antipode.Longitude, 1e-9, "lon");
        Assert.AreEqual(
            "S 50° 25.123' W 174° 14.877'",
            CoordinateFormatter.Format(antipode, CoordinateFormat.DegreesDecimalMinutes));
    }

    [TestMethod]
    public void Of_FlipsLatitudeHemisphere_MagnitudeUnchanged()
    {
        var antipode = Antipode.Of(new GeoCoordinate(50.0, 5.0));
        Assert.AreEqual(-50.0, antipode.Latitude);
    }

    [DataTestMethod]
    [DataRow(5.0, -175.0)]    // east -> west
    [DataRow(-5.0, 175.0)]    // west -> east
    [DataRow(120.0, -60.0)]
    [DataRow(-120.0, 60.0)]
    public void Of_ShiftsLongitudeBy180_WrappingInRange(double lon, double expected)
    {
        var antipode = Antipode.Of(new GeoCoordinate(0.0, lon));
        Assert.AreEqual(expected, antipode.Longitude, 1e-9);
    }

    [TestMethod]
    public void Of_PrimeMeridian_BecomesAntimeridian()
    {
        var antipode = Antipode.Of(new GeoCoordinate(10.0, 0.0));
        Assert.AreEqual(180.0, antipode.Longitude, 1e-9);
    }

    [TestMethod]
    public void Of_Antimeridian_BecomesPrimeMeridian()
    {
        var antipode = Antipode.Of(new GeoCoordinate(10.0, 180.0));
        Assert.AreEqual(0.0, antipode.Longitude, 1e-9);
    }

    [TestMethod]
    public void Of_EquatorPrimeMeridian_MapsToEquatorAntimeridian()
    {
        var antipode = Antipode.Of(new GeoCoordinate(0.0, 0.0));
        Assert.AreEqual(0.0, antipode.Latitude, 1e-9);
        Assert.AreEqual(180.0, antipode.Longitude, 1e-9);
    }

    [DataTestMethod]
    [DataRow(90.0, 0.0)]    // north pole
    [DataRow(-90.0, 0.0)]   // south pole
    public void Of_Pole_FlipsToOppositePole(double lat, double lon)
    {
        var antipode = Antipode.Of(new GeoCoordinate(lat, lon));
        Assert.AreEqual(-lat, antipode.Latitude, 1e-9);
    }

    [TestMethod]
    public void Of_IsItsOwnInverse()
    {
        var source = new GeoCoordinate(49.205750, 16.576117);
        var round = Antipode.Of(Antipode.Of(source));
        Assert.AreEqual(source.Latitude, round.Latitude, 1e-9);
        Assert.AreEqual(source.Longitude, round.Longitude, 1e-9);
    }

    [TestMethod]
    public void Of_ResultIsAlwaysValid()
    {
        // Sweep the extremes; the antipode must never escape the geographic bounds.
        foreach (var lat in new[] { -90.0, -45.0, 0.0, 45.0, 90.0 })
        {
            foreach (var lon in new[] { -180.0, -90.0, 0.0, 90.0, 180.0 })
            {
                var antipode = Antipode.Of(new GeoCoordinate(lat, lon));
                Assert.IsTrue(antipode.IsValid, $"({lat},{lon}) -> ({antipode.Latitude},{antipode.Longitude})");
            }
        }
    }

    [TestMethod]
    public void Of_DistanceToSource_IsNearHalfCircumference()
    {
        // The antipode is, by definition, the farthest point: ~20,000 km away.
        var source = new GeoCoordinate(49.205750, 16.576117);
        var distance = Geodesy.DistanceMeters(source, Antipode.Of(source));
        Assert.IsTrue(distance is > 19_900_000 and < 20_100_000, $"distance was {distance}");
    }
}
