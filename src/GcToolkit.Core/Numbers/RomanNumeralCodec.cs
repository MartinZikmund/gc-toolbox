using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>One additive piece of a numeral: the symbol(s) emitted and the value they contribute.</summary>
public readonly record struct RomanNumeralPart(string Symbol, long Value);

/// <summary>
/// Bidirectional Roman numeral translator. Encoding emits canonical subtractive numerals;
/// decoding is lenient (it computes a value for non-standard additive forms common in geocaching
/// puzzles, e.g. <c>IIII</c>) and reports whether the input was canonical. Coverage exceeds the
/// geocachingtoolbox.com converter (which caps at 3999) by supporting the <b>vinculum</b> — an
/// overline that multiplies a symbol by 1000 — for values up to 3,999,999.
/// </summary>
/// <remarks>
/// The vinculum is represented in text as the combining overline <see cref="Vinculum"/> (<c>U+0305</c>)
/// after each thousands-block symbol; that is the form this codec emits and parses. Per the common
/// convention, the whole thousands count is overlined (e.g. 4000 = <c>I̅V̅</c>, 5000 = <c>V̅</c>), and the
/// vinculum is only used from 4000 upward (1000–3999 stay as <c>M</c>/<c>MM</c>/<c>MMM</c>).
/// </remarks>
public sealed class RomanNumeralCodec
{
    /// <summary>Smallest representable value (Roman numerals have no zero or negatives).</summary>
    public const long MinValue = 1;

    /// <summary>Largest representable value: <c>M̅M̅M̅C̅M̅X̅C̅I̅X̅CMXCIX</c>.</summary>
    public const long MaxValue = 3_999_999;

    /// <summary>Combining overline (<c>U+0305</c>) used to render the vinculum after a symbol.</summary>
    public const char Vinculum = '̅';

    private const long VinculumThreshold = 4000;

    /// <summary>Greedy value→symbol table for the 1–3999 range, largest first.</summary>
    private static readonly (int Value, string Symbol)[] Table =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
        (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
        (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    ];

    private static readonly IReadOnlyDictionary<char, long> BaseValues = new Dictionary<char, long>
    {
        ['I'] = 1,
        ['V'] = 5,
        ['X'] = 10,
        ['L'] = 50,
        ['C'] = 100,
        ['D'] = 500,
        ['M'] = 1000,
    };

    /// <summary>Encodes <paramref name="value"/> to a canonical Roman numeral.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Value is outside [<see cref="MinValue"/>, <see cref="MaxValue"/>].</exception>
    public string Encode(long value)
    {
        EnsureInRange(value);

        var builder = new StringBuilder();
        foreach (var part in BuildParts(value))
        {
            builder.Append(part.Symbol);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Attempts to decode a Roman numeral. Returns <see langword="false"/> for empty input, unknown
    /// characters, a malformed overline, or an out-of-range result. On success <paramref name="value"/>
    /// is the computed integer and <paramref name="isCanonical"/> is <see langword="true"/> only when the
    /// input is exactly the canonical numeral for that value (so non-standard forms can be flagged).
    /// </summary>
    public bool TryDecode(string? text, out long value, out bool isCanonical)
    {
        value = 0;
        isCanonical = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().ToUpperInvariant();

        // Tokenize into symbol values, where a base letter optionally followed by a vinculum is one
        // symbol (the vinculum multiplies it by 1000). `normalized` rebuilds the letter+vinculum form
        // for the canonicality comparison against Encode's output.
        var symbolValues = new List<long>();
        var normalized = new StringBuilder(trimmed.Length);
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (!BaseValues.TryGetValue(c, out var symbolValue))
            {
                // Unknown character — also catches a vinculum that is not attached to a base letter.
                return false;
            }

            normalized.Append(c);
            if (i + 1 < trimmed.Length && trimmed[i + 1] == Vinculum)
            {
                symbolValue *= 1000;
                normalized.Append(Vinculum);
                i++;
            }

            symbolValues.Add(symbolValue);
        }

        // Subtractive scan: a symbol smaller than the next one is subtracted, otherwise added. This
        // computes the right value for canonical subtractives (IV), additive runs (IIII), and
        // cross-vinculum subtractives (MV̅ = 4000) alike.
        long total = 0;
        for (var i = 0; i < symbolValues.Count; i++)
        {
            var current = symbolValues[i];
            if (i + 1 < symbolValues.Count && current < symbolValues[i + 1])
            {
                total -= current;
            }
            else
            {
                total += current;
            }
        }

        if (total < MinValue || total > MaxValue)
        {
            return false;
        }

        value = total;
        isCanonical = string.Equals(Encode(total), normalized.ToString(), StringComparison.Ordinal);
        return true;
    }

    /// <summary>Decomposes <paramref name="value"/> into its ordered additive parts (one per greedy
    /// step), e.g. <c>1994 → M(1000), CM(900), XC(90), IV(4)</c>. Thousands-block parts carry the overline.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Value is outside [<see cref="MinValue"/>, <see cref="MaxValue"/>].</exception>
    public IReadOnlyList<RomanNumeralPart> Explain(long value)
    {
        EnsureInRange(value);
        return BuildParts(value);
    }

    private static List<RomanNumeralPart> BuildParts(long value)
    {
        var parts = new List<RomanNumeralPart>();

        // Values from 4000 up write (value / 1000) as an overlined numeral, then the remainder normally.
        if (value >= VinculumThreshold)
        {
            AppendGreedy(parts, (int)(value / 1000), overlined: true);
            value %= 1000;
        }

        AppendGreedy(parts, (int)value, overlined: false);
        return parts;
    }

    private static void AppendGreedy(List<RomanNumeralPart> parts, int amount, bool overlined)
    {
        foreach (var (val, symbol) in Table)
        {
            while (amount >= val)
            {
                amount -= val;
                parts.Add(overlined
                    ? new RomanNumeralPart(Overline(symbol), (long)val * 1000)
                    : new RomanNumeralPart(symbol, val));
            }
        }
    }

    private static string Overline(string symbol)
    {
        var builder = new StringBuilder(symbol.Length * 2);
        foreach (var c in symbol)
        {
            builder.Append(c).Append(Vinculum);
        }

        return builder.ToString();
    }

    private static void EnsureInRange(long value)
    {
        if (value < MinValue || value > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value must be between {MinValue} and {MaxValue}.");
        }
    }
}
