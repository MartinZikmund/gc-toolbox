using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

/// <summary>
/// British OSGB36 National Grid, verified against the Ordnance Survey "A guide to coordinate systems
/// in Great Britain" worked example (Caister water tower) and Chris Veness's geodesy reference.
/// OSGB36 lat/lon 52.6575703 N, 1.7179215 E projects to easting 651409, northing 313177 -> grid
/// reference "TG 51409 13177". The same point in WGS84 is ~52.65798 N, 1.71605 E, which through the
/// Helmert 7-parameter transform reaches the same grid reference. Helmert accuracy (a few metres) is
/// expected and matches geocachingtoolbox.com.
/// Sources: OS "A guide to coordinate systems in Great Britain" (worked example, Annexe C);
/// github.com/chrisveness/geodesy osgridref.js / latlon-ellipsoidal-datum.js.
/// </summary>
[TestClass]
public class BritishGridTests
{
    // ---- FromLatLon: OS worked example (Caister) ----

    [TestMethod]
    public void FromLatLon_CaisterWgs84_ProducesKnownGridReference()
    {
        // WGS84 52.65798 N, 1.71605 E -> TG 51409 13177 (via Helmert WGS84->OSGB36).
        var grid = BritishGrid.FromLatLon(new GeoCoordinate(52.65798, 1.71605));
        Assert.AreEqual("TG 51409 13177", grid);
    }

    [TestMethod]
    public void FromLatLon_CaisterEastingNorthing_MatchesOsWorkedExample()
    {
        // The OS easting/northing for the worked example are 651409, 313177 (Helmert ~a few m).
        var en = BritishGrid.ToEastingNorthing(new GeoCoordinate(52.65798, 1.71605));
        Assert.AreEqual(651409.0, en.Easting, 3.0, "easting");
        Assert.AreEqual(313177.0, en.Northing, 3.0, "northing");
    }

    [TestMethod]
    public void FromLatLon_CentralLondon_StartsWithTQ()
    {
        // Charing Cross ~51.5074 N, -0.1278 E sits in the TQ 100km square.
        var grid = BritishGrid.FromLatLon(new GeoCoordinate(51.5074, -0.1278));
        StringAssert.StartsWith(grid, "TQ");
    }

    // ---- Parsing ----

    [DataTestMethod]
    [DataRow("TG 51409 13177")]
    [DataRow("TG5140913177")]
    [DataRow("tg 51409 13177")]
    [DataRow("SU 38700 14800")]
    public void TryParse_ValidGridReferences_Parses(string text)
        => Assert.IsTrue(BritishGrid.TryParse(text, out _), text);

    [TestMethod]
    public void TryParse_Caister_RecoversApproxLatLon()
    {
        Assert.IsTrue(BritishGrid.TryParse("TG 51409 13177", out var c));
        // Round-trips back to WGS84 within a few metres (~1e-4 deg).
        Assert.AreEqual(52.65798, c.Latitude, 1e-3, "lat");
        Assert.AreEqual(1.71605, c.Longitude, 1e-3, "lon");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("hello")]
    [DataRow("TG 5140 13177")]   // unequal digit groups
    [DataRow("II 12345 67890")]  // 'I' is not a valid grid letter
    [DataRow("49.20575 16.576117")]
    public void TryParse_Invalid_ReturnsFalse(string text)
        => Assert.IsFalse(BritishGrid.TryParse(text, out _));

    // ---- Round trip ----

    [DataTestMethod]
    [DataRow(52.65798, 1.71605)]   // Caister
    [DataRow(51.5074, -0.1278)]    // London
    [DataRow(55.9533, -3.1883)]    // Edinburgh
    [DataRow(51.4778, -0.0015)]    // Greenwich
    public void RoundTrip_FromThenTryParse_RecoversWithinMetres(double lat, double lon)
    {
        var grid = BritishGrid.FromLatLon(new GeoCoordinate(lat, lon));
        Assert.IsTrue(BritishGrid.TryParse(grid, out var c), grid);
        // 1m grid truncation + Helmert -> within ~a few metres (~1e-4 deg).
        Assert.AreEqual(lat, c.Latitude, 1e-4, $"lat {grid}");
        Assert.AreEqual(lon, c.Longitude, 1e-4, $"lon {grid}");
    }

    // ---- Formatter wiring ----

    [TestMethod]
    public void Format_BritishGrid_RendersGridReference()
    {
        var text = CoordinateFormatter.Format(new GeoCoordinate(52.65798, 1.71605), CoordinateFormat.BritishGrid);
        Assert.AreEqual("TG 51409 13177", text);
    }
}
