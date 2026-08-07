namespace GcToolkit.Core.Coordinates;

/// <summary>
/// The full distance/bearing/midpoint result for a pair of coordinates: the geodesic distance in
/// every common unit, both the initial and final bearing, and the great-circle midpoint.
/// </summary>
public readonly record struct CoordinateDistanceResult(
    double DistanceMeters,
    double DistanceKilometers,
    double DistanceFeet,
    double DistanceMiles,
    double InitialBearingDegrees,
    double FinalBearingDegrees,
    GeoCoordinate Midpoint);

/// <summary>
/// Thin helper that turns two <see cref="GeoCoordinate"/>s into a ready-to-display
/// <see cref="CoordinateDistanceResult"/>. All the geodesy (Vincenty distance/bearing, great-circle
/// midpoint) lives in <see cref="Geodesy"/>; this type only orchestrates it and converts the metric
/// distance into the imperial/decimal units the UI shows. Keeps the ViewModel thin and testable.
/// </summary>
public static class CoordinateDistanceCalculator
{
    /// <summary>Metres per international foot's reciprocal: 1 m = 3.280839895 ft.</summary>
    private const double FeetPerMeter = 3.280839895;

    /// <summary>1 statute mile = 1609.344 m.</summary>
    private const double MetersPerMile = 1609.344;

    public static double MetersToKilometers(double meters) => meters / 1000.0;

    public static double MetersToFeet(double meters) => meters * FeetPerMeter;

    public static double MetersToMiles(double meters) => meters / MetersPerMile;

    /// <summary>Computes distance (all units), initial + final bearing, and the midpoint for A→B.</summary>
    public static CoordinateDistanceResult Calculate(GeoCoordinate a, GeoCoordinate b)
    {
        var meters = Geodesy.DistanceMeters(a, b);
        return new CoordinateDistanceResult(
            DistanceMeters: meters,
            DistanceKilometers: MetersToKilometers(meters),
            DistanceFeet: MetersToFeet(meters),
            DistanceMiles: MetersToMiles(meters),
            InitialBearingDegrees: Geodesy.InitialBearingDegrees(a, b),
            FinalBearingDegrees: Geodesy.FinalBearingDegrees(a, b),
            Midpoint: Geodesy.Midpoint(a, b));
    }

    /// <summary>Formats the midpoint in the geocaching norm — degrees and decimal minutes.</summary>
    public static string FormatMidpoint(GeoCoordinate midpoint)
        => CoordinateFormatter.Format(midpoint, CoordinateFormat.DegreesDecimalMinutes);
}
