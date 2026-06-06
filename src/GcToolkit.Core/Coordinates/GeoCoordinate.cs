namespace GcToolkit.Core.Coordinates;

/// <summary>
/// A geographic position on the WGS84 datum, expressed as signed decimal degrees. Positive
/// <see cref="Latitude"/> is north, positive <see cref="Longitude"/> is east. This is the neutral
/// in-memory representation every parser/formatter and the geodesy routines round-trip through.
/// </summary>
public readonly record struct GeoCoordinate(double Latitude, double Longitude)
{
    /// <summary><see langword="true"/> when both components are finite and within the geographic bounds:
    /// latitude in <c>[-90, 90]</c> and longitude in <c>[-180, 180]</c>.</summary>
    public bool IsValid =>
        double.IsFinite(Latitude) && double.IsFinite(Longitude) &&
        Latitude is >= -90.0 and <= 90.0 &&
        Longitude is >= -180.0 and <= 180.0;
}
