using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Converts between WGS84 latitude/longitude and the Military Grid Reference System. MGRS is UTM
/// with the easting/northing replaced by a two-letter 100 km-square identifier (the WGS84 "AA"
/// lettering scheme) followed by the truncated easting/northing digits.
/// </summary>
public static partial class Mgrs
{
    // Column letters repeat every three zones over A..Z without I or O (set 1: A–H, set 2: J–R, set 3: S–Z).
    private const string ColumnLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    // Row letters cycle through the same 20-letter alphabet (A–V without I, O); even zones start 5 rows up.
    private const string RowLetters = "ABCDEFGHJKLMNPQRSTUV";

    private const double SquareSize = 100_000.0;

    /// <summary>Formats a coordinate as a spaced MGRS string, e.g. <c>33U VR 05083 44673</c>.</summary>
    /// <param name="digits">Easting/northing digits per axis (1–5); 5 gives 1 m precision.</param>
    public static string FromLatLon(GeoCoordinate c, int digits = 5)
    {
        digits = Math.Clamp(digits, 1, 5);
        var utm = Utm.FromLatLon(c);

        var (column, row) = SquareLetters(utm);

        // Round to the nearest millimetre before truncating so projection round-off just below an
        // integer (e.g. 323407.99996) lands on the correct grid digit (23408, not 23407). The extra
        // modulo keeps a value that rounds up to exactly 100000 inside the square.
        var withinEasting = Math.Round(utm.Easting % SquareSize, 3) % SquareSize;
        var withinNorthing = Math.Round(((utm.Northing % SquareSize) + SquareSize) % SquareSize, 3) % SquareSize;
        var divisor = Math.Pow(10, 5 - digits);
        var easting = (int)(Math.Floor(withinEasting) / divisor);
        var northing = (int)(Math.Floor(withinNorthing) / divisor);

        return string.Create(CultureInfo.InvariantCulture,
            $"{utm.ZoneNumber}{utm.ZoneBand} {column}{row} {easting.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0')} {northing.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0')}");
    }

    /// <summary>Parses an MGRS string (spaced or compact) back to WGS84 latitude/longitude (the south-west
    /// corner of the addressed square plus half a cell, so the result sits at the cell centre).</summary>
    public static bool TryParse(string? text, out GeoCoordinate c)
    {
        c = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = MgrsPattern().Match(Regex.Replace(text.Trim(), @"\s+", string.Empty).ToUpperInvariant());
        if (!match.Success)
        {
            return false;
        }

        var zone = int.Parse(match.Groups["zone"].Value, CultureInfo.InvariantCulture);
        if (zone is < 1 or > 60)
        {
            return false;
        }

        var band = match.Groups["band"].Value[0];
        var column = match.Groups["col"].Value[0];
        var row = match.Groups["row"].Value[0];
        if (ColumnLetters.IndexOf(column) < 0 || RowLetters.IndexOf(row) < 0)
        {
            return false;
        }

        var digitsText = match.Groups["digits"].Value;
        if (digitsText.Length % 2 != 0)
        {
            return false;
        }

        var per = digitsText.Length / 2;
        var scale = Math.Pow(10, 5 - per);
        var half = per == 0 ? 0.0 : scale / 2.0;
        var eastDigits = per == 0 ? 0.0 : double.Parse(digitsText[..per], CultureInfo.InvariantCulture) * scale + half;
        var northDigits = per == 0 ? 0.0 : double.Parse(digitsText[per..], CultureInfo.InvariantCulture) * scale + half;

        if (!TryResolveSquare(zone, band, column, row, out var squareEasting, out var squareNorthing))
        {
            return false;
        }

        var easting = squareEasting + eastDigits;
        var northing = squareNorthing + northDigits;
        var utm = new UtmCoordinate(zone, band, easting, northing);
        c = Utm.ToLatLon(utm);
        return true;
    }

    /// <summary>The 100 km-square column/row letters for a UTM coordinate (WGS84 "AA" scheme).</summary>
    private static (char Column, char Row) SquareLetters(UtmCoordinate utm)
    {
        var columnIndex = (int)Math.Floor(utm.Easting / SquareSize) - 1; // eastings span 100k..900k -> sets of A..H
        var setColumnOrigin = ((utm.ZoneNumber - 1) % 3) * 8;            // each zone shifts the column alphabet by 8
        var column = ColumnLetters[(setColumnOrigin + columnIndex) % ColumnLetters.Length];

        var northingSquares = (int)Math.Floor(((utm.Northing % 2_000_000.0) + 2_000_000.0) % 2_000_000.0 / SquareSize);
        var rowOffset = utm.ZoneNumber % 2 == 0 ? 5 : 0; // even zones start the row alphabet 5 letters higher
        var row = RowLetters[(northingSquares + rowOffset) % RowLetters.Length];
        return (column, row);
    }

    /// <summary>Recovers the absolute UTM easting/northing of a 100 km square's south-west corner.</summary>
    private static bool TryResolveSquare(int zone, char band, char column, char row, out double easting, out double northing)
    {
        easting = 0.0;
        northing = 0.0;

        var columnIndex = ColumnLetters.IndexOf(column);
        var setColumnOrigin = ((zone - 1) % 3) * 8;
        var columnSquare = ((columnIndex - setColumnOrigin) % ColumnLetters.Length + ColumnLetters.Length) % ColumnLetters.Length;
        if (columnSquare is < 0 or > 7)
        {
            // A valid 100 km column for a zone is one of 8 consecutive letters (eastings 100k..800k).
            return false;
        }

        easting = (columnSquare + 1) * SquareSize;

        var rowOffset = zone % 2 == 0 ? 5 : 0;
        var rowIndex = RowLetters.IndexOf(row);
        var rowSquare = ((rowIndex - rowOffset) % RowLetters.Length + RowLetters.Length) % RowLetters.Length;

        // The northing repeats every 2,000,000 m (20 squares). Pick the cycle nearest the band's latitude
        // so the same row letter resolves unambiguously across hemispheres.
        var bandSouthNorthing = BandBaseNorthing(band);
        var candidate = rowSquare * SquareSize;
        while (candidate < bandSouthNorthing - 1_000_000.0)
        {
            candidate += 2_000_000.0;
        }

        northing = candidate;
        return true;
    }

    /// <summary>An approximate UTM northing for the southern edge of an MGRS latitude band, used to place a
    /// row letter into the correct 2,000,000 m cycle.</summary>
    private static double BandBaseNorthing(char band)
    {
        var index = "CDEFGHJKLMNPQRSTUVWX".IndexOf(char.ToUpperInvariant(band));
        if (index < 0)
        {
            return 0.0;
        }

        var southLatitude = -80.0 + index * 8.0;
        if (southLatitude >= 0.0)
        {
            // Northern hemisphere: northing grows from 0 at the equator.
            return southLatitude / 90.0 * 10_000_000.0;
        }

        // Southern hemisphere uses the 10,000,000 m false northing.
        return 10_000_000.0 + southLatitude / 90.0 * 10_000_000.0;
    }

    [GeneratedRegex(@"^(?<zone>\d{1,2})(?<band>[C-HJ-NP-X])(?<col>[A-HJ-NP-Z])(?<row>[A-HJ-NP-V])(?<digits>\d*)$")]
    private static partial Regex MgrsPattern();
}
