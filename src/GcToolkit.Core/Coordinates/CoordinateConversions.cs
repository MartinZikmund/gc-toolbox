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
    /// Seconds, UTM, MGRS, USNG, Dutch RD and British OSGB grid.</summary>
    public static readonly IReadOnlyList<CoordinateFormat> AllFormats =
    [
        CoordinateFormat.DecimalDegrees,
        CoordinateFormat.DegreesDecimalMinutes,
        CoordinateFormat.DegreesMinutesSeconds,
        CoordinateFormat.Utm,
        CoordinateFormat.Mgrs,
        CoordinateFormat.Usng,
        CoordinateFormat.DutchRd,
        CoordinateFormat.BritishGrid,
    ];

    /// <summary>Formats <paramref name="coordinate"/> (WGS84) in every notation in <see cref="AllFormats"/>.</summary>
    public static IReadOnlyList<CoordinateFormatResult> ToAllFormats(GeoCoordinate coordinate)
        => [.. AllFormats.Select(format => new CoordinateFormatResult(format, CoordinateFormatter.Format(coordinate, format)))];

    /// <summary>Formats <paramref name="wgs84"/> in every notation, with the angular notations expressed on
    /// <paramref name="outputDatum"/> (the grids keep their intrinsic datum — see
    /// <see cref="CoordinateFormatter.Format(GeoCoordinate, CoordinateFormat, Datum)"/>).</summary>
    public static IReadOnlyList<CoordinateFormatResult> ToAllFormats(GeoCoordinate wgs84, Datum outputDatum)
        => [.. AllFormats.Select(format => new CoordinateFormatResult(format, CoordinateFormatter.Format(wgs84, format, outputDatum)))];
}
