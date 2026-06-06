namespace GcToolkit.Core.Coordinates;

/// <summary>The textual coordinate notations this library can parse and format.</summary>
public enum CoordinateFormat
{
    /// <summary>Decimal degrees, e.g. <c>N 49.205750° E 016.576117°</c>.</summary>
    DecimalDegrees,

    /// <summary>Degrees and decimal minutes (the geocaching standard), e.g. <c>N 49° 12.345' E 016° 34.567'</c>.</summary>
    DegreesDecimalMinutes,

    /// <summary>Degrees, minutes and decimal seconds, e.g. <c>N 49° 12' 20.70" E 016° 34' 34.02"</c>.</summary>
    DegreesMinutesSeconds,

    /// <summary>Universal Transverse Mercator grid, e.g. <c>33U 705083 5644673</c>.</summary>
    Utm,

    /// <summary>Military Grid Reference System, e.g. <c>33U VR 05083 44673</c>.</summary>
    Mgrs,

    /// <summary>US National Grid, e.g. <c>18S UJ 23408 06479</c>. On WGS84 this is identical to MGRS.</summary>
    Usng,

    /// <summary>Dutch RD (Rijksdriehoek / "RD/AME-7") easting/northing in metres, e.g. <c>155000 463000</c>.</summary>
    DutchRd,

    /// <summary>British OSGB36 National Grid ("OGB-7"), e.g. <c>TG 51409 13177</c>.</summary>
    BritishGrid,
}
