using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class GeoCoordinateTests
{
    [DataTestMethod]
    [DataRow(0.0, 0.0)]
    [DataRow(49.20575, 16.576117)]
    [DataRow(-90.0, -180.0)]
    [DataRow(90.0, 180.0)]
    [DataRow(-33.8568, 151.2153)]
    public void IsValid_WithinBounds_ReturnsTrue(double lat, double lon)
        => Assert.IsTrue(new GeoCoordinate(lat, lon).IsValid);

    [DataTestMethod]
    [DataRow(90.0001, 0.0)]
    [DataRow(-90.0001, 0.0)]
    [DataRow(0.0, 180.0001)]
    [DataRow(0.0, -180.0001)]
    [DataRow(double.NaN, 0.0)]
    [DataRow(0.0, double.NaN)]
    public void IsValid_OutOfBounds_ReturnsFalse(double lat, double lon)
        => Assert.IsFalse(new GeoCoordinate(lat, lon).IsValid);

    [TestMethod]
    public void Record_Equality_ComparesByValue()
        => Assert.AreEqual(new GeoCoordinate(1.0, 2.0), new GeoCoordinate(1.0, 2.0));
}
