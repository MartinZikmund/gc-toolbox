namespace GcToolkit.Core.Coordinates;

/// <summary>
/// The result of projecting a coordinate: the destination point plus its rendering in the three
/// angular notations. Keeps the projection ViewModel thin — the heavy math is in <see cref="Geodesy"/>
/// and <see cref="CoordinateFormatter"/>.
/// </summary>
/// <param name="Destination">The projected coordinate.</param>
/// <param name="DecimalDegrees">The destination as decimal degrees.</param>
/// <param name="DegreesDecimalMinutes">The destination as degrees + decimal minutes (geocaching default).</param>
/// <param name="DegreesMinutesSeconds">The destination as degrees, minutes and seconds.</param>
public readonly record struct ProjectionResult(
    GeoCoordinate Destination,
    string DecimalDegrees,
    string DegreesDecimalMinutes,
    string DegreesMinutesSeconds);

/// <summary>Projects a start coordinate along a bearing by a distance, rendering the result in every
/// angular format at once (the "beyond parity" multi-format output).</summary>
public static class CoordinateProjection
{
    /// <summary>Projects <paramref name="start"/> by <paramref name="distanceMeters"/> along
    /// <paramref name="bearingDegrees"/> (measured clockwise from North) and formats the destination.</summary>
    public static ProjectionResult Project(GeoCoordinate start, double distanceMeters, double bearingDegrees)
    {
        var destination = Geodesy.Destination(start, distanceMeters, bearingDegrees);
        return new ProjectionResult(
            destination,
            CoordinateFormatter.Format(destination, CoordinateFormat.DecimalDegrees),
            CoordinateFormatter.Format(destination, CoordinateFormat.DegreesDecimalMinutes),
            CoordinateFormatter.Format(destination, CoordinateFormat.DegreesMinutesSeconds));
    }
}
