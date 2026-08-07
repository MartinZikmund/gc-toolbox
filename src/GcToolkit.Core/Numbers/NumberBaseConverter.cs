using System.Numerics;
using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>The classic four bases shown side by side: the same value rendered in binary, octal, decimal and hexadecimal.</summary>
public readonly record struct CommonBaseValues(string Binary, string Octal, string Decimal, string Hexadecimal);

/// <summary>A value rendered in one particular <paramref name="Radix"/>.</summary>
public readonly record struct BaseRendering(int Radix, string Text);

/// <summary>
/// One token of a batch conversion: the original <paramref name="Input"/> and — when the token was a
/// valid number in the source base — its <paramref name="Output"/>. Invalid tokens come back with
/// <paramref name="IsValid"/> false instead of aborting the batch.
/// </summary>
public readonly record struct BaseConversion(string Input, string Output, bool IsValid);

/// <summary>
/// A pure, stateless integer base converter. Every operation is defined over an ordered *alphabet* of
/// digit glyphs; the radix overloads simply supply the standard one. Bases 2–36 use <c>0-9</c> then
/// <c>A-Z</c> (case-insensitive on input unless the caller asks otherwise, upper-case on output); bases
/// 37–62 need both letter cases as distinct digits and therefore use <c>0-9</c>, <c>a-z</c> (10–35) then
/// <c>A-Z</c> (36–61), always case-sensitively. A leading <c>-</c>/<c>+</c> sign is honoured (unless the
/// alphabet claims that glyph) so negative values round-trip. Matches the geocachingtoolbox.com
/// base-conversion tool — bases 2–62, all-bases rendering, whitespace-separated batches that skip unknown
/// values, the case-sensitivity switch and its manual custom-alphabet mode — and exceeds it with
/// arbitrary-precision <see cref="BigInteger"/> values that never overflow.
/// </summary>
public sealed class NumberBaseConverter
{
    /// <summary>Smallest supported radix (binary).</summary>
    public const int MinBase = 2;

    /// <summary>Largest supported radix (digits <c>0-9a-zA-Z</c>).</summary>
    public const int MaxBase = 62;

    /// <summary>Largest radix whose alphabet uses only one letter case, so input can be folded.</summary>
    public const int MaxCaseInsensitiveBase = 36;

    private const string StandardDigits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string ExtendedDigits = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary><see langword="true"/> when <paramref name="radix"/> is a usable base in [<see cref="MinBase"/>, <see cref="MaxBase"/>].</summary>
    public static bool IsValidBase(int radix) => radix is >= MinBase and <= MaxBase;

    /// <summary>
    /// <see langword="true"/> when <paramref name="radix"/>'s alphabet contains both cases of a letter,
    /// which forces case-sensitive parsing (the reference site disables its checkbox here too).
    /// </summary>
    public static bool RequiresCaseSensitivity(int radix) => radix > MaxCaseInsensitiveBase;

    /// <summary>The ordered digit glyphs <paramref name="radix"/> uses, or an empty string for an invalid base.</summary>
    public static string DigitsFor(int radix)
        => IsValidBase(radix)
            ? (radix <= MaxCaseInsensitiveBase ? StandardDigits : ExtendedDigits)[..radix]
            : string.Empty;

    /// <summary>A usable custom alphabet has at least two glyphs and no duplicates; its length is the radix.</summary>
    public static bool IsValidAlphabet(string? alphabet)
        => alphabet is { Length: >= MinBase } && alphabet.Distinct().Count() == alphabet.Length;

    /// <summary>
    /// <see langword="true"/> when folding letter case keeps every glyph distinct — i.e. when a
    /// case-insensitive reading of <paramref name="alphabet"/> is unambiguous.
    /// </summary>
    public static bool CanFoldCase(string? alphabet)
        => IsValidAlphabet(alphabet)
            && alphabet!.Select(char.ToUpperInvariant).Distinct().Count() == alphabet.Length;

