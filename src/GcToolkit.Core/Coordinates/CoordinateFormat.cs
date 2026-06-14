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
}
