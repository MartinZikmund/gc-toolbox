using System.Globalization;

namespace GcToolkit.Core.Coordinates;

/// <summary>
/// A position on the Dutch RD (Rijksdriehoek) grid: metric <see cref="Easting"/>/<see cref="Northing"/>
/// in the Amersfoort-origin oblique-stereographic projection. The origin (Amersfoort) sits at
/// <c>(155000, 463000)</c>.
/// </summary>
public readonly record struct RdCoordinate(double Easting, double Northing)
{
    /// <summary>The geocaching grid notation, e.g. <c>155000 463000</c> (easting/northing rounded to the metre).</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Math.Round(Easting)} {Math.Round(Northing)}");
}
