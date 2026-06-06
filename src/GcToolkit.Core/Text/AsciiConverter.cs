using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Text;

/// <summary>The numeric base a character code is written in.</summary>
public enum AsciiNumberBase
{
    /// <summary>Base 2.</summary>
    Binary = 2,

    /// <summary>Base 8.</summary>
    Octal = 8,

    /// <summary>Base 10.</summary>
    Decimal = 10,

    /// <summary>Base 16 (upper-case digits).</summary>
    Hexadecimal = 16,
}

/// <summary>One character's code shown in every base at once (the "all bases" breakdown row).</summary>
public readonly record struct AsciiCodeRow(
    string Character,
    int CodePoint,
    string Decimal,
    string Hexadecimal,
    string Octal,
    string Binary);

/// <summary>The codes for a single piece of text rendered in all four bases simultaneously.</summary>
public readonly record struct AsciiCodes(string Decimal, string Hexadecimal, string Octal, string Binary);

/// <summary>
/// A pure, stateless converter between text and the numeric codes of its characters. Unlike the
/// geocachingtoolbox.com ASCII tool (which caps at the 0–255 byte range), this works on full Unicode
/// <b>code points</b> — combining UTF-16 surrogate pairs into one code (so astral characters and emoji
/// convert correctly) — and exposes all four bases (decimal, hexadecimal, octal, binary) at once, a
/// configurable separator, both directions, and base auto-detection for decoding.
/// </summary>
public sealed class AsciiConverter
{
    /// <summary>The default separator placed between codes when encoding (a single space).</summary>
    public const string DefaultSeparator = " ";

    /// <summary>The largest valid Unicode scalar value (U+10FFFF).</summary>
    private const int MaxCodePoint = 0x10FFFF;

    /// <summary>
    /// The order auto-detection tries bases in. Binary first (only an all-0/1 input qualifies), then
    /// decimal — the common case for ASCII codes — then hexadecimal (needed once an A–F digit appears),
    /// then octal last because most digit-only inputs are meant as decimal, not octal.
    /// </summary>
    private static readonly AsciiNumberBase[] AutoDetectOrder =
        [AsciiNumberBase.Binary, AsciiNumberBase.Decimal, AsciiNumberBase.Hexadecimal, AsciiNumberBase.Octal];

