using System.Buffers.Text;
using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>The Base64 alphabet/wrapping flavor used for an encode or decode.</summary>
public enum Base64Variant
{
    /// <summary>RFC 4648 standard alphabet (<c>A–Z a–z 0–9 + /</c>) with <c>=</c> padding.</summary>
    Standard,

    /// <summary>RFC 4648 URL/filename-safe alphabet (<c>-</c> and <c>_</c> replace <c>+</c> and <c>/</c>).</summary>
    UrlSafe,

    /// <summary>Standard alphabet wrapped to 76-character lines separated by CRLF (RFC 2045 / MIME).</summary>
    Mime,
}

/// <summary>How the plaintext side is turned into / read from bytes.</summary>
public enum Base64TextEncoding
{
    /// <summary>UTF-8 (default) — handles every Unicode character.</summary>
    Utf8,

    /// <summary>US-ASCII; non-ASCII characters become <c>?</c>.</summary>
    Ascii,

    /// <summary>ISO-8859-1 (Latin-1) — one byte per character for the first 256 code points.</summary>
    Latin1,
}

/// <summary>Whether <see cref="Base64Codec.AutoDirection"/> chose to encode, decode, or do nothing.</summary>
public enum Base64Direction
{
    None,
    Encoded,
    Decoded,
}

/// <summary>The decoded payload, rendered several ways so non-text data stays readable in the field.</summary>
/// <param name="Bytes">The raw decoded bytes.</param>
public readonly record struct Base64DecodeResult(byte[] Bytes)
{
    /// <summary>The decoded bytes interpreted as UTF-8 text (invalid sequences become the replacement char).</summary>
    public string AsUtf8Text => Encoding.UTF8.GetString(Bytes);

    /// <summary>The decoded bytes interpreted as Latin-1 (ISO-8859-1) text — every byte maps to a character.</summary>
    public string AsLatin1Text => Encoding.Latin1.GetString(Bytes);

    /// <summary>Space-separated upper-case hex view of the bytes, e.g. <c>66 6F 6F</c>.</summary>
    public string Hex => Convert.ToHexString(Bytes).Chunk(2).Select(static c => new string(c)) is var pairs
        ? string.Join(' ', pairs)
        : string.Empty;

    /// <summary>Number of decoded bytes.</summary>
    public int ByteCount => Bytes.Length;
}

/// <summary>The result of auto-direction detection: the chosen direction and produced text.</summary>
public readonly record struct Base64AutoResult(Base64Direction Direction, string Output);

/// <summary>One entry of <see cref="Base64Codec.TryAllVariants"/>: a variant and how it decoded the input.</summary>
public readonly record struct Base64VariantAttempt(Base64Variant Variant, bool Success, string Text, bool IsPrintable, int ByteCount);

/// <summary>
/// Pure, offline Base64 codec — the single source of truth for the Base64 tool. Encodes and decodes
/// the Standard (RFC 4648), URL-safe, and MIME variants with an optional padding toggle and a
/// selectable plaintext encoding (UTF-8 / ASCII / Latin-1). Decoding is deliberately <b>forgiving</b>:
/// it strips stray whitespace and newlines, auto-fixes missing padding, and transparently accepts the
/// URL-safe alphabet even in Standard mode so puzzle text pasted from anywhere just works. Beyond
/// straight conversion it offers auto-direction detection and a try-all-variants helper for nudging
/// stubborn geocaching ciphertext.
/// </summary>
public sealed class Base64Codec
{
    private const int MimeLineLength = 76;

    /// <summary>Encodes <paramref name="text"/> (via <paramref name="textEncoding"/>) to Base64.</summary>
    public string Encode(string? text, Base64Variant variant, bool padding, Base64TextEncoding textEncoding)
        => EncodeBytes(GetEncoding(textEncoding).GetBytes(text ?? string.Empty), variant, padding);

    /// <summary>Encodes raw <paramref name="bytes"/> to Base64 in the chosen <paramref name="variant"/>.</summary>
    public string EncodeBytes(byte[] bytes, Base64Variant variant, bool padding)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var encoded = Convert.ToBase64String(bytes);

        if (variant == Base64Variant.UrlSafe)
        {
            encoded = encoded.Replace('+', '-').Replace('/', '_');
        }

        if (!padding)
        {
            encoded = encoded.TrimEnd('=');
        }

        if (variant == Base64Variant.Mime)
        {
            encoded = WrapMime(encoded);
        }

