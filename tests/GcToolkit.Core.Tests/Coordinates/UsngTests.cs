using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

/// <summary>
/// US National Grid. On the WGS84 datum the USNG is identical to MGRS, so these tests pin the
/// USNG string to the MGRS output and round-trip it. The canonical USNG worked example is the
/// Washington Monument: "18S UJ 23408 06479".
/// </summary>
[TestClass]
public class UsngTests
{
    private static readonly GeoCoordinate WashingtonMonument = new(38.88944771819054, -77.03610626563939);

    [TestMethod]
    public void FromLatLon_WashingtonMonument_ProducesKnownUsng()
    {
        var usng = Usng.FromLatLon(WashingtonMonument);
        Assert.AreEqual("18S UJ 23408 06479", usng);
    }

    [DataTestMethod]
    [DataRow(49.205750, 16.576117)]
    [DataRow(38.8894477, -77.0361063)]
    [DataRow(-33.8568, 151.2153)]
    [DataRow(0.0, 3.0)]
    public void FromLatLon_EqualsMgrs_OnWgs84(double lat, double lon)
    {
        var c = new GeoCoordinate(lat, lon);
        // USNG == MGRS(WGS84): the two strings must be byte-for-byte identical.
        Assert.AreEqual(Mgrs.FromLatLon(c), Usng.FromLatLon(c));
    }

    [DataTestMethod]
    [DataRow("18S UJ 23408 06479")]
    [DataRow("18SUJ2340806479")]
    public void TryParse_ValidUsng_Parses(string text)
        => Assert.IsTrue(Usng.TryParse(text, out _), text);

    [TestMethod]
    public void TryParse_DelegatesToMgrs()
    {
        Assert.IsTrue(Usng.TryParse("18S UJ 23408 06479", out var usng));
        Assert.IsTrue(Mgrs.TryParse("18S UJ 23408 06479", out var mgrs));
        Assert.AreEqual(mgrs, usng);
    }

    [DataTestMethod]
    [DataRow(49.205750, 16.576117)]
    [DataRow(38.8894477, -77.0361063)]
    [DataRow(-33.8568, 151.2153)]
    public void RoundTrip_FromThenTryParse_RecoversWithinOneMetre(double lat, double lon)
    {
        var usng = Usng.FromLatLon(new GeoCoordinate(lat, lon));
        Assert.IsTrue(Usng.TryParse(usng, out var c), usng);
        Assert.AreEqual(lat, c.Latitude, 1e-4, $"lat {usng}");
        Assert.AreEqual(lon, c.Longitude, 1e-4, $"lon {usng}");
    }

    [TestMethod]
    public void Format_Usng_RendersGrid()
    {
        var text = CoordinateFormatter.Format(WashingtonMonument, CoordinateFormat.Usng);
        Assert.AreEqual("18S UJ 23408 06479", text);
    }
}
