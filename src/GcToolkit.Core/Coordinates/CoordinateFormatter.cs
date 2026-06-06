using System.Globalization;

namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Renders a <see cref="GeoCoordinate"/> in any supported notation. The angular formats follow the
/// geocaching convention: a leading hemisphere letter, the longitude degrees padded to three digits
/// (latitude to two), and the <c>° ' "</c> symbols.
/// </summary>
public static class CoordinateFormatter
{
    /// <summary>Formats <paramref name="c"/> (WGS84) in the requested <paramref name="format"/>.</summary>
    public static string Format(GeoCoordinate c, CoordinateFormat format) => format switch
    {
        CoordinateFormat.DecimalDegrees => FormatDecimalDegrees(c),
        CoordinateFormat.DegreesDecimalMinutes => FormatDegreesDecimalMinutes(c),
        CoordinateFormat.DegreesMinutesSeconds => FormatDegreesMinutesSeconds(c),
        CoordinateFormat.Utm => Utm.FromLatLon(c).ToString(),
        CoordinateFormat.Mgrs => Mgrs.FromLatLon(c),
        CoordinateFormat.Usng => Usng.FromLatLon(c),
        CoordinateFormat.DutchRd => DutchRd.FromLatLon(c).ToString(),
        CoordinateFormat.BritishGrid => BritishGrid.FromLatLon(c),
        _ => FormatDecimalDegrees(c),
    };

    /// <summary>Formats the WGS84 coordinate <paramref name="wgs84"/> as seen on <paramref name="outputDatum"/>.
    /// The angular notations (DD/DDM/DMS) are shifted to the datum; the grid systems keep their own
    /// intrinsic datum (UTM/MGRS/USNG are WGS84, OSGB is OSGB36, RD is Bessel/Amersfoort), so they are
    /// rendered from the WGS84 hub regardless of the selected output datum.</summary>
    public static string Format(GeoCoordinate wgs84, CoordinateFormat format, Datum outputDatum)
    {
        var angular = format is CoordinateFormat.DecimalDegrees
            or CoordinateFormat.DegreesDecimalMinutes
            or CoordinateFormat.DegreesMinutesSeconds;

        var c = angular ? DatumTransform.FromWgs84(wgs84, outputDatum) : wgs84;
        return Format(c, format);
    }

    private static string FormatDecimalDegrees(GeoCoordinate c)
    {
        var lat = FormatDecimalComponent(Math.Abs(c.Latitude), 2, NorthSouth(c.Latitude));
        var lon = FormatDecimalComponent(Math.Abs(c.Longitude), 3, EastWest(c.Longitude));
        return $"{lat} {lon}";
    }

    private static string FormatDecimalComponent(double absDegrees, int integerDigits, char hemisphere)
    {
        var text = absDegrees.ToString("F6", CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        var intPart = text[..dot].PadLeft(integerDigits, '0');
        return $"{hemisphere} {intPart}{text[dot..]}°";
    }

    private static string FormatDegreesDecimalMinutes(GeoCoordinate c)
    {
        var lat = FormatDdmComponent(Math.Abs(c.Latitude), 2, NorthSouth(c.Latitude));
        var lon = FormatDdmComponent(Math.Abs(c.Longitude), 3, EastWest(c.Longitude));
        return $"{lat} {lon}";
    }

    private static string FormatDdmComponent(double absDegrees, int integerDigits, char hemisphere)
    {
        var degrees = (int)absDegrees;
        var minutes = (absDegrees - degrees) * 60.0;

        // Guard the carry when rounding minutes to 3 dp pushes them to 60.000.
        if (Math.Round(minutes, 3) >= 60.0)
        {
            degrees += 1;
            minutes = 0.0;
        }

        var degText = degrees.ToString(CultureInfo.InvariantCulture).PadLeft(integerDigits, '0');
        var minText = minutes.ToString("F3", CultureInfo.InvariantCulture);
        if (minutes < 10.0)
        {
            minText = "0" + minText; // pad minutes to two integer digits, e.g. 09.123
        }

        return $"{hemisphere} {degText}° {minText}'";
    }

    private static string FormatDegreesMinutesSeconds(GeoCoordinate c)
    {
        var lat = FormatDmsComponent(Math.Abs(c.Latitude), 2, NorthSouth(c.Latitude));
        var lon = FormatDmsComponent(Math.Abs(c.Longitude), 3, EastWest(c.Longitude));
        return $"{lat} {lon}";
    }

    private static string FormatDmsComponent(double absDegrees, int integerDigits, char hemisphere)
    {
        var degrees = (int)absDegrees;
        var minutesFull = (absDegrees - degrees) * 60.0;
        var minutes = (int)minutesFull;
        var seconds = (minutesFull - minutes) * 60.0;

        if (Math.Round(seconds, 2) >= 60.0)
        {
            seconds = 0.0;
            minutes += 1;
        }

        if (minutes >= 60)
        {
            minutes = 0;
            degrees += 1;
        }

        var degText = degrees.ToString(CultureInfo.InvariantCulture).PadLeft(integerDigits, '0');
        var minText = minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
        var secText = seconds.ToString("F2", CultureInfo.InvariantCulture);
        if (seconds < 10.0)
        {
            secText = "0" + secText;
        }

        return $"{hemisphere} {degText}° {minText}' {secText}\"";
    }

    private static char NorthSouth(double latitude) => latitude < 0.0 ? 'S' : 'N';

    private static char EastWest(double longitude) => longitude < 0.0 ? 'W' : 'E';
}