        return encoded;
    }

    /// <summary>
    /// Forgiving decode. Returns <see langword="false"/> only when the input genuinely is not Base64
    /// (invalid characters, an impossible length, or malformed padding). Whitespace and the URL-safe
    /// alphabet are always tolerated, and missing padding is added automatically.
    /// </summary>
    public bool TryDecode(string? input, Base64Variant variant, out Base64DecodeResult result)
        => TryDecode(input, variant, out result, out _);

    /// <summary>Forgiving decode, additionally reporting a human-readable <paramref name="error"/> on failure.</summary>
    public bool TryDecode(string? input, Base64Variant variant, out Base64DecodeResult result, out string error)
    {
        result = default;
        error = string.Empty;

        // Strip every whitespace character (handles MIME CRLF wrapping and pasted line breaks).
        var cleaned = Strip(input);
        if (cleaned.Length == 0)
        {
            result = new Base64DecodeResult([]);
            return true;
        }

        // Always fold the URL-safe alphabet back to standard so either mode accepts either input.
        cleaned = cleaned.Replace('-', '+').Replace('_', '/');

        // Drop any existing padding and re-pad to a multiple of four.
        cleaned = cleaned.TrimEnd('=');
        var remainder = cleaned.Length % 4;
        if (remainder == 1)
        {
            error = "Invalid Base64 length.";
            return false;
        }

        if (remainder != 0)
        {
            cleaned = cleaned.PadRight(cleaned.Length + (4 - remainder), '=');
        }

        Span<byte> buffer = new byte[cleaned.Length / 4 * 3];
        if (Base64.DecodeFromUtf8(Encoding.ASCII.GetBytes(cleaned), buffer, out _, out var written)
            is System.Buffers.OperationStatus.Done)
        {
            result = new Base64DecodeResult(buffer[..written].ToArray());
            return true;
        }

        error = "Input contains characters that are not valid Base64.";
        return false;
    }

    /// <summary>
    /// Detects intent: if the input decodes cleanly to <b>printable</b> bytes it is treated as Base64
    /// and decoded; otherwise it is treated as plaintext and encoded. Returns
    /// <see cref="Base64Direction.None"/> for blank input.
    /// </summary>
    public Base64AutoResult AutoDirection(string? input, Base64Variant variant, bool padding, Base64TextEncoding textEncoding)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new Base64AutoResult(Base64Direction.None, string.Empty);
        }

        if (TryDecode(input, variant, out var decoded) && decoded.ByteCount > 0 && IsPrintable(decoded.Bytes))
        {
            return new Base64AutoResult(Base64Direction.Decoded, Render(decoded, textEncoding));
        }

        return new Base64AutoResult(Base64Direction.Encoded, Encode(input, variant, padding, textEncoding));
    }

    /// <summary>Decodes the input under every variant, flagging which attempts yield printable text.</summary>
    public IReadOnlyList<Base64VariantAttempt> TryAllVariants(string? input)
    {
        var attempts = new List<Base64VariantAttempt>(3);
        foreach (var variant in (ReadOnlySpan<Base64Variant>)[Base64Variant.Standard, Base64Variant.UrlSafe, Base64Variant.Mime])
        {
            if (TryDecode(input, variant, out var decoded))
            {
                var printable = IsPrintable(decoded.Bytes);
                attempts.Add(new Base64VariantAttempt(variant, true, decoded.AsUtf8Text, printable, decoded.ByteCount));
            }
            else
            {
                attempts.Add(new Base64VariantAttempt(variant, false, string.Empty, false, 0));
            }
        }

        return attempts;
    }

    /// <summary>Renders a decoded payload as text in the requested <paramref name="encoding"/> view.</summary>
    public static string Render(Base64DecodeResult result, Base64TextEncoding encoding) => encoding switch
    {
        Base64TextEncoding.Latin1 => result.AsLatin1Text,
        Base64TextEncoding.Ascii => result.AsLatin1Text,
        _ => result.AsUtf8Text,
    };

    private static string WrapMime(string encoded)
    {
        if (encoded.Length <= MimeLineLength)
        {
            return encoded;
        }

        var builder = new StringBuilder(encoded.Length + (encoded.Length / MimeLineLength * 2));
        for (var i = 0; i < encoded.Length; i += MimeLineLength)
        {
            if (i > 0)
            {
                builder.Append("\r\n");
            }

            builder.Append(encoded.AsSpan(i, Math.Min(MimeLineLength, encoded.Length - i)));
        }

        return builder.ToString();
    }

    private static string Strip(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Heuristic: bytes decode to "printable" text if every byte is a UTF-8 sequence of
    /// printable/whitespace characters (no control bytes outside tab/newline). Used to bias
    /// auto-direction and to flag try-all-variants hits.</summary>
    private static bool IsPrintable(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return false;
        }

        try
        {
            var text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            foreach (var rune in text.EnumerateRunes())
            {
                if (Rune.IsControl(rune) && rune.Value is not ('\t' or '\n' or '\r'))
                {
                    return false;
                }
            }

            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static Encoding GetEncoding(Base64TextEncoding encoding) => encoding switch
    {
        Base64TextEncoding.Ascii => Encoding.ASCII,
        Base64TextEncoding.Latin1 => Encoding.Latin1,
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };
}
