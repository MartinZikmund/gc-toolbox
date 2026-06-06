namespace GcToolkit.Core.Coordinates;

/// <summary>Converts a distance expressed in a <see cref="DistanceUnit"/> to metres, the unit the
/// geodesy routines (e.g. <see cref="Geodesy.Destination"/>) operate in.</summary>
public static class DistanceUnits
{
    private const double FeetPerMeter = 3.280839895; // 1 / 0.3048
    private const double MetersPerMile = 1609.344;   // international mile

    /// <summary>Converts <paramref name="value"/> in <paramref name="unit"/> to metres.</summary>
    public static double ToMeters(double value, DistanceUnit unit) => unit switch
    {
        DistanceUnit.Kilometer => value * 1000.0,
        DistanceUnit.Feet => value / FeetPerMeter,
        DistanceUnit.Mile => value * MetersPerMile,
        _ => value,
    };
}
