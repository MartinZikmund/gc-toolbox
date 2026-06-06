using System.Globalization;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Converts between WGS84 latitude/longitude and the Universal Transverse Mercator grid. The
/// projection uses the Karney–Krüger series (accurate to a few nanometres within a zone), the
/// standard UTM scale factor <c>k0 = 0.9996</c>, and the Norway/Svalbard zone exceptions.
/// </summary>
public static partial class Utm
{
    private const double K0 = 0.9996;
    private const double FalseEasting = 500_000.0;
    private const double FalseNorthing = 10_000_000.0; // applied in the southern hemisphere

    // MGRS latitude-band letters for 8° bands from 80°S (C) to 84°N (X). I and O are omitted.
    private const string LatitudeBands = "CDEFGHJKLMNPQRSTUVWXX";

    /// <summary>Projects a geographic coordinate onto its UTM zone.</summary>
    public static UtmCoordinate FromLatLon(GeoCoordinate c)
    {
        var lat = Math.Clamp(c.Latitude, -80.0, 84.0);
        var zone = ZoneNumber(lat, c.Longitude);
        var band = LatitudeBand(lat);
        var lon0 = CentralMeridianDegrees(zone);

        var (easting, northing) = ProjectForward(c.Latitude, c.Longitude, lon0);
        if (c.Latitude < 0.0)
        {
            northing += FalseNorthing;
        }

        return new UtmCoordinate(zone, band, easting, northing);
    }

    /// <summary>Unprojects a UTM coordinate back to WGS84 latitude/longitude.</summary>
    public static GeoCoordinate ToLatLon(UtmCoordinate u)
    {
        var northing = u.Northing;
        if (!u.IsNorthern)
        {
            northing -= FalseNorthing;
        }

        var lon0 = CentralMeridianDegrees(u.ZoneNumber);
        return ProjectInverse(u.Easting, northing, lon0);
    }

    /// <summary>Parses a UTM grid notation such as <c>33U 705083 5644673</c> or <c>33 U 705083 5644673</c>.</summary>
    public static bool TryParse(string? text, out UtmCoordinate u)
    {
        u = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = UtmPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var zone = int.Parse(match.Groups["zone"].Value, CultureInfo.InvariantCulture);
        if (zone is < 1 or > 60)
        {
            return false;
        }

        var band = char.ToUpperInvariant(match.Groups["band"].Value[0]);
        if (LatitudeBands.IndexOf(band) < 0)
        {
            return false;
        }

        var easting = double.Parse(match.Groups["easting"].Value, CultureInfo.InvariantCulture);
        var northing = double.Parse(match.Groups["northing"].Value, CultureInfo.InvariantCulture);
        u = new UtmCoordinate(zone, band, easting, northing);
        return true;
    }

    /// <summary>The 1–60 UTM zone for a position, honouring the Norway and Svalbard exceptions.</summary>
    internal static int ZoneNumber(double lat, double lon)
    {
        var zone = (int)Math.Floor((NormalizeLongitude(lon) + 180.0) / 6.0) + 1;

        // South-west Norway: zone 32 is widened westward over 56°–64°N.
        if (lat is >= 56.0 and < 64.0 && lon is >= 3.0 and < 12.0)
        {
            return 32;
        }

        // Svalbard: zones 32, 34, 36 are dropped; the odd zones either side widen to cover the gap.
        if (lat is >= 72.0 and < 84.0)
        {
            if (lon is >= 0.0 and < 9.0)
            {
                return 31;
            }

            if (lon is >= 9.0 and < 21.0)
            {
                return 33;
            }

            if (lon is >= 21.0 and < 33.0)
            {
                return 35;
            }

            if (lon is >= 33.0 and < 42.0)
            {
                return 37;
            }
        }

        return Math.Clamp(zone, 1, 60);
    }

    /// <summary>The central-meridian longitude (degrees) of a UTM zone.</summary>
    internal static double CentralMeridianDegrees(int zone) => zone * 6.0 - 183.0;

    /// <summary>The MGRS latitude-band letter for a latitude in <c>[-80, 84]</c>.</summary>
    internal static char LatitudeBand(double lat)
    {
        if (lat >= 84.0)
        {
            return 'X';
        }

        var index = (int)Math.Floor((Math.Clamp(lat, -80.0, 83.9999) + 80.0) / 8.0);
        return LatitudeBands[Math.Clamp(index, 0, LatitudeBands.Length - 1)];
    }

