using System.Globalization;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class CoordinateProjectionTests
{
    // ---- Distance-unit conversion to metres ----

    [TestMethod]
    public void ToMeters_Meter_IsUnchanged()
        => Assert.AreEqual(1234.5, DistanceUnits.ToMeters(1234.5, DistanceUnit.Meter), 1e-9);

    [TestMethod]
    public void ToMeters_Kilometer_MultipliesByThousand()
        => Assert.AreEqual(2500.0, DistanceUnits.ToMeters(2.5, DistanceUnit.Kilometer), 1e-9);

    [TestMethod]
    public void ToMeters_Feet_UsesInternationalFoot()
        => Assert.AreEqual(304.8, DistanceUnits.ToMeters(1000.0, DistanceUnit.Feet), 1e-9);

    [TestMethod]
    public void ToMeters_Yard_UsesInternationalYard()
        => Assert.AreEqual(914.4, DistanceUnits.ToMeters(1000.0, DistanceUnit.Yard), 1e-9);

    [TestMethod]
    public void ToMeters_Yard_IsThreeFeet()
        => Assert.AreEqual(DistanceUnits.ToMeters(3.0, DistanceUnit.Feet), DistanceUnits.ToMeters(1.0, DistanceUnit.Yard), 1e-12);

    [TestMethod]
    public void ToMeters_Mile_UsesInternationalMile()
        => Assert.AreEqual(1609.344, DistanceUnits.ToMeters(1.0, DistanceUnit.Mile), 1e-9);

    // ---- Projection result (multi-format) ----

    [TestMethod]
    public void Project_DueEast1000m_IncreasesLongitudeLatitudeUnchanged()
    {
        var start = new GeoCoordinate(0.0, 10.0);
        var result = CoordinateProjection.Project(start, 1000.0, 90.0);

        Assert.IsTrue(result.Destination.Latitude > -0.0001 && result.Destination.Latitude < 0.0001,
            $"latitude moved: {result.Destination.Latitude}");
        Assert.IsTrue(result.Destination.Longitude > 10.0,
            $"longitude did not increase eastward: {result.Destination.Longitude}");
    }

    [TestMethod]
    public void Project_DueNorth_IncreasesLatitudeLongitudeUnchanged()
    {
        var start = new GeoCoordinate(0.0, 10.0);
        var result = CoordinateProjection.Project(start, 111195.0, 0.0); // ~1 degree of latitude

        Assert.AreEqual(10.0, result.Destination.Longitude, 1e-6);
        Assert.IsTrue(result.Destination.Latitude is > 0.99 and < 1.01, $"lat was {result.Destination.Latitude}");
    }

    [TestMethod]
    public void Project_ProducesAllThreeAngularFormats()
    {
        var start = new GeoCoordinate(49.205750, 16.576117);
        var result = CoordinateProjection.Project(start, 500.0, 45.0);

        // Each format is non-empty and matches the library formatter for the same destination.
        Assert.AreEqual(CoordinateFormatter.Format(result.Destination, CoordinateFormat.DecimalDegrees), result.DecimalDegrees);
        Assert.AreEqual(CoordinateFormatter.Format(result.Destination, CoordinateFormat.DegreesDecimalMinutes), result.DegreesDecimalMinutes);
        Assert.AreEqual(CoordinateFormatter.Format(result.Destination, CoordinateFormat.DegreesMinutesSeconds), result.DegreesMinutesSeconds);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.DecimalDegrees));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.DegreesDecimalMinutes));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.DegreesMinutesSeconds));
    }

    // ---- ViewModel wiring ----

    private static CoordinateProjectionViewModel CreateViewModel(
        FakeClipboardService? clipboard = null,
        FakeShareService? share = null)
    {
        var localizer = new FakeStringLocalizer();
        var catalog = new StubCatalogService("CoordinateProjection");
        var preferences = new InMemoryPreferences();

        return new CoordinateProjectionViewModel(
            catalog,
            new RecentsService(preferences, catalog),
            new FavoriteToolsService(preferences, catalog),
            localizer,
            clipboard ?? new FakeClipboardService(),
            share ?? new FakeShareService());
    }

    [TestMethod]
    public void ValidInput_ProducesOutputAndHasResult()
    {
        var vm = CreateViewModel();
        vm.StartCoordinate = "N 00 00.000 E 010 00.000";
        vm.AngleDegrees = "90";
        vm.Distance = "1000";
        vm.DistanceUnitIndex = 0; // metres

        Assert.IsTrue(vm.HasResult);
        Assert.IsFalse(vm.HasError);
        Assert.IsFalse(string.IsNullOrWhiteSpace(vm.DecimalDegrees));
        Assert.IsFalse(string.IsNullOrWhiteSpace(vm.DegreesDecimalMinutes));
        Assert.IsFalse(string.IsNullOrWhiteSpace(vm.DegreesMinutesSeconds));
    }

    [TestMethod]
    public void InvalidStartCoordinate_SetsErrorAndNoResult()
    {
        var vm = CreateViewModel();
        vm.AngleDegrees = "90";
        vm.Distance = "1000";
        vm.StartCoordinate = "not a coordinate";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasResult);
        Assert.AreEqual(string.Empty, vm.DegreesDecimalMinutes);
    }

    [TestMethod]
    public void EmptyInput_NoErrorNoResult()
    {
        var vm = CreateViewModel();

        Assert.IsFalse(vm.HasError);
        Assert.IsFalse(vm.HasResult);
    }

    [TestMethod]
    public void KilometerUnit_ScalesDistance()
    {
        var vm = CreateViewModel();
        vm.StartCoordinate = "N 00 00.000 E 010 00.000";
        vm.AngleDegrees = "0"; // due north
        vm.DistanceUnitIndex = 1; // kilometre
        vm.Distance = "111.195"; // ~1 degree of latitude

        Assert.IsTrue(vm.HasResult);
        var parsed = CoordinateParser.TryParse(vm.DecimalDegrees, out var dest, out _);
        Assert.IsTrue(parsed, "destination output should round-trip through the parser");
        Assert.IsTrue(dest.Latitude is > 0.99 and < 1.01, $"lat was {dest.Latitude}");
    }

    [TestMethod]
    public void InvalidDistance_SetsErrorNoResult()
    {
        var vm = CreateViewModel();
        vm.StartCoordinate = "N 00 00.000 E 010 00.000";
        vm.AngleDegrees = "90";
        vm.Distance = "abc";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasResult);
    }

    [DataRow(0, 1000.0, 1000.0)]      // metre
    [DataRow(1, 1.0, 1000.0)]         // kilometre
    [DataRow(2, 1000.0, 304.8)]       // foot
    [DataRow(3, 1000.0, 914.4)]       // yard
    [DataRow(4, 1.0, 1609.344)]       // mile
    [TestMethod]
    public void DistanceUnitIndex_MapsToTheSameUnitAsThePicker(int index, double value, double expectedMeters)
    {
        const string Start = "N 49 12.345 E 016 34.567";

        var vm = CreateViewModel();
        vm.StartCoordinate = Start;
        vm.AngleDegrees = "45";
        vm.DistanceUnitIndex = index;
        vm.Distance = value.ToString(CultureInfo.InvariantCulture);

        Assert.IsTrue(vm.HasResult);

        // The projected point must match projecting the equivalent metre distance directly.
        Assert.IsTrue(CoordinateParser.TryParse(Start, out var parsedStart, out _));
        var direct = CoordinateProjection.Project(parsedStart, expectedMeters, 45.0);
        Assert.AreEqual(direct.DegreesDecimalMinutes, vm.DegreesDecimalMinutes);
    }

    [TestMethod]
    public void NegativeDistance_SetsErrorNoResult()
    {
        var vm = CreateViewModel();
        vm.StartCoordinate = "N 00 00.000 E 010 00.000";
        vm.AngleDegrees = "90";
        vm.Distance = "-100";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasResult);
    }

    [TestMethod]
    public void ZeroDistance_ProjectsToTheStartPoint()
    {
        var vm = CreateViewModel();
        vm.StartCoordinate = "N 49 12.345 E 016 34.567";
        vm.AngleDegrees = "90";
        vm.Distance = "0";

        Assert.IsTrue(vm.HasResult);
        Assert.IsFalse(vm.HasError);
        Assert.IsTrue(CoordinateParser.TryParse(vm.DecimalDegrees, out var dest, out _));
        Assert.IsTrue(CoordinateParser.TryParse(vm.StartCoordinate, out var start, out _));
        Assert.AreEqual(start.Latitude, dest.Latitude, 1e-6);
        Assert.AreEqual(start.Longitude, dest.Longitude, 1e-6);
    }

    [TestMethod]
    public void CopyOutput_PutsDefaultFormatOnClipboard()
    {
        var clipboard = new FakeClipboardService();
        var vm = CreateViewModel(clipboard: clipboard);
        vm.StartCoordinate = "N 00 00.000 E 010 00.000";
        vm.AngleDegrees = "90";
        vm.Distance = "1000";

        Assert.IsTrue(vm.CopyOutputCommand.CanExecute(null));
        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual(vm.DegreesDecimalMinutes, clipboard.LastText);
    }

    [TestMethod]
    public void Clear_ResetsInputAndResult()
    {
        var vm = CreateViewModel();
        vm.StartCoordinate = "N 00 00.000 E 010 00.000";
        vm.AngleDegrees = "90";
        vm.DistanceUnitIndex = 4; // mile
        vm.Distance = "1000";
        Assert.IsTrue(vm.HasResult);

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.StartCoordinate);
        Assert.AreEqual(string.Empty, vm.AngleDegrees);
        Assert.AreEqual(string.Empty, vm.Distance);
        Assert.AreEqual(0, vm.DistanceUnitIndex);
        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.HasError);
    }
}
