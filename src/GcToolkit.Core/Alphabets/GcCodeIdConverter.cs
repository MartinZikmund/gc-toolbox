using System.Globalization;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// Converts a public Geocaching GC-code (e.g. <c>GC16XYD</c>) to and from its internal numeric ID
/// (issue #49). Two eras are handled:
/// <list type="bullet">
///   <item><description><b>Legacy hex era</b> (IDs 0–65535): the code after the <c>GC</c> prefix is a
///   1–4 digit hexadecimal number, so <c>GCFFFF</c> = 65535.</description></item>
///   <item><description><b>Modern base-31 era</b> (IDs ≥ 65536, codes from <c>GCG000</c> up): the code is a
///   base-31 number over the ambiguity-free charset <see cref="Charset"/> (the letters
///   <c>I, L, O, S, U</c> are omitted), with a fixed <see cref="Offset"/> subtracted.</description></item>
/// </list>
/// All logic is pure and head-independent so the ViewModel can stay thin.
/// </summary>
public sealed class GcCodeIdConverter
{
    /// <summary>Base-31 charset with the visually ambiguous letters I, L, O, S, U omitted.</summary>
    public const string Charset = "0123456789ABCDEFGHJKMNPQRTVWXYZ";

    /// <summary>The constant subtracted/added when crossing into the base-31 era.</summary>
    public const long Offset = 411120;

    /// <summary>
    /// The smallest valid base-31 value: 16 * 31^3, i.e. the base-31 value of "G000" — the first
    /// modern code, which maps to ID 65536. A computed base-31 value below this means the body is a
    /// short/sub-threshold token (e.g. "G", "G00") that does not denote a real modern code, so the
    /// modern branch must reject it rather than return a bogus negative/small ID.
    /// </summary>
    private const long MinBase31Value = 476656;

    /// <summary>Highest ID still encoded in the legacy hexadecimal scheme.</summary>
    private const long MaxHexId = 0xFFFF;

    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>
    /// Converts a GC-code to its numeric ID, or <see langword="null"/> when the input is empty or
    /// contains a character outside <see cref="Charset"/>. The leading <c>GC</c> is optional, input is
    /// trimmed and uppercased.
    /// </summary>
    public long? GcCodeToId(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var body = code.Trim().ToUpperInvariant();
        if (body.StartsWith("GC", StringComparison.Ordinal))
        {
            body = body[2..];
        }

        if (body.Length == 0)
        {
            return null;
        }

        // Any character outside the base-31 charset (notably the ambiguous I/L/O/S/U) is invalid.
        foreach (var c in body)
        {
            if (Charset.IndexOf(c) < 0)
            {
                return null;
            }
        }

        // Legacy hex era: a short, all-hex body whose value fits in 16 bits is read as hexadecimal.
        if (body.Length <= 4 && IsHex(body))
        {
            var hexValue = Convert.ToInt64(body, 16);
            if (hexValue <= MaxHexId)
            {
                return hexValue;
            }
        }

        // Modern base-31 era: positional value over the charset, then shift down by the offset.
        long value = 0;
        foreach (var c in body)
        {
            value = (value * Charset.Length) + Charset.IndexOf(c);
        }

        // Only valid from the first modern code "G000" (value 476656 -> id 65536) up. Anything below
        // that is a sub-threshold body that would otherwise yield a bogus negative/small id.
        if (value < MinBase31Value)
        {
            return null;
        }

        return value - Offset;
    }

    /// <summary>
    /// Converts a numeric ID to its GC-code, or <see langword="null"/> for a negative ID. IDs up to
    /// 65535 produce a hex code (<c>GC</c> + uppercase hex, no leading zeros); larger IDs produce the
    /// base-31 code.
    /// </summary>
    public string? IdToGcCode(long id)
    {
        if (id < 0)
        {
            return null;
        }

        if (id <= MaxHexId)
        {
            return "GC" + id.ToString("X", CultureInfo.InvariantCulture);
        }

        var n = id + Offset;
        var digits = new Stack<char>();
        while (n > 0)
        {
            digits.Push(Charset[(int)(n % Charset.Length)]);
            n /= Charset.Length;
        }

        return "GC" + new string([.. digits]);
    }

    /// <summary>Parses a plain decimal ID (used by the ID → code direction). Rejects anything non-numeric.</summary>
    public static bool TryParseId(string? text, out long id)
        => long.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out id);

    private static bool IsHex(string body)
    {
        foreach (var c in body)
        {
            if (HexDigits.IndexOf(c) < 0)
            {
                return false;
            }
        }

        return true;
    }
}
