namespace GcToolkit.Core.Coordinates;

/// <summary>
/// US National Grid. On the WGS84 datum the USNG is defined to be identical to MGRS — same zone,
/// 100 km-square lettering, and easting/northing digits — so this is a thin alias over
/// <see cref="Mgrs"/> that produces the conventionally spaced string (e.g. <c>18S UJ 23408 06479</c>)
/// and parses it back. (USNG == MGRS(WGS84).)
/// </summary>
public static class Usng
{
    /// <summary>Formats a coordinate as a spaced USNG string, e.g. <c>18S UJ 23408 06479</c>.</summary>
    /// <param name="digits">Easting/northing digits per axis (1–5); 5 gives 1 m precision.</param>
    public static string FromLatLon(GeoCoordinate c, int digits = 5) => Mgrs.FromLatLon(c, digits);

    /// <summary>Parses a USNG string (spaced or compact) back to WGS84 latitude/longitude.</summary>
    public static bool TryParse(string? text, out GeoCoordinate c) => Mgrs.TryParse(text, out c);
}
