using System.Numerics;
using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>The classic four bases shown side by side: the same value rendered in binary, octal, decimal and hexadecimal.</summary>
public readonly record struct CommonBaseValues(string Binary, string Octal, string Decimal, string Hexadecimal);

/// <summary>
/// A pure, stateless integer base converter. Parses a value written in any base from
/// <see cref="MinBase"/> to <see cref="MaxBase"/> (digits <c>0-9</c> then <c>A-Z</c>,
/// case-insensitive on input) into a <see cref="BigInteger"/>, and renders a value back out in any
/// base in the same range (upper-case digits by convention). A leading <c>-</c> (or <c>+</c>) sign is
/// honoured so negative values round-trip. Matches the geocachingtoolbox.com base-conversion tool
/// (binary/octal/decimal/hex and an arbitrary base) and exceeds it with arbitrary-precision
/// <see cref="BigInteger"/> values that never overflow.
/// </summary>
public sealed class NumberBaseConverter
{
    /// <summary>Smallest supported radix (binary).</summary>
    public const int MinBase = 2;

    /// <summary>Largest supported radix (digits <c>0-9A-Z</c>).</summary>
    public const int MaxBase = 36;

    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary><see langword="true"/> when <paramref name="radix"/> is a usable base in [<see cref="MinBase"/>, <see cref="MaxBase"/>].</summary>
    public static bool IsValidBase(int radix) => radix is >= MinBase and <= MaxBase;

    /// <summary>
    /// Attempts to parse <paramref name="text"/> as an integer written in <paramref name="fromBase"/>.
    /// Surrounding whitespace is ignored and an optional leading <c>+</c>/<c>-</c> sign is honoured.
    /// Returns <see langword="false"/> (never throws) for an empty/null input, an out-of-range base, or
    /// any character that is not a valid digit of that base — so callers can show an error state.
    /// </summary>
    public bool TryParse(string? text, int fromBase, out BigInteger value)
    {
        value = BigInteger.Zero;
        if (!IsValidBase(fromBase) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();

        var negative = false;
        if (span.Length > 0 && (span[0] == '-' || span[0] == '+'))
        {
            negative = span[0] == '-';
            span = span[1..];
        }

        if (span.IsEmpty)
        {
            return false;
        }

        BigInteger radix = fromBase;
        BigInteger acc = BigInteger.Zero;
        foreach (var c in span)
        {
            var digit = DigitValue(c);
            if (digit < 0 || digit >= fromBase)
            {
                value = BigInteger.Zero;
                return false;
            }

            acc = acc * radix + digit;
        }

        value = negative ? -acc : acc;
        return true;
    }

    /// <summary>
    /// Renders <paramref name="value"/> in <paramref name="toBase"/> using upper-case digits, with a
    /// leading <c>-</c> for negatives. Zero is <c>"0"</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="toBase"/> is outside [<see cref="MinBase"/>, <see cref="MaxBase"/>].</exception>
    public string Format(BigInteger value, int toBase)
    {
        if (!IsValidBase(toBase))
        {
            throw new ArgumentOutOfRangeException(nameof(toBase), toBase, $"Base must be between {MinBase} and {MaxBase}.");
        }

        if (value.IsZero)
        {
            return "0";
        }

        var negative = value.Sign < 0;
        var magnitude = BigInteger.Abs(value);
        BigInteger radix = toBase;

        // Emit least-significant digit first, then reverse.
        var builder = new StringBuilder();
        while (magnitude > BigInteger.Zero)
        {
            magnitude = BigInteger.DivRem(magnitude, radix, out var remainder);
            builder.Append(Digits[(int)remainder]);
        }

        if (negative)
        {
            builder.Append('-');
        }

        var chars = new char[builder.Length];
        builder.CopyTo(0, chars, 0, builder.Length);
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>Renders <paramref name="value"/> in the four conventional bases at once (binary, octal, decimal, hex).</summary>
    public CommonBaseValues ToCommonBases(BigInteger value)
        => new(Format(value, 2), Format(value, 8), Format(value, 10), Format(value, 16));

    /// <summary>Maps a digit character (<c>0-9</c>, <c>A-Z</c>, <c>a-z</c>) to its value, or <c>-1</c> if it is not a base-36 digit.</summary>
    private static int DigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'Z' => c - 'A' + 10,
        >= 'a' and <= 'z' => c - 'a' + 10,
        _ => -1,
    };
}
