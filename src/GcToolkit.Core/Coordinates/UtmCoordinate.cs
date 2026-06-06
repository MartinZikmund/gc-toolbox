using System.Globalization;

namespace GcToolkit.Core.Coordinates;

/// <summary>
/// A position on the Universal Transverse Mercator grid: a 6°-wide <see cref="ZoneNumber"/> (1–60),
/// the MGRS latitude-band letter <see cref="ZoneBand"/> (C–X, omitting I and O), and the metric
/// <see cref="Easting"/>/<see cref="Northing"/> within that zone.
/// </summary>
public readonly record struct UtmCoordinate(int ZoneNumber, char ZoneBand, double Easting, double Northing)
{
    /// <summary><see langword="true"/> if the band lies in the northern hemisphere (bands N–X).</summary>
    public bool IsNorthern => char.ToUpperInvariant(ZoneBand) >= 'N';

    /// <summary>The geocaching grid notation, e.g. <c>33U 705083 5644673</c> (easting/northing rounded to the metre).</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{ZoneNumber}{char.ToUpperInvariant(ZoneBand)} {Math.Round(Easting)} {Math.Round(Northing)}");
}
