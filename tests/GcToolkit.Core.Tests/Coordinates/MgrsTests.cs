using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class MgrsTests
{
    // Exact location of the Wikipedia worked example "18S UJ 23408 06479" (1 m precision).
    private static readonly GeoCoordinate WashingtonMonument = new(38.88944771819054, -77.03610626563939);

    // ---- FromLatLon: Wikipedia worked example (Washington Monument) ----

    [TestMethod]
    public void FromLatLon_WashingtonMonument_ProducesKnownMgrs()
    {
        // 38.889448, -77.036106 -> 18S UJ 23408 06479 (Wikipedia worked example)
        var mgrs = Mgrs.FromLatLon(WashingtonMonument);
        Assert.AreEqual("18S UJ 23408 06479", mgrs);
    }

    [TestMethod]
    public void FromLatLon_Golden_MatchesReferenceMgrs()
    {
        // 49.205750, 16.576117 -> 33U XQ 14803 51524 (reference: mgrs python package "33UXQ1480351524")
        var mgrs = Mgrs.FromLatLon(new GeoCoordinate(49.205750, 16.576117));
        Assert.AreEqual("33U XQ 14803 51524", mgrs);
    }

    [TestMethod]
    public void FromLatLon_DefaultDigits_IsFivePlusFive()
    {
        var mgrs = Mgrs.FromLatLon(WashingtonMonument);
        // 18S UJ ##### ##### : two 5-digit groups
        var parts = mgrs.Split(' ');
        Assert.AreEqual(4, parts.Length);
        Assert.AreEqual(5, parts[2].Length);
        Assert.AreEqual(5, parts[3].Length);
    }

    [DataTestMethod]
    [DataRow(1, "18S UJ 2 0")]
    [DataRow(2, "18S UJ 23 06")]
    [DataRow(3, "18S UJ 234 064")]
    [DataRow(4, "18S UJ 2340 0647")]
    [DataRow(5, "18S UJ 23408 06479")]
    public void FromLatLon_PrecisionDigits_TruncatesGrid(int digits, string expected)
        => Assert.AreEqual(expected, Mgrs.FromLatLon(WashingtonMonument, digits));

    [TestMethod]
    public void FromLatLon_Equator_UsesNorthernBandN()
    {
        // lat=0, lon=3 -> zone 31, band N, square EA, all-zero grid.
        var mgrs = Mgrs.FromLatLon(new GeoCoordinate(0.0, 3.0));
        StringAssert.StartsWith(mgrs, "31N");
    }

    // ---- TryParse ----

    [DataTestMethod]
    [DataRow("18S UJ 23408 06479")]
    [DataRow("18SUJ2340806479")]
    [DataRow("33U VR 05083 44673")]
    [DataRow("33UVR0508344673")]
    [DataRow("4Q FJ 1 6")]            // Wikipedia 10 km example
    [DataRow("4QFJ1234567890")]       // Wikipedia 1 m example
    public void TryParse_ValidNotations_Parses(string text)
        => Assert.IsTrue(Mgrs.TryParse(text, out _), text);

    [TestMethod]
    public void TryParse_WikipediaTenKmExample_RecoversToSquareRegion()
    {
        // 4Q FJ 1 6 sits in Hawaii (~21.34N, -157.94E); the 10 km cell centre is within ~7 km.
        Assert.IsTrue(Mgrs.TryParse("4Q FJ 1 6", out var c));
        Assert.AreEqual(21.34, c.Latitude, 0.1);
        Assert.AreEqual(-157.94, c.Longitude, 0.1);
    }

    [TestMethod]
    public void TryParse_WashingtonMonument_RecoversApproxLatLon()
    {
        Assert.IsTrue(Mgrs.TryParse("18S UJ 23408 06479", out var c));
        Assert.AreEqual(WashingtonMonument.Latitude, c.Latitude, 1e-3);
        Assert.AreEqual(WashingtonMonument.Longitude, c.Longitude, 1e-3);
    }

    [TestMethod]
    public void TryParse_LowPrecision_RecoversToSquareCentre()
    {
        // 1+1 digit = 10km square; centre is within ~7km of the truth.
        Assert.IsTrue(Mgrs.TryParse("18S UJ 2 0", out var c));
        Assert.AreEqual(38.88, c.Latitude, 0.1);
        Assert.AreEqual(-77.03, c.Longitude, 0.1);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("hello")]
    [DataRow("18S UJ 234 0647")]   // unequal digit groups
    [DataRow("49.20575 16.576117")]
    public void TryParse_Invalid_ReturnsFalse(string text)
        => Assert.IsFalse(Mgrs.TryParse(text, out _));

    // ---- Round trip ----

    [DataTestMethod]
    [DataRow(49.205750, 16.576117)]
    [DataRow(38.8894477, -77.0361063)]
    [DataRow(-33.8568, 151.2153)]
    [DataRow(0.0, 3.0)]
    public void RoundTrip_FromThenTryParse_RecoversWithinOneMetre(double lat, double lon)
    {
        var mgrs = Mgrs.FromLatLon(new GeoCoordinate(lat, lon));
        Assert.IsTrue(Mgrs.TryParse(mgrs, out var c), mgrs);
        // 1m grid truncation -> within a couple of metres (~2e-5 deg)
        Assert.AreEqual(lat, c.Latitude, 1e-4, $"lat {mgrs}");
        Assert.AreEqual(lon, c.Longitude, 1e-4, $"lon {mgrs}");
    }
}
