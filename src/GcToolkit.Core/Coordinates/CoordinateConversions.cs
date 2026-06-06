namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Renders one point in every supported notation at once — the heart of the coordinate-conversion
/// tool. The angular/grid math all lives in <see cref="CoordinateFormatter"/> (and below it
/// <see cref="Utm"/>/<see cref="Mgrs"/>); this just assembles the formats in a stable order so the
/// caller can show them as a list with per-row copy. Keeping it here keeps the ViewModel thin.
/// </summary>
public static class CoordinateConversions
{
    /// <summary>The notations the conversion tool emits, in display order (declaration order of
    /// <see cref="CoordinateFormat"/>): Decimal Degrees, Degrees Decimal Minutes, Degrees Minutes
    /// Seconds, UTM and MGRS.</summary>
    public static readonly IReadOnlyList<CoordinateFormat> AllFormats =
    [
        CoordinateFormat.DecimalDegrees,
        CoordinateFormat.DegreesDecimalMinutes,
        CoordinateFormat.DegreesMinutesSeconds,
        CoordinateFormat.Utm,
        CoordinateFormat.Mgrs,
    ];

    /// <summary>Formats <paramref name="coordinate"/> in every notation in <see cref="AllFormats"/>.</summary>
    public static IReadOnlyList<CoordinateFormatResult> ToAllFormats(GeoCoordinate coordinate)
        => [.. AllFormats.Select(format => new CoordinateFormatResult(format, CoordinateFormatter.Format(coordinate, format)))];
}
