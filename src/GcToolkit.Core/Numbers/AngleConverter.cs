using System.Globalization;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Numbers;

/// <summary>The angle units the converter supports.</summary>
public enum AngleUnit
{
    Degrees,
    Radians,
    Gradians,
    MilsNato,
    MilsMilliradian,
    MilsSoviet,
    MilsSweden,
    Turns,
    Points,
    HourAngles,
    Arcminutes,
    Arcseconds,
}

/// <summary>
/// Converts an angle between every common unit (issue #8). Conversion uses a single canonical axis —
/// "units per full turn" — so any-to-any is <c>target = value / unitsPerTurn[source] * unitsPerTurn[target]</c>;
/// this keeps every pair exact-by-construction and the table the only source of truth. Coverage matches
/// the geocachingtoolbox.com converter's 10 units and goes beyond parity with arcminutes/arcseconds plus
/// lenient DMS (sexagesimal) parsing and formatting for degrees.
/// </summary>
public sealed class AngleConverter
{
    private const string DegreeGlyph = "°";
    private const string MinuteGlyph = "'";
    private const string SecondGlyph = "\"";

    /// <summary>How many of each unit make one full turn (the canonical axis).</summary>
    private static readonly IReadOnlyDictionary<AngleUnit, double> UnitsPerTurn = new Dictionary<AngleUnit, double>
    {
        [AngleUnit.Degrees] = 360,
        [AngleUnit.Radians] = 2 * Math.PI,
        [AngleUnit.Gradians] = 400,
        [AngleUnit.MilsNato] = 6400,
        [AngleUnit.MilsMilliradian] = 2000 * Math.PI,
        [AngleUnit.MilsSoviet] = 6000,
        [AngleUnit.MilsSweden] = 6300,
        [AngleUnit.Turns] = 1,
        [AngleUnit.Points] = 32,
        [AngleUnit.HourAngles] = 24,
        [AngleUnit.Arcminutes] = 21600,
        [AngleUnit.Arcseconds] = 1296000,
    };

    /// <summary>All supported units in display order.</summary>
    public static IReadOnlyList<AngleUnit> Units { get; } = Enum.GetValues<AngleUnit>();

    /// <summary>Converts <paramref name="value"/> from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public double Convert(double value, AngleUnit from, AngleUnit to)
    {
        if (from == to)
        {
            return value;
        }

        var turns = value / UnitsPerTurn[from];
        return turns * UnitsPerTurn[to];
    }

    /// <summary>The number of <paramref name="unit"/> in one full turn (exposed for tests/labels).</summary>
    public static double PerTurn(AngleUnit unit) => UnitsPerTurn[unit];

    // Lenient DMS: optional sign, a degrees number, then optional minutes and seconds, each with an
    // optional unit glyph. The seconds glyph is matched as "any non-digit trailer" (\D) to stay lenient
    // about which glyph follows. Decimal point or comma is accepted on any component; whitespace anywhere.
    private static readonly Regex DmsPattern = new(
        @"^\s*(?<sign>[-+])?\s*(?<deg>\d+(?:[.,]\d+)?)\s*°?\s*(?:(?<min>\d+(?:[.,]\d+)?)\s*['′]?\s*(?:(?<sec>\d+(?:[.,]\d+)?)\s*\D?\s*)?)?$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses an angle in degrees, leniently accepting a plain decimal (<c>45</c>, <c>45.5</c>, <c>45,5</c>)
    /// or a sexagesimal DMS form (<c>45°30'15"</c>, lenient about glyphs and whitespace). Returns
    /// <see langword="false"/> for empty, malformed, or non-numeric input.
    /// </summary>
    public static bool TryParseDegrees(string? text, out double degrees)
    {
        degrees = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = DmsPattern.Match(text);
        if (!match.Success)
        {
            return false;
        }

        if (!TryParseComponent(match.Groups["deg"].Value, out var deg))
        {
            return false;
        }

        var minutes = 0.0;
        if (match.Groups["min"].Success && !TryParseComponent(match.Groups["min"].Value, out minutes))
        {
            return false;
        }

        var seconds = 0.0;
        if (match.Groups["sec"].Success && !TryParseComponent(match.Groups["sec"].Value, out seconds))
        {
            return false;
        }

        var magnitude = deg + (minutes / 60.0) + (seconds / 3600.0);
        degrees = match.Groups["sign"].Value == "-" ? -magnitude : magnitude;
        return true;
    }

    /// <summary>Formats decimal <paramref name="degrees"/> as a sexagesimal <c>D°M'S"</c> string.</summary>
    public static string FormatDms(double degrees)
    {
        var sign = degrees < 0 ? "-" : string.Empty;
        var total = Math.Abs(degrees);

        var d = (int)Math.Floor(total);
        var minutesTotal = (total - d) * 60.0;
        var m = (int)Math.Floor(minutesTotal);
        var s = (int)Math.Round((minutesTotal - m) * 60.0);

        // Carry rounded seconds/minutes (e.g. 59.6 sec -> 60 rolls into the next minute).
        if (s >= 60)
        {
            s -= 60;
            m++;
        }

        if (m >= 60)
        {
            m -= 60;
            d++;
        }

        var inv = CultureInfo.InvariantCulture;
        return sign
            + d.ToString(inv) + DegreeGlyph
            + m.ToString(inv) + MinuteGlyph
            + s.ToString(inv) + SecondGlyph;
    }

    private static bool TryParseComponent(string value, out double result)
        => double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
}