    private static double NormalizeLongitude(double lon) => ((lon + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;

    // ---- Karney–Krüger transverse Mercator (forward) ----

    private static (double Easting, double Northing) ProjectForward(double latDeg, double lonDeg, double lon0Deg)
    {
        var f = Wgs84.F;
        var a = Wgs84.A;
        var n = f / (2.0 - f);
        var n2 = n * n;
        var n3 = n2 * n;
        var n4 = n3 * n;
        var n5 = n4 * n;
        var n6 = n5 * n;

        var phi = latDeg * Math.PI / 180.0;
        var lambda = (lonDeg - lon0Deg) * Math.PI / 180.0;

        var e = Math.Sqrt(f * (2.0 - f));
        var t = Math.Sinh(Atanh(Math.Sin(phi)) - e * Atanh(e * Math.Sin(phi)));
        var xiP = Math.Atan2(t, Math.Cos(lambda));
        var etaP = Asinh(Math.Sin(lambda) / Math.Sqrt(t * t + Math.Cos(lambda) * Math.Cos(lambda)));

        var bigA = a / (1.0 + n) * (1.0 + n2 / 4.0 + n4 / 64.0 + n6 / 256.0);

        double[] alpha =
        [
            1.0 / 2.0 * n - 2.0 / 3.0 * n2 + 5.0 / 16.0 * n3 + 41.0 / 180.0 * n4 - 127.0 / 288.0 * n5 + 7891.0 / 37800.0 * n6,
            13.0 / 48.0 * n2 - 3.0 / 5.0 * n3 + 557.0 / 1440.0 * n4 + 281.0 / 630.0 * n5 - 1983433.0 / 1935360.0 * n6,
            61.0 / 240.0 * n3 - 103.0 / 140.0 * n4 + 15061.0 / 26880.0 * n5 + 167603.0 / 181440.0 * n6,
            49561.0 / 161280.0 * n4 - 179.0 / 168.0 * n5 + 6601661.0 / 7257600.0 * n6,
            34729.0 / 80640.0 * n5 - 3418889.0 / 1995840.0 * n6,
            212378941.0 / 319334400.0 * n6,
        ];

        var xi = xiP;
        var eta = etaP;
        for (var j = 1; j <= 6; j++)
        {
            xi += alpha[j - 1] * Math.Sin(2.0 * j * xiP) * Math.Cosh(2.0 * j * etaP);
            eta += alpha[j - 1] * Math.Cos(2.0 * j * xiP) * Math.Sinh(2.0 * j * etaP);
        }

        var easting = K0 * bigA * eta + FalseEasting;
        var northing = K0 * bigA * xi;
        return (easting, northing);
    }

    // ---- Karney–Krüger transverse Mercator (inverse) ----

    private static GeoCoordinate ProjectInverse(double easting, double northing, double lon0Deg)
    {
        var f = Wgs84.F;
        var a = Wgs84.A;
        var n = f / (2.0 - f);
        var n2 = n * n;
        var n3 = n2 * n;
        var n4 = n3 * n;
        var n5 = n4 * n;
        var n6 = n5 * n;

        var bigA = a / (1.0 + n) * (1.0 + n2 / 4.0 + n4 / 64.0 + n6 / 256.0);

        var xi = northing / (K0 * bigA);
        var eta = (easting - FalseEasting) / (K0 * bigA);

        double[] beta =
        [
            1.0 / 2.0 * n - 2.0 / 3.0 * n2 + 37.0 / 96.0 * n3 - 1.0 / 360.0 * n4 - 81.0 / 512.0 * n5 + 96199.0 / 604800.0 * n6,
            1.0 / 48.0 * n2 + 1.0 / 15.0 * n3 - 437.0 / 1440.0 * n4 + 46.0 / 105.0 * n5 - 1118711.0 / 3870720.0 * n6,
            17.0 / 480.0 * n3 - 37.0 / 840.0 * n4 - 209.0 / 4480.0 * n5 + 5569.0 / 90720.0 * n6,
            4397.0 / 161280.0 * n4 - 11.0 / 504.0 * n5 - 830251.0 / 7257600.0 * n6,
            4583.0 / 161280.0 * n5 - 108847.0 / 3991680.0 * n6,
            20648693.0 / 638668800.0 * n6,
        ];

        var xiP = xi;
        var etaP = eta;
        for (var j = 1; j <= 6; j++)
        {
            xiP -= beta[j - 1] * Math.Sin(2.0 * j * xi) * Math.Cosh(2.0 * j * eta);
            etaP -= beta[j - 1] * Math.Cos(2.0 * j * xi) * Math.Sinh(2.0 * j * eta);
        }

        var e = Math.Sqrt(f * (2.0 - f));
        var chi = Math.Asin(Math.Sin(xiP) / Math.Cosh(etaP));

        // Geographic latitude from the conformal latitude chi via Karney's direct series.
        var phi = chi + ConformalToGeographic(e, chi);

        var lambda = Math.Atan2(Math.Sinh(etaP), Math.Cos(xiP));
        var latDeg = phi * 180.0 / Math.PI;
        var lonDeg = lon0Deg + lambda * 180.0 / Math.PI;
        return new GeoCoordinate(latDeg, lonDeg);
    }

    /// <summary>Karney's direct series for the correction <c>phi - chi</c> that maps a conformal latitude
    /// <paramref name="chi"/> to the geographic latitude (argument is the conformal latitude, single pass).</summary>
    private static double ConformalToGeographic(double e, double chi)
    {
        var sin2 = Math.Sin(2.0 * chi);
        var sin4 = Math.Sin(4.0 * chi);
        var sin6 = Math.Sin(6.0 * chi);
        var sin8 = Math.Sin(8.0 * chi);
        var e2 = e * e;
        var e4 = e2 * e2;
        var e6 = e4 * e2;
        var e8 = e6 * e2;

        return (e2 / 2.0 + 5.0 * e4 / 24.0 + e6 / 12.0 + 13.0 * e8 / 360.0) * sin2
             + (7.0 * e4 / 48.0 + 29.0 * e6 / 240.0 + 811.0 * e8 / 11520.0) * sin4
             + (7.0 * e6 / 120.0 + 81.0 * e8 / 1120.0) * sin6
             + (4279.0 * e8 / 161280.0) * sin8;
    }

    private static double Atanh(double x) => 0.5 * Math.Log((1.0 + x) / (1.0 - x));

    private static double Asinh(double x) => Math.Log(x + Math.Sqrt(x * x + 1.0));

    [GeneratedRegex(@"^(?<zone>\d{1,2})\s*(?<band>[C-HJ-NP-Xc-hj-np-x])\s+(?<easting>\d+(?:\.\d+)?)\s+(?<northing>\d+(?:\.\d+)?)$")]
    private static partial Regex UtmPattern();
}
