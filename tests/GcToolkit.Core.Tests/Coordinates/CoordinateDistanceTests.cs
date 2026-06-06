using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Coordinates;

/// <summary>
/// Covers the <see cref="CoordinateDistanceCalculator"/> helper: the unit conversions and the
/// multi-format result it builds for the distance/bearing/midpoint tool. The heavy geodesy is the
/// library's job (and is unit-tested in <c>GeodesyTests</c>); these tests verify the wiring — that we
/// convert metres to every unit correctly and surface a non-empty distance/bearing/midpoint.
/// </summary>
[TestClass]
public class CoordinateDistanceTests
{
    // Geoscience Australia Vincenty reference (same points GeodesyTests uses): 54972.271 m.
    private static readonly GeoCoordinate FlindersPeak = new(-37.95103341666667, 144.42486788888888);
    private static readonly GeoCoordinate Buninyong = new(-37.65282113888889, 143.92649552777777);

    // ---- Distance-unit conversions ----

    [TestMethod]
    public void MetersToKilometers_DividesByOneThousand()
        => Assert.AreEqual(1.5, CoordinateDistanceCalculator.MetersToKilometers(1500.0), 1e-9);

    [TestMethod]
    public void MetersToFeet_UsesInternationalFoot()
        => Assert.AreEqual(3.280839895, CoordinateDistanceCalculator.MetersToFeet(1.0), 1e-9);

    [TestMethod]
    public void MetersToMiles_UsesStatuteMile()
        => Assert.AreEqual(1.0, CoordinateDistanceCalculator.MetersToMiles(1609.344), 1e-9);

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(1234.5)]
    [DataRow(54972.271)]
    public void UnitConversions_RoundTripThroughMeters(double meters)
    {
        // Each unit, converted back to metres, returns the original value.
        Assert.AreEqual(meters, CoordinateDistanceCalculator.MetersToKilometers(meters) * 1000.0, 1e-6, "km");
        Assert.AreEqual(meters, CoordinateDistanceCalculator.MetersToFeet(meters) / 3.280839895, 1e-6, "ft");
        Assert.AreEqual(meters, CoordinateDistanceCalculator.MetersToMiles(meters) * 1609.344, 1e-6, "mi");
    }

    // ---- Building the result from two coordinates ----

    [TestMethod]
    public void Calculate_KnownPair_DistanceMatchesGeodesy()
    {
        var result = CoordinateDistanceCalculator.Calculate(FlindersPeak, Buninyong);

        // Cross-check the library: ~54972.271 m, i.e. ~54.97 km / ~180,348 ft / ~34.16 mi.
        Assert.AreEqual(54972.271, result.DistanceMeters, 0.01);
        Assert.AreEqual(54.972271, result.DistanceKilometers, 1e-3);
        Assert.AreEqual(180_355.0, result.DistanceFeet, 5.0);
        Assert.AreEqual(34.158, result.DistanceMiles, 1e-2);
    }

    [TestMethod]
    public void Calculate_KnownPair_BearingsMatchGeodesy()
    {
        var result = CoordinateDistanceCalculator.Calculate(FlindersPeak, Buninyong);

        // GeodesyTests asserts these exact reference values.
        Assert.AreEqual(306.868158, result.InitialBearingDegrees, 0.01);
        Assert.AreEqual(307.173631, result.FinalBearingDegrees, 0.01);
    }

    [TestMethod]
    public void Calculate_KnownPair_MidpointIsEquidistantFromBoth()
    {
        var result = CoordinateDistanceCalculator.Calculate(FlindersPeak, Buninyong);

        var toA = Geodesy.DistanceMeters(result.Midpoint, FlindersPeak);
        var toB = Geodesy.DistanceMeters(result.Midpoint, Buninyong);
        Assert.AreEqual(toA, toB, 1.0);
    }

    [TestMethod]
    public void Calculate_SamePoint_IsZeroDistance()
    {
        var result = CoordinateDistanceCalculator.Calculate(FlindersPeak, FlindersPeak);

        Assert.AreEqual(0.0, result.DistanceMeters, 1e-6);
        Assert.AreEqual(0.0, result.DistanceKilometers, 1e-9);
        Assert.AreEqual(0.0, result.DistanceFeet, 1e-6);
        Assert.AreEqual(0.0, result.DistanceMiles, 1e-9);
    }

    [TestMethod]
    public void Calculate_ClosePair_DistanceIsPlausible()
    {
        // Two points ~157 m apart (0.001 degree of latitude is ~111 m; with a small lon offset).
        var a = new GeoCoordinate(49.0, 16.0);
        var b = new GeoCoordinate(49.001, 16.001);
        var result = CoordinateDistanceCalculator.Calculate(a, b);

        Assert.IsTrue(result.DistanceMeters is > 100.0 and < 200.0, $"distance was {result.DistanceMeters}");
    }

    // ---- Midpoint formatting (geocaching norm = degrees + decimal minutes) ----

    [TestMethod]
    public void FormatMidpoint_UsesDegreesDecimalMinutes()
    {
        var result = CoordinateDistanceCalculator.Calculate(
            new GeoCoordinate(49.205750, 16.576117),
            new GeoCoordinate(49.205750, 16.576117));

        var formatted = CoordinateDistanceCalculator.FormatMidpoint(result.Midpoint);
        Assert.AreEqual(CoordinateFormatter.Format(result.Midpoint, CoordinateFormat.DegreesDecimalMinutes), formatted);
        // Sanity: the geocaching DDM shape carries the ' minutes marker.
        StringAssert.Contains(formatted, "'");
    }
}

