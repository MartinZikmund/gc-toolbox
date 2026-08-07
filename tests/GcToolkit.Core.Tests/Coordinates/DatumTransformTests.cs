using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

/// <summary>
/// The generalized datum engine (<see cref="DatumTransform"/> + <see cref="DatumRegistry"/>).
/// Verified against the Ordnance Survey worked example for OSGB36 and round-trips for the rest.
/// </summary>
[TestClass]
public class DatumTransformTests
{
    [TestMethod]
    public void ToWgs84_Wgs84Datum_IsIdentity()
    {
        var c = new GeoCoordinate(49.2, 16.5);
        var back = DatumTransform.ToWgs84(c, DatumRegistry.Wgs84);
        Assert.AreEqual(c.Latitude, back.Latitude, 1e-12);
        Assert.AreEqual(c.Longitude, back.Longitude, 1e-12);
    }

    [TestMethod]
    public void FromWgs84_Osgb36_MatchesOrdnanceSurveyWorkedExample()
    {
        // OS worked example: WGS84 52.65798 N, 1.71605 E -> OSGB36 ~52.6575703 N, 1.7179215 E.
        var osgb = DatumTransform.FromWgs84(new GeoCoordinate(52.65798, 1.71605), DatumRegistry.Osgb36);
        Assert.AreEqual(52.6575703, osgb.Latitude, 1e-3, "lat");
        Assert.AreEqual(1.7179215, osgb.Longitude, 1e-3, "lon");
    }

    [DataTestMethod]
    [DataRow("OGB-7")]
    [DataRow("AME-7")]
    [DataRow("EUR-7")]
    [DataRow("WGC-7")]
    [DataRow("NAS-C")]
    [DataRow("TOY-A")]
    [DataRow("EUR-M")]
    public void RoundTrip_FromThenToWgs84_RecoversWithinCentimetres(string code)
    {
        var datum = DatumRegistry.Find(code)!.Value;
        var wgs = new GeoCoordinate(50.0, 8.0);
        var local = DatumTransform.FromWgs84(wgs, datum);
        var back = DatumTransform.ToWgs84(local, datum);
        // Round-trip with height assumed zero recovers to within a few cm (~1e-6 deg).
        Assert.AreEqual(wgs.Latitude, back.Latitude, 1e-6, $"{code} lat");
        Assert.AreEqual(wgs.Longitude, back.Longitude, 1e-6, $"{code} lon");
    }

    [TestMethod]
    public void Find_IsHyphenAndCaseInsensitive()
    {
        Assert.IsNotNull(DatumRegistry.Find("OGB-7"));
        Assert.IsNotNull(DatumRegistry.Find("ogb7"));
        Assert.IsNotNull(DatumRegistry.Find("OGBM"));
        Assert.IsNull(DatumRegistry.Find("NOPE"));
    }

    [TestMethod]
    public void Registry_ContainsTheFullNimaTablePlusPreciseDatums()
    {
        // WGS84 + 4 precise 7-parameter datums + 183 NIMA datums = 188.
        Assert.IsTrue(DatumRegistry.All.Count >= 180, $"only {DatumRegistry.All.Count} datums");
        // Spot-check representative codes across regions.
        foreach (var code in new[] { "WGS84", "OGB-7", "AME-7", "EUR-7", "ADI-M", "NAS-C", "TOY-B", "SPK-A", "ARF-M", "SAN-C" })
        {
            Assert.IsNotNull(DatumRegistry.Find(code), code);
        }
    }

    [TestMethod]
    public void Nad27_ShiftsWestUsPointByExpectedTensOfMetres()
    {
        // NAD27 (NAS-C) differs from WGS84 by roughly 10s of metres in CONUS; the shift must be
        // non-trivial and in a sane range (not zero, not kilometres).
        var wgs = new GeoCoordinate(39.0, -105.0);
        var nad27 = DatumTransform.FromWgs84(wgs, DatumRegistry.Find("NAS-C")!.Value);
        var dLat = Math.Abs(nad27.Latitude - wgs.Latitude);
        var dLon = Math.Abs(nad27.Longitude - wgs.Longitude);
        Assert.IsTrue(dLat is > 1e-6 and < 1e-2, $"dLat={dLat}");
        Assert.IsTrue(dLon is > 1e-6 and < 1e-2, $"dLon={dLon}");
    }

    [DataTestMethod]
    [DataRow("OGB-7")]
    [DataRow("AME-7")]
    [DataRow("WGC-7")]
    public void RoundTrip_ScaledDatum_IsExactNotJustCentimetreClose(string code)
    {
        // A datum with a non-zero scale exposes an approximated (parameter-negating) inverse: the
        // round trip must land back on the millimetre, not a couple of centimetres away.
        var datum = DatumRegistry.Find(code)!.Value;
        var wgs = new GeoCoordinate(52.65798, 1.71605);

        var back = DatumTransform.ToWgs84(DatumTransform.FromWgs84(wgs, datum), datum);

        // A few mm; the residual is the h=0 assumption, not the transform.
        Assert.AreEqual(wgs.Latitude, back.Latitude, 5e-8, $"{code} lat");
        Assert.AreEqual(wgs.Longitude, back.Longitude, 5e-8, $"{code} lon");
    }
}