    /// <summary>Splits <paramref name="text"/> into the whitespace-separated tokens a batch conversion works on.</summary>
    public static string[] Tokenize(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Parses <paramref name="text"/> in <paramref name="fromBase"/>, folding letter case where the base allows it.</summary>
    public bool TryParse(string? text, int fromBase, out BigInteger value)
        => TryParse(text, fromBase, caseSensitive: false, out value);

    /// <summary>
    /// Attempts to parse <paramref name="text"/> as an integer written in <paramref name="fromBase"/>.
    /// <paramref name="caseSensitive"/> is ignored (treated as <see langword="true"/>) for bases above
    /// <see cref="MaxCaseInsensitiveBase"/>, whose alphabet already distinguishes the cases.
    /// </summary>
    public bool TryParse(string? text, int fromBase, bool caseSensitive, out BigInteger value)
        => TryParse(text, DigitsFor(fromBase), caseSensitive, out value);

    /// <summary>
    /// Attempts to parse <paramref name="text"/> as an integer written in <paramref name="alphabet"/>,
    /// whose length is the radix and whose glyph order gives each digit's value. Surrounding whitespace is
    /// ignored and a leading <c>+</c>/<c>-</c> is honoured unless the alphabet claims that glyph. Returns
    /// <see langword="false"/> (never throws) for an empty/null input, an unusable alphabet, or any
    /// character that is not one of its glyphs — so callers can show an error state.
    /// </summary>
    public bool TryParse(string? text, string? alphabet, bool caseSensitive, out BigInteger value)
    {
        value = BigInteger.Zero;
        if (!IsValidAlphabet(alphabet) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var digits = alphabet!;
        var fold = !caseSensitive && CanFoldCase(digits);
        var span = text.AsSpan().Trim();

        var negative = false;
        if (span.Length > 0 && (span[0] == '-' || span[0] == '+') && !digits.Contains(span[0]))
        {
            negative = span[0] == '-';
            span = span[1..];
        }

        if (span.IsEmpty)
        {
            return false;
        }

        BigInteger radix = digits.Length;
        BigInteger acc = BigInteger.Zero;
        foreach (var c in span)
        {
            var digit = digits.IndexOf(c);
            if (digit < 0 && fold)
            {
                digit = IndexOfFolded(digits, c);
            }

            if (digit < 0)
            {
                value = BigInteger.Zero;
                return false;
            }

            acc = (acc * radix) + digit;
        }

        value = negative ? -acc : acc;
        return true;
    }

    /// <summary>
    /// Renders <paramref name="value"/> in <paramref name="toBase"/> with a leading <c>-</c> for negatives.
    /// Zero is the alphabet's first glyph.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="toBase"/> is outside [<see cref="MinBase"/>, <see cref="MaxBase"/>].</exception>
    public string Format(BigInteger value, int toBase)
        => IsValidBase(toBase)
            ? Format(value, DigitsFor(toBase))
            : throw new ArgumentOutOfRangeException(nameof(toBase), toBase, $"Base must be between {MinBase} and {MaxBase}.");

    /// <summary>Renders <paramref name="value"/> using the ordered glyphs of <paramref name="alphabet"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="alphabet"/> is too short or has duplicate glyphs.</exception>
    public string Format(BigInteger value, string? alphabet)
    {
        if (!IsValidAlphabet(alphabet))
        {
            throw new ArgumentException("An alphabet needs at least two distinct glyphs.", nameof(alphabet));
        }

        var digits = alphabet!;
        if (value.IsZero)
        {
            return digits[0].ToString();
        }

        var negative = value.Sign < 0;
        var magnitude = BigInteger.Abs(value);
        BigInteger radix = digits.Length;

        // Emit least-significant digit first, then reverse.
        var builder = new StringBuilder();
        while (magnitude > BigInteger.Zero)
        {
            magnitude = BigInteger.DivRem(magnitude, radix, out var remainder);
            builder.Append(digits[(int)remainder]);
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

    /// <summary>The case-folded position of <paramref name="c"/> in <paramref name="digits"/>, or <c>-1</c>.</summary>
    private static int IndexOfFolded(string digits, char c)
    {
        var upper = char.ToUpperInvariant(c);
        for (var i = 0; i < digits.Length; i++)
        {
            if (char.ToUpperInvariant(digits[i]) == upper)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Renders <paramref name="value"/> in the four conventional bases at once (binary, octal, decimal, hex).</summary>
    public CommonBaseValues ToCommonBases(BigInteger value)
        => new(Format(value, 2), Format(value, 8), Format(value, 10), Format(value, 16));

    /// <summary>Renders <paramref name="value"/> in every base from <see cref="MinBase"/> to <see cref="MaxBase"/> ("show all bases").</summary>
    public IReadOnlyList<BaseRendering> ToAllBases(BigInteger value)
    {
        var results = new List<BaseRendering>(MaxBase - MinBase + 1);
        for (var radix = MinBase; radix <= MaxBase; radix++)
        {
            results.Add(new BaseRendering(radix, Format(value, radix)));
        }

        return results;
    }

    /// <summary>Converts every whitespace-separated token of <paramref name="text"/> between two standard bases.</summary>
    public IReadOnlyList<BaseConversion> ConvertBatch(string? text, int fromBase, int toBase, bool caseSensitive = false)
        => ConvertBatch(text, DigitsFor(fromBase), DigitsFor(toBase), caseSensitive);

    /// <summary>
    /// Converts every whitespace-separated token of <paramref name="text"/> independently. Tokens that
    /// aren't valid in <paramref name="fromAlphabet"/> are returned marked invalid rather than halting the
    /// batch, mirroring the reference site's "unknown values are skipped".
    /// </summary>
    public IReadOnlyList<BaseConversion> ConvertBatch(string? text, string? fromAlphabet, string? toAlphabet, bool caseSensitive = false)
    {
        var tokens = Tokenize(text);
        var canFormat = IsValidAlphabet(toAlphabet);
        var results = new List<BaseConversion>(tokens.Length);
        foreach (var token in tokens)
        {
            results.Add(canFormat && TryParse(token, fromAlphabet, caseSensitive, out var value)
                ? new BaseConversion(token, Format(value, toAlphabet), true)
                : new BaseConversion(token, string.Empty, false));
        }

        return results;
    }
}
