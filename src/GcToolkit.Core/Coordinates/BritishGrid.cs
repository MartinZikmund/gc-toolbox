using System.Globalization;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Converts between WGS84 latitude/longitude and the British OSGB36 National Grid ("OGB-7").
/// WGS84 is shifted to OSGB36 with the published Helmert 7-parameter transform, projected onto the
/// Airy 1830 Transverse Mercator (the OS National Grid), and expressed as a two-letter grid
/// reference (e.g. <c>TG 51409 13177</c>). Helmert-based accuracy is a few metres — matching
/// geocachingtoolbox.com (this is not OSTN15).
/// Verified against the Ordnance Survey worked example (Caister): OSGB36 52.6575703 N, 1.7179215 E ->
/// E 651409, N 313177 -> "TG 51409 13177"; the WGS84 equivalent ~52.65798 N, 1.71605 E reaches the
/// same reference through the datum shift.
/// Sources: OS "A guide to coordinate systems in Great Britain"; github.com/chrisveness/geodesy
/// (osgridref.js, latlon-ellipsoidal-datum.js).
/// </summary>
public static partial class BritishGrid
{
    // Airy 1830 ellipsoid (the OSGB36 reference ellipsoid).
    private static readonly double AiryA = Ellipsoids.Airy1830.A;
    private static readonly double AiryB = Ellipsoids.Airy1830.B;

    // National Grid Transverse Mercator parameters.
    private const double Lat0 = 49.0;          // true origin latitude (49°N)
    private const double Lon0 = -2.0;          // true origin longitude (2°W)
    private const double F0 = 0.9996012717;    // scale factor on the central meridian
    private const double E0 = 400_000.0;       // easting of true origin
    private const double N0 = -100_000.0;      // northing of true origin

    // National Grid 5x5 letter scheme (A..Z without I), row-major from the bottom-left.
    private const string GridLetters = "ABCDEFGHJKLMNOPQRSTUVWXYZ";

    /// <summary>Formats a WGS84 coordinate as a two-letter National Grid reference, e.g. <c>TG 51409 13177</c>.</summary>
    /// <param name="digits">Easting/northing digits per axis (1–5); 5 gives 1 m precision.</param>
    public static string FromLatLon(GeoCoordinate c, int digits = 5)
    {
        var en = ToEastingNorthing(c);
        return ToGridReference(en.Easting, en.Northing, digits);
    }

    /// <summary>Projects a WGS84 coordinate onto OSGB36 National Grid easting/northing (metres).</summary>
    public static (double Easting, double Northing) ToEastingNorthing(GeoCoordinate c)
    {
        var osgb36 = DatumTransform.FromWgs84(c, DatumRegistry.Osgb36);
        return ProjectForward(osgb36.Latitude, osgb36.Longitude);
    }

    /// <summary>Parses a National Grid reference (spaced or compact) back to WGS84 latitude/longitude
    /// (the centre of the addressed cell).</summary>
    public static bool TryParse(string? text, out GeoCoordinate c)
    {
        c = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!TryParseGridReference(text, out var easting, out var northing))
        {
            return false;
        }