/// <summary>
/// Exercises <see cref="CoordinateDistanceViewModel"/> with hand-written fakes: live recompute on
/// input, the parse-failure error state, the formatted outputs, and the copy/share/clear commands —
/// all without a UI head.
/// </summary>
[TestClass]
public sealed class CoordinateDistanceViewModelTests
{
    private const string ParseErrorKey = "CoordinateDistanceParseError";
    private const string ParseErrorText = "Could not parse a coordinate.";

    // Two valid coordinates ~54.97 km apart (the Geoscience Australia Vincenty pair).
    private const string ValidA = "S 37 57.062 E 144 25.492";
    private const string ValidB = "S 37 39.169 E 143 55.590";

    [TestMethod]
    public void Initial_State_HasNoResultAndNoError()
    {
        var sut = CreateSut();

        Assert.IsFalse(sut.HasResult);
        Assert.IsFalse(sut.HasError);
        Assert.AreEqual(string.Empty, sut.PointAText);
        Assert.AreEqual(string.Empty, sut.PointBText);
    }

    [TestMethod]
    public void BothValid_ProducesNonEmptyDistanceBearingAndMidpoint()
    {
        var sut = CreateSut();

        sut.PointAText = ValidA;
        sut.PointBText = ValidB;

        Assert.IsTrue(sut.HasResult);
        Assert.IsFalse(sut.HasError);
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.DistanceMetersText));
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.DistanceKilometersText));
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.DistanceFeetText));
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.DistanceMilesText));
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.InitialBearingText));
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.FinalBearingText));
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.MidpointText));
    }

    [TestMethod]
    public void BothValid_DistanceTextsReflectKnownPair()
    {
        var sut = CreateSut();

        sut.PointAText = ValidA;
        sut.PointBText = ValidB;

        // ~54.97 km / ~34.16 mi for this pair — the formatted strings must carry those magnitudes.
        StringAssert.Contains(sut.DistanceKilometersText, "54.97");
        StringAssert.Contains(sut.DistanceMilesText, "34.1");
    }

    [TestMethod]
    public void OnlyOnePointEntered_StaysWithoutResultOrError()
    {
        var sut = CreateSut();

        sut.PointAText = ValidA;

        // One coordinate alone is not an error — just an incomplete (no-result) state.
        Assert.IsFalse(sut.HasResult);
        Assert.IsFalse(sut.HasError);
    }

    [TestMethod]
    public void InvalidPointA_SetsErrorAndClearsResult()
    {
        var sut = CreateSut();
        sut.PointBText = ValidB;

        sut.PointAText = "not a coordinate";

        Assert.IsTrue(sut.HasError);
        Assert.AreEqual(ParseErrorText, sut.ErrorMessage);
        Assert.IsFalse(sut.HasResult);
    }

    [TestMethod]
    public void InvalidPointB_SetsError()
    {
        var sut = CreateSut();
        sut.PointAText = ValidA;

        sut.PointBText = "qwerty";

        Assert.IsTrue(sut.HasError);
        Assert.IsFalse(sut.HasResult);
    }

    [TestMethod]
    public void FixingInvalidInput_ClearsErrorAndRecomputes()
    {
        var sut = CreateSut();
        sut.PointAText = "garbage";
        sut.PointBText = ValidB;
        Assert.IsTrue(sut.HasError);

        sut.PointAText = ValidA;

        Assert.IsFalse(sut.HasError);
        Assert.IsTrue(sut.HasResult);
    }

    [TestMethod]
    public void CopyOutput_WhenResult_CopiesSummaryWithAllParts()
    {
        var clipboard = new FakeClipboardService();
        var sut = CreateSut(clipboard: clipboard);
        sut.PointAText = ValidA;
        sut.PointBText = ValidB;

        sut.CopyOutputCommand.Execute(null);

        Assert.AreEqual(1, clipboard.SetTextCallCount);
        var text = clipboard.LastText!;
        StringAssert.Contains(text, sut.DistanceKilometersText);
        StringAssert.Contains(text, sut.InitialBearingText);
        StringAssert.Contains(text, sut.MidpointText);
    }

    [TestMethod]
    public void CopyOutput_WithoutResult_CannotExecute()
    {
        var sut = CreateSut();

        Assert.IsFalse(sut.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ShareOutput_WhenResult_SharesSummary()
    {
        var share = new FakeShareService();
        var sut = CreateSut(share: share);
        sut.PointAText = ValidA;
        sut.PointBText = ValidB;

        await sut.ShareOutputCommand.ExecuteAsync(null);

        Assert.AreEqual(1, share.ShareTextCallCount);
        Assert.IsFalse(string.IsNullOrWhiteSpace(share.LastText));
    }

    [TestMethod]
    public void Clear_ResetsBothInputsAndResult()
    {
        var sut = CreateSut();
        sut.PointAText = ValidA;
        sut.PointBText = ValidB;
        Assert.IsTrue(sut.HasResult);

        sut.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, sut.PointAText);
        Assert.AreEqual(string.Empty, sut.PointBText);
        Assert.IsFalse(sut.HasResult);
        Assert.IsFalse(sut.HasError);
    }

    private static CoordinateDistanceViewModel CreateSut(
        FakeClipboardService? clipboard = null,
        FakeShareService? share = null)
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name_CoordinateDistance"] = "Distance & bearing",
            [ParseErrorKey] = ParseErrorText,
        });

        return new CoordinateDistanceViewModel(
            new StubCatalogService("CoordinateDistance"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            localizer,
            clipboard ?? new FakeClipboardService(),
            share ?? new FakeShareService());
    }
}
