using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class GeodesyTests
{
    // Geoscience Australia Vincenty test case (movable-type.co.uk/scripts/latlong-vincenty.html)
    private static readonly GeoCoordinate FlindersPeak = new(-37.95103341666667, 144.42486788888888);
    private static readonly GeoCoordinate Buninyong = new(-37.65282113888889, 143.92649552777777);

    // Land's End area -> John o' Groats area (movable-type spherical reference)
    private static readonly GeoCoordinate LandsEnd = new(50.0663888888889, -5.71472222222222);
    private static readonly GeoCoordinate JohnOGroats = new(58.6438888888889, -3.07);

    // ---- Distance (Vincenty inverse, WGS84) ----

    [TestMethod]
    public void DistanceMeters_FlindersToBuninyong_MatchesGeoscienceAustralia()
    {
        // Authoritative: 54972.271 m (sub-millimetre accuracy of Vincenty)
        var d = Geodesy.DistanceMeters(FlindersPeak, Buninyong);
        Assert.AreEqual(54972.271, d, 0.01);
    }

    [TestMethod]
    public void DistanceMeters_IsSymmetric()
    {
        var ab = Geodesy.DistanceMeters(FlindersPeak, Buninyong);
        var ba = Geodesy.DistanceMeters(Buninyong, FlindersPeak);
        Assert.AreEqual(ab, ba, 1e-6);
    }

    [TestMethod]
    public void DistanceMeters_SamePoint_IsZero()
        => Assert.AreEqual(0.0, Geodesy.DistanceMeters(FlindersPeak, FlindersPeak), 1e-6);

    [TestMethod]
    public void DistanceMeters_NearAntipodal_FallsBackAndReturnsFiniteValue()
    {
        // Vincenty inverse famously fails to converge near antipodal points; the haversine
        // fallback must still return a sane, finite half-circumference-ish distance.
        var d = Geodesy.DistanceMeters(new GeoCoordinate(0.0, 0.0), new GeoCoordinate(0.5, 179.5));
        Assert.IsTrue(double.IsFinite(d));
        Assert.IsTrue(d > 19_000_000 && d < 20_100_000, $"distance was {d}");
    }

    // ---- Initial / final bearing (Vincenty) ----

    [TestMethod]
    public void InitialBearingDegrees_FlindersToBuninyong_MatchesReference()
    {
        // 306°52'05.37" = 306.868158
        Assert.AreEqual(306.868158, Geodesy.InitialBearingDegrees(FlindersPeak, Buninyong), 0.01);
    }

    [TestMethod]
    public void FinalBearingDegrees_FlindersToBuninyong_MatchesReference()
    {
        // Forward final bearing (p1->p2 direction) = 307°10'25.07" = 307.173631
        Assert.AreEqual(307.173631, Geodesy.FinalBearingDegrees(FlindersPeak, Buninyong), 0.01);
    }

    [DataTestMethod]
    [DataRow(0.0, 0.0, 1.0, 0.0, 0.0)]   // due north
    [DataRow(0.0, 0.0, 0.0, 1.0, 90.0)]  // due east
    [DataRow(1.0, 0.0, 0.0, 0.0, 180.0)] // due south
    [DataRow(0.0, 1.0, 0.0, 0.0, 270.0)] // due west
    public void InitialBearingDegrees_CardinalDirections(double lat1, double lon1, double lat2, double lon2, double expected)
    {
        var b = Geodesy.InitialBearingDegrees(new GeoCoordinate(lat1, lon1), new GeoCoordinate(lat2, lon2));
        Assert.AreEqual(expected, b, 0.01);
    }

    [TestMethod]
    public void InitialBearingDegrees_AlwaysInZeroTo360()
    {
        var b = Geodesy.InitialBearingDegrees(Buninyong, FlindersPeak);
        Assert.IsTrue(b is >= 0.0 and < 360.0, $"bearing was {b}");
    }

    // ---- Midpoint ----

    [TestMethod]
    public void Midpoint_LandsEndToJohnOGroats_MatchesReference()
    {
        // movable-type great-circle midpoint: 54.362287, -4.530673
        var m = Geodesy.Midpoint(LandsEnd, JohnOGroats);
        Assert.AreEqual(54.362287, m.Latitude, 1e-4);
        Assert.AreEqual(-4.530673, m.Longitude, 1e-4);
    }

    [TestMethod]
    public void Midpoint_OfTwoPoints_IsEquidistantFromBoth()
    {
        var m = Geodesy.Midpoint(FlindersPeak, Buninyong);
        var toA = Geodesy.DistanceMeters(m, FlindersPeak);
        var toB = Geodesy.DistanceMeters(m, Buninyong);
        Assert.AreEqual(toA, toB, 1.0);
    }

    // ---- Destination (Vincenty direct) ----

    [TestMethod]
    public void Destination_ThenDistance_EqualsRequestedDistance()
    {
        var dest = Geodesy.Destination(FlindersPeak, 25000.0, 45.0);
        Assert.AreEqual(25000.0, Geodesy.DistanceMeters(FlindersPeak, dest), 0.1);
    }

    [TestMethod]
    public void Destination_AlongInitialBearingByDistance_ReachesTarget()
    {
        // Walking the geodesic from Flinders toward Buninyong by their exact distance lands on Buninyong.
        var d = Geodesy.DistanceMeters(FlindersPeak, Buninyong);
        var brg = Geodesy.InitialBearingDegrees(FlindersPeak, Buninyong);
        var dest = Geodesy.Destination(FlindersPeak, d, brg);
        Assert.AreEqual(Buninyong.Latitude, dest.Latitude, 1e-6, "lat");
        Assert.AreEqual(Buninyong.Longitude, dest.Longitude, 1e-6, "lon");
    }

    [TestMethod]
    public void Destination_DueNorth_IncreasesLatitudeOnly()
    {
        var start = new GeoCoordinate(0.0, 10.0);
        var dest = Geodesy.Destination(start, 111195.0, 0.0); // ~1 degree of latitude
        Assert.AreEqual(10.0, dest.Longitude, 1e-6);
        Assert.IsTrue(dest.Latitude > 0.99 && dest.Latitude < 1.01, $"lat was {dest.Latitude}");
    }

    [TestMethod]
    public void Destination_ZeroDistance_ReturnsStart()
    {
        var dest = Geodesy.Destination(FlindersPeak, 0.0, 123.0);
        Assert.AreEqual(FlindersPeak.Latitude, dest.Latitude, 1e-9);
        Assert.AreEqual(FlindersPeak.Longitude, dest.Longitude, 1e-9);
    }

    // ---- Haversine sanity (spherical reference for a long path) ----

    [TestMethod]
    public void DistanceMeters_LandsEndToJohnOGroats_CloseToKnownLength()
    {
        // Ellipsoidal distance ~970 km; assert within a few km of the spherical 968.85 km.
        var d = Geodesy.DistanceMeters(LandsEnd, JohnOGroats);
        Assert.AreEqual(970_000.0, d, 5_000.0);
    }
}
