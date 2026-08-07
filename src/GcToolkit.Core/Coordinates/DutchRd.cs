using System.Globalization;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Converts between WGS84 latitude/longitude and the Dutch RD (Rijksdriehoek, "RD/AME-7") grid.
/// Uses the Schreutelkamp &amp; Strang van Hees polynomial approximation of the official RD
/// oblique-stereographic projection (Bessel 1841, Amersfoort origin); it is centimetre-accurate
/// inside the Netherlands. Verified reference points: Amersfoort (52.1551744 N, 5.38720621 E) ->
/// (155000, 463000); central Amsterdam (52.37422 N, 4.89801 E) -> (121687, 487484).
/// Coefficients: F.H. Schreutelkamp &amp; G.L. Strang van Hees, "Benaderingsformules voor de
/// transformatie tussen RD- en WGS84-kaartcoordinaten" (mirrored at
/// github.com/djvanderlaan/rijksdriehoek).
/// </summary>
public static partial class DutchRd
{
    // Amersfoort origin: the projection's false easting/northing and the lat/lon it sits at.
    private const double X0 = 155_000.0;
    private const double Y0 = 463_000.0;
    private const double Phi0 = 52.15517440;
    private const double Lambda0 = 5.38720621;

    // A polynomial term: value * arg1^p * arg2^q.
    private readonly record struct Term(int P, int Q, double Coefficient);

    // WGS84 -> RD. Argument: dPhi = 0.36*(lat - Phi0), dLam = 0.36*(lon - Lambda0). Result in metres.
    private static readonly Term[] _eastingTerms =
    [
        new(0, 1, 190_094.945), new(1, 1, -11_832.228), new(2, 1, -114.221),
        new(0, 3, -32.391), new(1, 0, -0.705), new(3, 1, -2.340),
        new(1, 3, -0.608), new(0, 2, -0.008), new(2, 3, 0.148),
    ];

    private static readonly Term[] _northingTerms =
    [
        new(1, 0, 309_056.544), new(0, 2, 3_638.893), new(2, 0, 73.077),
        new(1, 2, -157.984), new(3, 0, 59.788), new(0, 1, 0.433),
        new(2, 2, -6.439), new(1, 1, -0.032), new(0, 4, 0.092), new(1, 4, -0.054),
    ];

    // RD -> WGS84. Argument: dX = 1e-5*(E - X0), dY = 1e-5*(N - Y0). Result added to Phi0/Lambda0
    // after dividing by 3600 (the coefficients are in arc-seconds).
    private static readonly Term[] _latitudeTerms =
    [
        new(0, 1, 3_235.65389), new(2, 0, -32.58297), new(0, 2, -0.24750),
        new(2, 1, -0.84978), new(0, 3, -0.06550), new(2, 2, -0.01709),
        new(1, 0, -0.00738), new(4, 0, 0.00530), new(2, 3, -0.00039),
        new(4, 1, 0.00033), new(1, 1, -0.00012),
    ];

    private static readonly Term[] _longitudeTerms =
    [
        new(1, 0, 5_260.52916), new(1, 1, 105.94684), new(1, 2, 2.45656),
        new(3, 0, -0.81885), new(1, 3, 0.05594), new(3, 1, -0.05607),
        new(0, 1, 0.01199), new(3, 2, -0.00256), new(1, 4, 0.00128),
        new(0, 2, 0.00022), new(2, 0, -0.00022), new(5, 0, 0.00026),
    ];

    /// <summary>Projects a WGS84 coordinate onto the RD grid.</summary>
    public static RdCoordinate FromLatLon(GeoCoordinate c)
    {
        var dPhi = 0.36 * (c.Latitude - Phi0);
        var dLam = 0.36 * (c.Longitude - Lambda0);

        var easting = X0 + Sum(_eastingTerms, dPhi, dLam);
        var northing = Y0 + Sum(_northingTerms, dPhi, dLam);
        return new RdCoordinate(easting, northing);
    }

    /// <summary>Unprojects an RD coordinate back to WGS84 latitude/longitude.</summary>
    public static GeoCoordinate ToLatLon(RdCoordinate rd)
    {
        var dX = 1e-5 * (rd.Easting - X0);
        var dY = 1e-5 * (rd.Northing - Y0);

        var latitude = Phi0 + Sum(_latitudeTerms, dX, dY) / 3600.0;
        var longitude = Lambda0 + Sum(_longitudeTerms, dX, dY) / 3600.0;
        return new GeoCoordinate(latitude, longitude);
    }

    /// <summary>Parses an RD grid notation such as <c>155000 463000</c> (space- or comma-separated metres).</summary>
    public static bool TryParse(string? text, out RdCoordinate rd)
    {
        rd = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = RdPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var easting = double.Parse(match.Groups["e"].Value, CultureInfo.InvariantCulture);
        var northing = double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);

        // Reject values outside the RD extent so plain lat/lon pairs aren't mistaken for RD.
        if (easting is < -7_000 or > 300_000 || northing is < 289_000 or > 629_000)
        {
            return false;
        }

        rd = new RdCoordinate(easting, northing);
        return true;
    }

    private static double Sum(Term[] terms, double arg1, double arg2)
    {
        var total = 0.0;
        foreach (var term in terms)
        {
            total += term.Coefficient * Math.Pow(arg1, term.P) * Math.Pow(arg2, term.Q);
        }

        return total;
    }

    // Two integers (RD eastings are 5–6 digits, northings 6 digits), optional decimals, space/comma separated.
    [GeneratedRegex(@"^(?<e>-?\d{4,6}(?:\.\d+)?)\s*[,\s]\s*(?<n>\d{5,6}(?:\.\d+)?)$")]
    private static partial Regex RdPattern();
}
