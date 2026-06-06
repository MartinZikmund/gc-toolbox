using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

/// <summary>
/// Dutch RD (Rijksdriehoek) projection, verified against the Schreutelkamp &amp; Strang van Hees
/// approximation (the published reference points): Amersfoort, the system origin, maps to RD
/// (155000, 463000); central Amsterdam RD (121687, 487484) maps to WGS84 (52.37422, 4.89801).
/// Source: F.H. Schreutelkamp &amp; G.L. Strang van Hees, "Benaderingsformules voor de transformatie
/// tussen RD- en WGS84-kaartcoordinaten" (coefficients mirrored by github.com/djvanderlaan/rijksdriehoek).
/// </summary>
[TestClass]
public class DutchRdTests
{
    // ---- FromLatLon: published reference points ----

    [TestMethod]
    public void FromLatLon_Amersfoort_IsTheOrigin()
    {
        // The RD origin: 52.15517440 N, 5.38720621 E -> exactly (155000, 463000).
        var rd = DutchRd.FromLatLon(new GeoCoordinate(52.15517440, 5.38720621));
        Assert.AreEqual(155000.0, rd.Easting, 0.5, "easting");
        Assert.AreEqual(463000.0, rd.Northing, 0.5, "northing");
    }

    [TestMethod]
    public void FromLatLon_Amsterdam_MatchesPublishedRd()
    {
        // Schreutelkamp & Strang van Hees test point: 52.37422 N, 4.89801 E -> RD 121687, 487484.
        var rd = DutchRd.FromLatLon(new GeoCoordinate(52.37422, 4.89801));
        Assert.AreEqual(121687.0, rd.Easting, 1.0, "easting");
        Assert.AreEqual(487484.0, rd.Northing, 1.0, "northing");
    }

    // ---- ToLatLon: published reference points ----

    [TestMethod]
    public void ToLatLon_Origin_IsAmersfoort()
    {
        var ll = DutchRd.ToLatLon(new RdCoordinate(155000.0, 463000.0));
        Assert.AreEqual(52.15517440, ll.Latitude, 1e-6, "lat");
        Assert.AreEqual(5.38720621, ll.Longitude, 1e-6, "lon");
    }

    [TestMethod]
    public void ToLatLon_Amsterdam_MatchesPublishedLatLon()
    {
        var ll = DutchRd.ToLatLon(new RdCoordinate(121687.0, 487484.0));
        Assert.AreEqual(52.37422, ll.Latitude, 1e-4, "lat");
        Assert.AreEqual(4.89801, ll.Longitude, 1e-4, "lon");
    }

    // ---- Formatting ----

    [TestMethod]
    public void ToString_RendersEastingThenNorthingRoundedToTheMetre()
    {
        var rd = new RdCoordinate(155000.0, 463000.0);
        Assert.AreEqual("155000 463000", rd.ToString());
    }

    [TestMethod]
    public void Format_DutchRd_RendersGrid()
    {
        var text = CoordinateFormatter.Format(new GeoCoordinate(52.15517440, 5.38720621), CoordinateFormat.DutchRd);
        Assert.AreEqual("155000 463000", text);
    }

    // ---- Parsing ----

    [DataTestMethod]
    [DataRow("155000 463000", 155000.0, 463000.0)]
    [DataRow("121687 487484", 121687.0, 487484.0)]
    [DataRow("155000.0 463000.0", 155000.0, 463000.0)]
    [DataRow("155000, 463000", 155000.0, 463000.0)]
    public void TryParse_ValidGrids_Parses(string text, double easting, double northing)
    {
        Assert.IsTrue(DutchRd.TryParse(text, out var rd), text);
        Assert.AreEqual(easting, rd.Easting, 0.5, "easting");
        Assert.AreEqual(northing, rd.Northing, 0.5, "northing");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("hello")]
    [DataRow("155000")]           // missing northing
    [DataRow("49.20575 16.576117")] // lat/lon, not RD (out of RD range)
    public void TryParse_Invalid_ReturnsFalse(string text)
        => Assert.IsFalse(DutchRd.TryParse(text, out _));

    // ---- Round trip ----

    [DataTestMethod]
    [DataRow(52.15517440, 5.38720621)] // Amersfoort
    [DataRow(52.37422, 4.89801)]       // Amsterdam
    [DataRow(51.92250, 4.47917)]       // Rotterdam approx
    [DataRow(50.85100, 5.69000)]       // Maastricht approx
    public void RoundTrip_FromThenToLatLon_RecoversWithinAMetre(double lat, double lon)
    {
        var original = new GeoCoordinate(lat, lon);
        var back = DutchRd.ToLatLon(DutchRd.FromLatLon(original));
        // The approximation is centimetre-accurate; allow ~a metre (~1e-5 deg).
        Assert.AreEqual(lat, back.Latitude, 2e-5, "lat");
        Assert.AreEqual(lon, back.Longitude, 2e-5, "lon");
    }
}