    /// <summary>
    /// Encodes each Unicode code point of <paramref name="text"/> to its numeric code in
    /// <paramref name="numberBase"/>, joined by <paramref name="separator"/> (a space by default;
    /// pass <see cref="string.Empty"/> to concatenate).
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input.</returns>
    public string Encode(string? text, AsciiNumberBase numberBase, string separator = DefaultSeparator)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var first = true;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!first)
            {
                builder.Append(separator);
            }

            builder.Append(Format(rune.Value, numberBase));
            first = false;
        }

        return builder.ToString();
    }

    /// <summary>Encodes <paramref name="text"/> once into all four bases (for the "show every base" view).</summary>
    public AsciiCodes EncodeAll(string? text, string separator = DefaultSeparator)
        => new(
            Encode(text, AsciiNumberBase.Decimal, separator),
            Encode(text, AsciiNumberBase.Hexadecimal, separator),
            Encode(text, AsciiNumberBase.Octal, separator),
            Encode(text, AsciiNumberBase.Binary, separator));

    /// <summary>
    /// Decodes whitespace/comma-separated <paramref name="codes"/> written in <paramref name="numberBase"/>
    /// back to text. Tolerant of any run of separators and of a <c>0x</c>/<c>0X</c> prefix on hex tokens.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> (with <paramref name="text"/> set) when every token is a valid code point in
    /// range; <see langword="true"/> with empty text for blank input; <see langword="false"/> if any token
    /// is not a valid number for the base or is outside the Unicode range.
    /// </returns>
    public bool TryDecode(string? codes, AsciiNumberBase numberBase, out string text)
    {
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(codes))
        {
            return true;
        }

        var builder = new StringBuilder();
        foreach (var token in Tokenize(codes))
        {
            if (!TryParse(token, numberBase, out var codePoint))
            {
                text = string.Empty;
                return false;
            }

            builder.Append(char.ConvertFromUtf32(codePoint));
        }

        text = builder.ToString();
        return true;
    }

    /// <summary>
    /// Decodes <paramref name="codes"/> after inferring the base from the tokens (binary, then octal,
    /// then decimal, then hexadecimal — the first base under which <em>every</em> token is a valid
    /// in-range code point wins).
    /// </summary>
    public bool TryDecodeAuto(string? codes, out string text, out AsciiNumberBase detectedBase)
    {
        detectedBase = AsciiNumberBase.Decimal;
        if (string.IsNullOrWhiteSpace(codes))
        {
            text = string.Empty;
            return true;
        }

        foreach (var candidate in AutoDetectOrder)
        {
            if (TryDecode(codes, candidate, out text) && text.Length > 0)
            {
                detectedBase = candidate;
                return true;
            }
        }

        text = string.Empty;
        return false;
    }

    /// <summary>
    /// Decomposes <paramref name="text"/> into one <see cref="AsciiCodeRow"/> per code point, each
    /// carrying the character, its code point, and its rendering in all four bases.
    /// </summary>
    public IReadOnlyList<AsciiCodeRow> Describe(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var rows = new List<AsciiCodeRow>();
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            rows.Add(new AsciiCodeRow(
                rune.ToString(),
                value,
                Format(value, AsciiNumberBase.Decimal),
                Format(value, AsciiNumberBase.Hexadecimal),
                Format(value, AsciiNumberBase.Octal),
                Format(value, AsciiNumberBase.Binary)));
        }

        return rows;
    }

    /// <summary>Splits an input string into code tokens on any run of non-alphanumeric characters.</summary>
    private static IEnumerable<string> Tokenize(string codes)
        => codes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(static part => part.Split(',', StringSplitOptions.RemoveEmptyEntries));

    private static string Format(int value, AsciiNumberBase numberBase) => numberBase switch
    {
        AsciiNumberBase.Decimal => value.ToString(CultureInfo.InvariantCulture),
        AsciiNumberBase.Hexadecimal => ToRadix(value, 16),
        AsciiNumberBase.Octal => ToRadix(value, 8),
        AsciiNumberBase.Binary => ToRadix(value, 2),
        _ => value.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>Renders a non-negative value in the given radix (2–16) with upper-case hex digits, no prefix.</summary>
    private static string ToRadix(int value, int radix)
    {
        if (value == 0)
        {
            return "0";
        }

        const string Digits = "0123456789ABCDEF";
        Span<char> buffer = stackalloc char[32];
        var i = buffer.Length;
        while (value > 0)
        {
            buffer[--i] = Digits[value % radix];
            value /= radix;
        }

        return new string(buffer[i..]);
    }

    private static bool TryParse(string token, AsciiNumberBase numberBase, out int codePoint)
    {
        codePoint = 0;

        if (numberBase == AsciiNumberBase.Hexadecimal &&
            (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)))
        {
            token = token[2..];
        }

        if (token.Length == 0)
        {
            return false;
        }

        var radix = (int)numberBase;
        long value = 0;
        foreach (var c in token)
        {
            var digit = DigitValue(c);
            if (digit < 0 || digit >= radix)
            {
                return false;
            }

            value = (value * radix) + digit;
            if (value > MaxCodePoint)
            {
                return false;
            }
        }

        // Surrogate halves (U+D800–U+DFFF) are not valid scalar values on their own.
        if (value is >= 0xD800 and <= 0xDFFF)
        {
            return false;
        }

        codePoint = (int)value;
        return true;
    }

    private static int DigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'F' => c - 'A' + 10,
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => -1,
    };
}
