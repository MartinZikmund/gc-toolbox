using System.Globalization;
using System.Net;
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

/// <summary>
/// An input/output format of the converter. The four numeric bases mirror <see cref="AsciiNumberBase"/>;
/// <see cref="Text"/>, <see cref="Base32"/>, <see cref="Base64"/> and <see cref="Html"/> complete the
/// any-to-any matrix offered by geocachingtoolbox.com.
/// </summary>
public enum AsciiFormat
{
    /// <summary>Plain text — the pivot every other format converts through.</summary>
    Text,

    /// <summary>Character codes in base 2.</summary>
    Binary,

    /// <summary>Character codes in base 8.</summary>
    Octal,

    /// <summary>Character codes in base 10.</summary>
    Decimal,

    /// <summary>Character codes in base 16.</summary>
    Hexadecimal,

    /// <summary>RFC 4648 Base32 of the text's UTF-8 bytes.</summary>
    Base32,

    /// <summary>RFC 4648 Base64 of the text's UTF-8 bytes.</summary>
    Base64,

    /// <summary>HTML character entities (numeric on output; numeric <b>and</b> named accepted on input).</summary>
    Html,
}

/// <summary>One character's code shown in every base at once (the "all bases" breakdown row).</summary>
public readonly record struct AsciiCodeRow(
    string Character,
    int CodePoint,
    string Decimal,
    string Hexadecimal,
    string Octal,
    string Binary)
{
    /// <summary>
    /// Announced verbatim by screen readers for the list row (a row without this reads as the record's
    /// generated form).
    /// </summary>
    public override string ToString()
        => $"{Character} — {Decimal} decimal, {Hexadecimal} hex, {Octal} octal, {Binary} binary";
}

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
    /// Decodes <paramref name="codes"/> after inferring the base from the tokens (binary, then decimal,
    /// then hexadecimal, then octal — the first base under which <em>every</em> token is a valid
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

    // ---- Any-to-any format conversion (Text / bases / Base32 / Base64 / HTML entities) ----

    /// <summary>
    /// Converts <paramref name="input"/> from <paramref name="from"/> to <paramref name="to"/>, going
    /// through text as the pivot (so e.g. binary code groups → hexadecimal code groups works). Pass
    /// <see langword="null"/> for <paramref name="from"/> to auto-detect the input format.
    /// </summary>
    /// <param name="resolvedFrom">The format actually used to read the input.</param>
    /// <returns><see langword="false"/> when the input is not valid in the source format.</returns>
    public bool TryConvert(
        string? input,
        AsciiFormat? from,
        AsciiFormat to,
        string separator,
        bool removeSpaces,
        out string result,
        out AsciiFormat resolvedFrom)
    {
        resolvedFrom = from ?? AsciiFormat.Text;
        result = string.Empty;

        if (string.IsNullOrEmpty(input))
        {
            return true;
        }

        if (from is null)
        {
            DetectFormat(input, out resolvedFrom);
        }

        if (!TryToText(input, resolvedFrom, out var text))
        {
            return false;
        }

        result = FromText(text, to, separator);
        if (removeSpaces)
        {
            result = result.Replace(" ", string.Empty, StringComparison.Ordinal);
        }

        return true;
    }

    /// <summary>Reads <paramref name="input"/> written in <paramref name="format"/> back to plain text.</summary>
    public bool TryToText(string? input, AsciiFormat format, out string text)
    {
        text = string.Empty;
        if (string.IsNullOrEmpty(input))
        {
            return true;
        }

        switch (format)
        {
            case AsciiFormat.Text:
                text = input;
                return true;

            case AsciiFormat.Binary:
            case AsciiFormat.Octal:
            case AsciiFormat.Decimal:
            case AsciiFormat.Hexadecimal:
                return TryDecode(input, ToNumberBase(format), out text);

            case AsciiFormat.Base32:
                if (!TryFromBase32(input, out var base32Bytes))
                {
                    return false;
                }

                text = ToText(base32Bytes);
                return true;

            case AsciiFormat.Base64:
                if (!TryFromBase64(input, out var base64Bytes))
                {
                    return false;
                }

                text = ToText(base64Bytes);
                return true;

            case AsciiFormat.Html:
                text = WebUtility.HtmlDecode(input);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Writes plain <paramref name="text"/> out in <paramref name="format"/>.</summary>
    public string FromText(string? text, AsciiFormat format, string separator = DefaultSeparator)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return format switch
        {
            AsciiFormat.Text => text,
            AsciiFormat.Binary or AsciiFormat.Octal or AsciiFormat.Decimal or AsciiFormat.Hexadecimal
                => Encode(text, ToNumberBase(format), separator),
            AsciiFormat.Base32 => ToBase32(Encoding.UTF8.GetBytes(text)),
            AsciiFormat.Base64 => System.Convert.ToBase64String(Encoding.UTF8.GetBytes(text)),
            AsciiFormat.Html => ToHtmlEntities(text),
            _ => text,
        };
    }

    /// <summary>
    /// Infers the input format: the first numeric base under which <em>every</em> token is a valid,
    /// in-range code point wins; otherwise the input is treated as plain text. Base32/Base64 are
    /// deliberately excluded — almost any letter run is valid in them, so they'd swallow real text.
    /// </summary>
    public bool DetectFormat(string? input, out AsciiFormat format)
    {
        format = AsciiFormat.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        foreach (var candidate in AutoDetectOrder)
        {
            if (TryDecode(input, candidate, out var decoded) && decoded.Length > 0)
            {
                format = ToFormat(candidate);
                return true;
            }
        }

        return true;
    }

    private static AsciiNumberBase ToNumberBase(AsciiFormat format) => format switch
    {
        AsciiFormat.Binary => AsciiNumberBase.Binary,
        AsciiFormat.Octal => AsciiNumberBase.Octal,
        AsciiFormat.Hexadecimal => AsciiNumberBase.Hexadecimal,
        _ => AsciiNumberBase.Decimal,
    };

    private static AsciiFormat ToFormat(AsciiNumberBase numberBase) => numberBase switch
    {
        AsciiNumberBase.Binary => AsciiFormat.Binary,
        AsciiNumberBase.Octal => AsciiFormat.Octal,
        AsciiNumberBase.Hexadecimal => AsciiFormat.Hexadecimal,
        _ => AsciiFormat.Decimal,
    };

    /// <summary>UTF-8 first (what we encode with); falls back to Latin-1 so arbitrary byte blobs still show.</summary>
    private static string ToText(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static string ToHtmlEntities(string text)
    {
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            builder.Append("&#").Append(rune.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        return builder.ToString();
    }

    private static bool TryFromBase64(string input, out byte[] bytes)
    {
        bytes = [];
        try
        {
            // Convert already ignores embedded whitespace; strip our own separators too.
            var cleaned = new string([.. input.Where(static c => !char.IsWhiteSpace(c) && c != ',')]);
            if (cleaned.Length == 0)
            {
                return true;
            }

            bytes = System.Convert.FromBase64String(cleaned);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string ToBase32(byte[] bytes)
    {
        var builder = new StringBuilder(((bytes.Length + 4) / 5) * 8);
        for (var offset = 0; offset < bytes.Length; offset += 5)
        {
            var chunk = Math.Min(5, bytes.Length - offset);
            ulong buffer = 0;
            for (var i = 0; i < 5; i++)
            {
                buffer <<= 8;
                if (i < chunk)
                {
                    buffer |= bytes[offset + i];
                }
            }

            // 5 bytes -> 8 chars; a short final chunk yields fewer chars, the rest is '=' padding.
            var charCount = chunk switch { 1 => 2, 2 => 4, 3 => 5, 4 => 7, _ => 8 };
            for (var i = 0; i < 8; i++)
            {
                builder.Append(i < charCount ? Base32Alphabet[(int)((buffer >> (35 - (i * 5))) & 0x1F)] : '=');
            }
        }

        return builder.ToString();
    }

    private static bool TryFromBase32(string input, out byte[] bytes)
    {
        bytes = [];
        var symbols = new List<int>(input.Length);
        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c) || c is '=' or ',')
            {
                continue;
            }

            var index = Base32Alphabet.IndexOf(char.ToUpperInvariant(c));
            if (index < 0)
            {
                return false;
            }

            symbols.Add(index);
        }

        var result = new List<byte>(symbols.Count * 5 / 8);
        var bitBuffer = 0;
        var bitCount = 0;
        foreach (var symbol in symbols)
        {
            bitBuffer = (bitBuffer << 5) | symbol;
            bitCount += 5;
            if (bitCount >= 8)
            {
                bitCount -= 8;
                result.Add((byte)((bitBuffer >> bitCount) & 0xFF));
            }
        }

        bytes = [.. result];
        return true;
    }

    /// <summary>Splits an input string into code tokens on any run of non-alphanumeric characters.</summary>
    private static IEnumerable<string> Tokenize(string codes)
    {
        var start = -1;
        for (var i = 0; i < codes.Length; i++)
        {
            if (char.IsLetterOrDigit(codes[i]))
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                yield return codes[start..i];
                start = -1;
            }
        }

        if (start >= 0)
        {
            yield return codes[start..];
        }
    }

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
