using GcToolkit.Core.Services.Devices;

namespace GcToolkit.Core.Tests.Services.Devices;

/// <summary>
/// The three heads disagree on how a compass says "no true north", and none of them uses a null:
/// Uno's Android compass emits <see cref="double.NaN"/>, its iOS compass forwards CoreLocation's -1,
/// and only WinRT returns null. <see cref="CompassHeading.TryCreate"/> is where that is flattened, so
/// the tool never renders "NaN°" or a bearing of -1.
/// </summary>
[TestClass]
public class CompassHeadingTests
{
    [TestMethod]
    public void TryCreate_WithFiniteHeadings_KeepsBothBearings()
    {
        var heading = CompassHeading.TryCreate(123.5d, 121.25d, DateTimeOffset.UnixEpoch);

        Assert.IsNotNull(heading);
        Assert.AreEqual(123.5d, heading.Value.MagneticNorthDegrees, 1e-9);
        Assert.AreEqual(121.25d, heading.Value.TrueNorthDegrees!.Value, 1e-9);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, heading.Value.Timestamp);
    }

    [TestMethod]
    [DataRow(double.NaN, DisplayName = "Android: no Geolocator fix")]
    [DataRow(-1d, DisplayName = "iOS: CLHeading.TrueHeading unavailable")]
    [DataRow(double.PositiveInfinity, DisplayName = "Garbage from a stale driver")]
    public void TryCreate_WithUnusableTrueNorth_ReportsNullTrueNorth(double trueNorth)
    {
        var heading = CompassHeading.TryCreate(90d, trueNorth, DateTimeOffset.UnixEpoch);

        Assert.IsNotNull(heading);
        Assert.AreEqual(90d, heading.Value.MagneticNorthDegrees, 1e-9);
        Assert.IsNull(heading.Value.TrueNorthDegrees);
    }

    [TestMethod]
    public void TryCreate_WithNullTrueNorth_ReportsNullTrueNorth()
    {
        var heading = CompassHeading.TryCreate(10d, null, DateTimeOffset.UnixEpoch);

        Assert.IsNotNull(heading);
        Assert.IsNull(heading.Value.TrueNorthDegrees);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void TryCreate_WithUnusableMagneticNorth_RejectsTheWholeReading(double magneticNorth)
    {
        // The bearing is the point of the reading — a stale driver must leave the last good one on
        // screen rather than replace it with nonsense.
        Assert.IsNull(CompassHeading.TryCreate(magneticNorth, 90d, DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    [DataRow(-90d, 270d)]
    [DataRow(360d, 0d)]
    [DataRow(450d, 90d)]
    [DataRow(-370d, 350d)]
    public void TryCreate_WithOutOfRangeMagneticNorth_WrapsInto0To360(double raw, double expected)
    {
        var heading = CompassHeading.TryCreate(raw, null, DateTimeOffset.UnixEpoch);

        Assert.IsNotNull(heading);
        Assert.AreEqual(expected, heading.Value.MagneticNorthDegrees, 1e-9);
    }

    [TestMethod]
    public void TryCreate_WithTrueNorthAbove360_WrapsInto0To360()
    {
        var heading = CompassHeading.TryCreate(0d, 400d, DateTimeOffset.UnixEpoch);

        Assert.IsNotNull(heading);
        Assert.AreEqual(40d, heading.Value.TrueNorthDegrees!.Value, 1e-9);
    }
}