        var osgb36 = ProjectInverse(easting, northing);
        c = DatumTransform.ToWgs84(osgb36, DatumRegistry.Osgb36);
        return true;
    }

    /// <summary>Parses the all-numeric National Grid form geocachingtoolbox.com also accepts — absolute
    /// easting/northing in metres, e.g. <c>651409 313177</c>. Guarded to the grid's extent and to the
    /// Great Britain bounding box so an arbitrary numeric pair is not read as a grid reference.</summary>
    public static bool TryParseNumeric(string? text, out GeoCoordinate c)
    {
        c = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = NumericPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var easting = double.Parse(match.Groups["e"].Value, CultureInfo.InvariantCulture);
        var northing = double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
        if (easting is < 0.0 or > 700_000.0 || northing is < 0.0 or > 1_300_000.0)
        {
            return false;
        }

        var candidate = DatumTransform.ToWgs84(ProjectInverse(easting, northing), DatumRegistry.Osgb36);
        if (candidate.Latitude is < 49.0 or > 61.5 || candidate.Longitude is < -9.0 or > 2.5)
        {
            return false;
        }

        c = candidate;
        return true;
    }

    // ---- National Grid two-letter reference ----

    private static string ToGridReference(double easting, double northing, int digits)
    {
        digits = Math.Clamp(digits, 1, 5);

        // The grid covers the 500 km squares whose SW corner is 1000 km west / 500 km south of the
        // true origin; outside that range there is no letter pair.
        var e100k = (int)Math.Floor(easting / 100_000.0);
        var n100k = (int)Math.Floor(northing / 100_000.0);
        if (e100k is < 0 or > 6 || n100k is < 0 or > 12)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Math.Round(easting)} {Math.Round(northing)}");
        }

        var first = GridLetters[(19 - n100k) - (19 - n100k) % 5 + (e100k + 10) / 5];
        var second = GridLetters[((19 - n100k) * 5) % 25 + e100k % 5];

        var divisor = Math.Pow(10, 5 - digits);
        var e = (int)Math.Floor(Math.Round(easting % 100_000.0, 3) / divisor);
        var n = (int)Math.Floor(Math.Round(northing % 100_000.0, 3) / divisor);

        return string.Create(CultureInfo.InvariantCulture,
            $"{first}{second} {e.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0')} {n.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0')}");
    }

    private static bool TryParseGridReference(string text, out double easting, out double northing)
    {
        easting = 0.0;
        northing = 0.0;

        var compact = Regex.Replace(text.Trim(), @"\s+", string.Empty).ToUpperInvariant();
        var match = GridPattern().Match(compact);
        if (!match.Success)
        {
            return false;
        }

        var first = match.Groups["l1"].Value[0];
        var second = match.Groups["l2"].Value[0];
        var i1 = GridLetters.IndexOf(first);
        var i2 = GridLetters.IndexOf(second);
        if (i1 < 0 || i2 < 0)
        {
            return false;
        }

        // 500 km square from the first letter, 100 km square from the second.
        var e500k = (i1 % 5) * 500_000.0 - 1_000_000.0;
        var n500k = (4 - i1 / 5) * 500_000.0 - 500_000.0;
        var e100k = (i2 % 5) * 100_000.0;
        var n100k = (4 - i2 / 5) * 100_000.0;

        var digitsText = match.Groups["digits"].Value;
        if (digitsText.Length % 2 != 0)
        {
            return false;
        }

        var per = digitsText.Length / 2;
        var scale = Math.Pow(10, 5 - per);
        var half = per == 0 ? 0.0 : scale / 2.0; // address the cell centre
        var eDigits = per == 0 ? 0.0 : double.Parse(digitsText[..per], CultureInfo.InvariantCulture) * scale + half;
        var nDigits = per == 0 ? 0.0 : double.Parse(digitsText[per..], CultureInfo.InvariantCulture) * scale + half;

        easting = e500k + e100k + eDigits;
        northing = n500k + n100k + nDigits;
        return true;
    }

    // ---- Airy 1830 Transverse Mercator (OS National Grid) ----
    // Datum shift WGS84 <-> OSGB36 is delegated to DatumTransform + DatumRegistry.Osgb36.

    private static (double Easting, double Northing) ProjectForward(double latDeg, double lonDeg)
    {
        var a = AiryA;
        var b = AiryB;
        var e2 = (a * a - b * b) / (a * a);
        var n = (a - b) / (a + b);

        var phi = Rad(latDeg);
        var lambda = Rad(lonDeg);
        var phi0 = Rad(Lat0);
        var lambda0 = Rad(Lon0);

        var sinPhi = Math.Sin(phi);
        var cosPhi = Math.Cos(phi);
        var tanPhi = Math.Tan(phi);

        var nu = a * F0 / Math.Sqrt(1.0 - e2 * sinPhi * sinPhi);
        var rho = a * F0 * (1.0 - e2) / Math.Pow(1.0 - e2 * sinPhi * sinPhi, 1.5);
        var eta2 = nu / rho - 1.0;

        var m = MeridionalArc(phi, phi0, b, n);

        var cos3 = cosPhi * cosPhi * cosPhi;
        var cos5 = cos3 * cosPhi * cosPhi;
        var tan2 = tanPhi * tanPhi;
        var tan4 = tan2 * tan2;

        var bigI = m + N0;
        var bigII = nu / 2.0 * sinPhi * cosPhi;
        var bigIII = nu / 24.0 * sinPhi * cos3 * (5.0 - tan2 + 9.0 * eta2);
        var bigIIIA = nu / 720.0 * sinPhi * cos5 * (61.0 - 58.0 * tan2 + tan4);
        var bigIV = nu * cosPhi;
        var bigV = nu / 6.0 * cos3 * (nu / rho - tan2);
        var bigVI = nu / 120.0 * cos5 * (5.0 - 18.0 * tan2 + tan4 + 14.0 * eta2 - 58.0 * tan2 * eta2);

        var dLambda = lambda - lambda0;
        var dl2 = dLambda * dLambda;

        var northing = bigI + bigII * dl2 + bigIII * dl2 * dl2 + bigIIIA * dl2 * dl2 * dl2;
        var easting = E0 + bigIV * dLambda + bigV * dLambda * dl2 + bigVI * dLambda * dl2 * dl2;
        return (easting, northing);
    }

    private static GeoCoordinate ProjectInverse(double easting, double northing)
    {
        var a = AiryA;
        var b = AiryB;
        var e2 = (a * a - b * b) / (a * a);
        var n = (a - b) / (a + b);
        var phi0 = Rad(Lat0);
        var lambda0 = Rad(Lon0);

        // Iterate latitude so the meridional arc matches the northing.
        var phi = (northing - N0) / (a * F0) + phi0;
        double m;
        do
        {
            m = MeridionalArc(phi, phi0, b, n);
            phi += (northing - N0 - m) / (a * F0);
        }
        while (Math.Abs(northing - N0 - m) >= 0.00001);

        var sinPhi = Math.Sin(phi);
        var cosPhi = Math.Cos(phi);
        var tanPhi = Math.Tan(phi);

        var nu = a * F0 / Math.Sqrt(1.0 - e2 * sinPhi * sinPhi);
        var rho = a * F0 * (1.0 - e2) / Math.Pow(1.0 - e2 * sinPhi * sinPhi, 1.5);
        var eta2 = nu / rho - 1.0;

        var tan2 = tanPhi * tanPhi;
        var tan4 = tan2 * tan2;
        var tan6 = tan4 * tan2;
        var secPhi = 1.0 / cosPhi;

        var nu3 = nu * nu * nu;
        var nu5 = nu3 * nu * nu;
        var nu7 = nu5 * nu * nu;

        var bigVII = tanPhi / (2.0 * rho * nu);
        var bigVIII = tanPhi / (24.0 * rho * nu3) * (5.0 + 3.0 * tan2 + eta2 - 9.0 * tan2 * eta2);
        var bigIX = tanPhi / (720.0 * rho * nu5) * (61.0 + 90.0 * tan2 + 45.0 * tan4);
        var bigX = secPhi / nu;
        var bigXI = secPhi / (6.0 * nu3) * (nu / rho + 2.0 * tan2);
        var bigXII = secPhi / (120.0 * nu5) * (5.0 + 28.0 * tan2 + 24.0 * tan4);
        var bigXIIA = secPhi / (5040.0 * nu7) * (61.0 + 662.0 * tan2 + 1320.0 * tan4 + 720.0 * tan6);

        var dE = easting - E0;
        var dE2 = dE * dE;

        var latitude = phi - bigVII * dE2 + bigVIII * dE2 * dE2 - bigIX * dE2 * dE2 * dE2;
        var longitude = lambda0 + bigX * dE - bigXI * dE * dE2 + bigXII * dE * dE2 * dE2 - bigXIIA * dE * dE2 * dE2 * dE2;
        return new GeoCoordinate(Deg(latitude), Deg(longitude));
    }

    /// <summary>The meridional arc length (developed M) for the OS series.</summary>
    private static double MeridionalArc(double phi, double phi0, double b, double n)
    {
        var n2 = n * n;
        var n3 = n2 * n;
        var dPhi = phi - phi0;
        var sPhi = phi + phi0;

        return b * F0 * (
            (1.0 + n + 5.0 / 4.0 * n2 + 5.0 / 4.0 * n3) * dPhi
            - (3.0 * n + 3.0 * n2 + 21.0 / 8.0 * n3) * Math.Sin(dPhi) * Math.Cos(sPhi)
            + (15.0 / 8.0 * n2 + 15.0 / 8.0 * n3) * Math.Sin(2.0 * dPhi) * Math.Cos(2.0 * sPhi)
            - 35.0 / 24.0 * n3 * Math.Sin(3.0 * dPhi) * Math.Cos(3.0 * sPhi));
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180.0;

    private static double Deg(double radians) => radians * 180.0 / Math.PI;

    // At least one digit per axis — a bare letter pair (or a stray word like "ab") is not a grid reference.
    [GeneratedRegex(@"^(?<l1>[A-HJ-Z])(?<l2>[A-HJ-Z])(?<digits>\d{2,10})$")]
    private static partial Regex GridPattern();

    // Absolute easting/northing in metres, space- or comma-separated (5–6 and 5–7 digits).
    [GeneratedRegex(@"^(?<e>\d{5,6})\s*[,\s]\s*(?<n>\d{5,7})$")]
    private static partial Regex NumericPattern();
}
