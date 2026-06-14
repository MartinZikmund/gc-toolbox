using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class UtmTests
{
    // ---- FromLatLon: worked vectors ----

    [TestMethod]
    public void FromLatLon_Equator3East_Zone31FalseEastingZeroNorthing()
    {
        var u = Utm.FromLatLon(new GeoCoordinate(0.0, 3.0));
        Assert.AreEqual(31, u.ZoneNumber);
        Assert.AreEqual('N', u.ZoneBand);
        Assert.AreEqual(500000.0, u.Easting, 0.5);
        Assert.AreEqual(0.0, u.Northing, 0.5);
        Assert.IsTrue(u.IsNorthern);
    }

    [TestMethod]
    public void FromLatLon_WashingtonMonument_MatchesKnownUtm()
    {
        // 38.889448, -77.036106 -> 18S 323408 4306479 (Wikipedia MGRS 18S UJ 23408 06479)
        var u = Utm.FromLatLon(new GeoCoordinate(38.88944771819054, -77.03610626563939));
        Assert.AreEqual(18, u.ZoneNumber);
        Assert.AreEqual('S', u.ZoneBand);
        Assert.AreEqual(323408, u.Easting, 1.0);
        Assert.AreEqual(4306479, u.Northing, 1.0);
    }

    [TestMethod]
    public void FromLatLon_Golden_MatchesReferenceUtm()
    {
        // 49.205750, 16.576117 -> 33U 614803 5451524 (reference: utm python package)
        var u = Utm.FromLatLon(new GeoCoordinate(49.205750, 16.576117));
        Assert.AreEqual(33, u.ZoneNumber);
        Assert.AreEqual('U', u.ZoneBand);
        Assert.AreEqual(614803.478, u.Easting, 1.0);
        Assert.AreEqual(5451524.006, u.Northing, 1.0);
    }

    [TestMethod]
    public void FromLatLon_Southern_Northing10MillionOffsetApplied()
    {
        // Flinders Peak -37.951033, 144.424868 -> 55H 273741 5796490
        var u = Utm.FromLatLon(new GeoCoordinate(-37.951033, 144.424868));
        Assert.AreEqual(55, u.ZoneNumber);
        Assert.AreEqual('H', u.ZoneBand);
        Assert.IsFalse(u.IsNorthern);
        Assert.AreEqual(273741.0, u.Easting, 2.0);
        Assert.AreEqual(5796490.0, u.Northing, 2.0);
    }

    // ---- Norway / Svalbard exceptions ----

    [TestMethod]
    public void FromLatLon_SouthwestNorway_UsesWidenedZone32()
    {
        // Around 60N, 4.5E falls in the widened zone 32 (zone 31 shrinks).
        var u = Utm.FromLatLon(new GeoCoordinate(60.0, 4.5));
        Assert.AreEqual(32, u.ZoneNumber);
    }

    [TestMethod]
    public void FromLatLon_Svalbard_UsesWidenedOddZones()
    {
        // Svalbard band X: 78N 20E -> zone 33 (32,34,36 unused).
        var u = Utm.FromLatLon(new GeoCoordinate(78.0, 20.0));
        Assert.AreEqual(33, u.ZoneNumber);
        Assert.AreEqual('X', u.ZoneBand);
    }

    // ---- Round trip ----

    [DataTestMethod]
    [DataRow(49.205750, 16.576117)]
    [DataRow(0.0, 3.0)]
    [DataRow(-37.951033, 144.424868)]
    [DataRow(38.8894477, -77.0361063)]
    [DataRow(51.477928, -0.001545)] // Greenwich
    [DataRow(-33.8568, 151.2153)]   // Sydney
    public void RoundTrip_FromThenToLatLon_RecoversInput(double lat, double lon)
    {
        var original = new GeoCoordinate(lat, lon);
        var back = Utm.ToLatLon(Utm.FromLatLon(original));
        Assert.AreEqual(lat, back.Latitude, 1e-5, "lat");
        Assert.AreEqual(lon, back.Longitude, 1e-5, "lon");
    }

    // ---- ToString ----

    [TestMethod]
    public void ToString_FormatsZoneBandEastingNorthing()
    {
        var u = new UtmCoordinate(33, 'U', 705083, 5644673);
        Assert.AreEqual("33U 705083 5644673", u.ToString());
    }

    [DataTestMethod]
    [DataRow('N', true)]
    [DataRow('U', true)]
    [DataRow('X', true)]
    [DataRow('M', false)]
    [DataRow('C', false)]
    public void IsNorthern_BasedOnBandLetter(char band, bool expected)
        => Assert.AreEqual(expected, new UtmCoordinate(33, band, 0, 0).IsNorthern);

    // ---- TryParse ----

    [DataTestMethod]
    [DataRow("33U 705083 5644673", 33, 'U', 705083.0, 5644673.0)]
    [DataRow("33 U 705083 5644673", 33, 'U', 705083.0, 5644673.0)]
    [DataRow("18S 323408 4306479", 18, 'S', 323408.0, 4306479.0)]
    public void TryParse_ValidNotations_Parses(string text, int zone, char band, double easting, double northing)
    {
        Assert.IsTrue(Utm.TryParse(text, out var u), text);
        Assert.AreEqual(zone, u.ZoneNumber);
        Assert.AreEqual(char.ToUpperInvariant(band), char.ToUpperInvariant(u.ZoneBand));
        Assert.AreEqual(easting, u.Easting, 0.5);
        Assert.AreEqual(northing, u.Northing, 0.5);
    }

    [TestMethod]
    public void TryParse_RoundTripsThroughLatLon()
    {
        Assert.IsTrue(Utm.TryParse("33U 705083 5644673", out var u));
        var ll = Utm.ToLatLon(u);
        Assert.AreEqual(50.91721, ll.Latitude, 1e-3);
        Assert.AreEqual(17.91774, ll.Longitude, 1e-3);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("hello")]
    [DataRow("99Z 1 1")]            // invalid zone
    [DataRow("33U 705083")]         // missing northing
    [DataRow("49.20575 16.576117")] // not UTM
    public void TryParse_Invalid_ReturnsFalse(string text)
        => Assert.IsFalse(Utm.TryParse(text, out _));
}
