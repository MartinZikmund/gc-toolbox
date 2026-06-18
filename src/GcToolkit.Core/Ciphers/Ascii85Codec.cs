using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>The Base85 / ASCII-85 dialect to encode or decode with.</summary>
public enum Ascii85Variant
{
    /// <summary>Adobe ASCII-85: alphabet '!'(33)..'u'(117), optional <c>&lt;~ ~&gt;</c> delimiters,
    /// <c>z</c> abbreviates an all-zero group. Seen inside PDF/PostScript streams.</summary>
    Adobe,

    /// <summary>btoa/Usenet: same 5/4 alphabet as Adobe with both <c>z</c> (all-zero) and
    /// <c>y</c> (all-space) group abbreviations.</summary>
    Btoa,

    /// <summary>Z85 (ZeroMQ RFC 32): a printable, shell-safe alphabet, input padded to a multiple
    /// of 4 bytes (pad count recorded so arbitrary text round-trips).</summary>
    Z85,

    /// <summary>RFC 1924: the IPv6-address alphabet applied with btoa-style 5/4 grouping
    /// (a practical interpretation — the RFC itself only encodes 128-bit values).</summary>
    Rfc1924,
}

/// <summary>Toggles that refine encoding for the abbreviation-capable variants.</summary>
public sealed class Ascii85Options
{
    /// <summary>Wrap Adobe output in <c>&lt;~ ~&gt;</c> delimiters.</summary>
    public bool UseAdobeDelimiters { get; init; }

    /// <summary>Emit <c>z</c> for an all-zero group (Adobe/btoa). On by default.</summary>
    public bool AbbreviateZeroGroup { get; init; } = true;

    /// <summary>Emit <c>y</c> for an all-space group (btoa only). On by default.</summary>
    public bool AbbreviateSpaceGroup { get; init; } = true;
}

/// <summary>An illegal character found while decoding, with its zero-based position in the
/// whitespace-stripped, delimiter-stripped payload.</summary>
public sealed record Ascii85DecodeError(char Character, int Position, string Message);

/// <summary>One variant's attempt at decoding the same input (for the "try all variants" panel).</summary>
public sealed record Ascii85VariantResult(Ascii85Variant Variant, bool Success, string Text, byte[] Bytes, string Hex, string? Error);

/// <summary>
/// Pure, offline, deterministic Base85 / ASCII-85 codec covering the Adobe, btoa/Usenet, Z85 and
/// RFC 1924 dialects. Encoding packs every 4 bytes into 5 printable characters; decoding is
/// whitespace-tolerant, strips Adobe delimiters, reports the first illegal character with its
/// position, and never throws on malformed input. No UI or platform dependencies.
/// </summary>
public sealed class Ascii85Codec
{
    private const string Z85Alphabet =
        "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";

    private const string Rfc1924Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!#$%&()*+-;<=>?@^_`{|}~";

    private const char AdobeFirst = '!';   // 33
    private const string AdobeOpen = "<~";
    private const string AdobeClose = "~>";

    private static readonly uint[] Pow85 = [52200625u, 614125u, 7225u, 85u, 1u];

    private static readonly int[] Z85Decode = BuildDecodeTable(Z85Alphabet);
    private static readonly int[] Rfc1924Decode = BuildDecodeTable(Rfc1924Alphabet);

    /// <summary>Encodes UTF-8 text. Convenience over <see cref="EncodeBytes"/>.</summary>
    public string EncodeText(string text, Ascii85Variant variant, Ascii85Options? options = null)
        => EncodeBytes(Encoding.UTF8.GetBytes(text ?? string.Empty), variant, options);

    /// <summary>Encodes raw bytes to the chosen variant.</summary>
    public string EncodeBytes(byte[] data, Ascii85Variant variant, Ascii85Options? options = null)
    {
        data ??= [];
        options ??= new Ascii85Options();

        return variant switch
        {
            Ascii85Variant.Z85 => EncodeZ85(data),
            _ => EncodeFiveFour(data, variant, options),
        };
    }

    /// <summary>Decodes to raw bytes. Returns <see langword="false"/> and sets <paramref name="error"/>
    /// on the first illegal character or a malformed final group; <paramref name="bytes"/> is then empty.</summary>
    public bool TryDecodeToBytes(string input, Ascii85Variant variant, out byte[] bytes, out Ascii85DecodeError? error)
    {
        bytes = [];
        error = null;

        return variant switch
        {
            Ascii85Variant.Z85 => TryDecodeZ85(input ?? string.Empty, out bytes, out error),
            _ => TryDecodeFiveFour(input ?? string.Empty, variant, out bytes, out error),
        };
    }

    /// <summary>Decodes and renders the bytes as UTF-8 text.</summary>
    public bool TryDecodeToText(string input, Ascii85Variant variant, out string text, out Ascii85DecodeError? error)
    {
        text = string.Empty;
        if (!TryDecodeToBytes(input, variant, out var bytes, out error))
        {
            return false;
        }

        text = Encoding.UTF8.GetString(bytes);
        return true;
    }

    /// <summary>Formats bytes as space-separated uppercase hex (e.g. <c>"00 FF"</c>).</summary>
    public static string ToHex(byte[] bytes)
        => bytes is { Length: > 0 } ? string.Join(' ', bytes.Select(b => b.ToString("X2"))) : string.Empty;

    /// <summary>Inspects charset and delimiters to guess the most likely variant. Prefers a variant
    /// whose alphabet/markers uniquely fit, then the first that decodes cleanly, defaulting to Adobe.</summary>
    public Ascii85Variant DetectVariant(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Ascii85Variant.Adobe;
        }

        if (input.Contains(AdobeOpen, StringComparison.Ordinal))
        {
            return Ascii85Variant.Adobe;
        }

        var payload = StripWhitespace(input);

        // 'y' only appears in btoa output; 'z' is an Adobe/btoa zero-group marker.
        if (payload.Contains('y'))
        {
            return Ascii85Variant.Btoa;
        }

        var withinAdobe = payload.All(c => c is >= AdobeFirst and <= 'u' or 'z');
        var withinZ85 = payload.All(c => c < Z85Decode.Length && Z85Decode[c] >= 0);

        // A clean multiple-of-5 block that fits Z85 but uses characters outside the Adobe range (or is
        // a bare aligned block) is almost certainly Z85 — Adobe streams seldom land on an exact 5-block
        // without a partial group, and Z85 is fixed-block by design.
        if (withinZ85 && payload.Length % 5 == 0 && payload.Length > 0)
        {
            if (!withinAdobe || payload.Contains('#') || payload.Contains('$'))
            {
                return Ascii85Variant.Z85;
            }

            // Ambiguous (fits both alphabets): prefer the variant whose decode looks like readable text.
            return MostTextLike(payload, [Ascii85Variant.Z85, Ascii85Variant.Adobe]);
        }

        // Otherwise pick the first variant that decodes without error, in puzzle-likelihood order.
        foreach (var variant in (ReadOnlySpan<Ascii85Variant>)
            [Ascii85Variant.Adobe, Ascii85Variant.Btoa, Ascii85Variant.Z85, Ascii85Variant.Rfc1924])
        {
            if (TryDecodeToBytes(input, variant, out var bytes, out _) && bytes.Length > 0)
            {
                return variant;
            }
        }

        return Ascii85Variant.Adobe;
    }

    /// <summary>Returns the candidate whose decoded UTF-8 is the most printable; ties keep list order.</summary>
    private Ascii85Variant MostTextLike(string payload, ReadOnlySpan<Ascii85Variant> candidates)
    {
        var best = candidates[0];
        var bestScore = -1.0;
        foreach (var variant in candidates)
        {
            var score = TryDecodeToBytes(payload, variant, out var bytes, out _) ? Printability(bytes) : -1.0;
            if (score > bestScore)
            {
                bestScore = score;
                best = variant;
            }
        }

        return best;
    }

    private static double Printability(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return 0;
        }

        var text = Encoding.UTF8.GetString(bytes);
        var printable = text.Count(c => !char.IsControl(c) || c is '\n' or '\r' or '\t');
        return (double)printable / text.Length;
    }

    /// <summary>Decodes the same input with every variant for side-by-side comparison.</summary>
    public IReadOnlyList<Ascii85VariantResult> TryAllVariants(string? input)
    {
        var results = new List<Ascii85VariantResult>();
        foreach (var variant in Enum.GetValues<Ascii85Variant>())
        {
            if (TryDecodeToBytes(input ?? string.Empty, variant, out var bytes, out var error))
            {
                results.Add(new Ascii85VariantResult(
                    variant, true, Encoding.UTF8.GetString(bytes), bytes, ToHex(bytes), null));
            }
            else
            {
                results.Add(new Ascii85VariantResult(
                    variant, false, string.Empty, [], string.Empty, error?.Message));
            }
        }

        return results;
    }

    // ---- 5/4 family (Adobe, btoa, RFC 1924) ----

    private static string EncodeFiveFour(byte[] data, Ascii85Variant variant, Ascii85Options options)
    {
        var builder = new StringBuilder(data.Length * 5 / 4 + 8);
        var useRfc = variant == Ascii85Variant.Rfc1924;
        var allowZ = options.AbbreviateZeroGroup && variant is Ascii85Variant.Adobe or Ascii85Variant.Btoa;
        var allowY = options.AbbreviateSpaceGroup && variant is Ascii85Variant.Btoa;

        // One reusable 5-char scratch span; every iteration overwrites all five slots before reading them.
        Span<char> group = stackalloc char[5];
        for (var i = 0; i < data.Length; i += 4)
        {
            var count = Math.Min(4, data.Length - i);
            uint tuple = 0;
            for (var j = 0; j < 4; j++)
            {
                tuple = (tuple << 8) | (j < count ? data[i + j] : (byte)0);
            }

            if (count == 4 && allowZ && tuple == 0)
            {
                builder.Append('z');
                continue;
            }

            if (count == 4 && allowY && tuple == 0x20202020)
            {
                builder.Append('y');
                continue;
            }

            // Five base-85 digits, most significant first; a partial group emits count+1 chars.
            for (var k = 4; k >= 0; k--)
            {
                var digit = (int)(tuple % 85);
                tuple /= 85;
                group[k] = useRfc ? Rfc1924Alphabet[digit] : (char)(AdobeFirst + digit);
            }

            builder.Append(group[..(count + 1)]);
        }

        if (variant == Ascii85Variant.Adobe && options.UseAdobeDelimiters)
        {
            return AdobeOpen + builder + AdobeClose;
        }

        return builder.ToString();
    }

    private static bool TryDecodeFiveFour(string input, Ascii85Variant variant, out byte[] bytes, out Ascii85DecodeError? error)
    {
        bytes = [];
        error = null;

        var useRfc = variant == Ascii85Variant.Rfc1924;
        var payload = StripAdobeDelimiters(input);

        var output = new List<byte>(payload.Length);
        Span<int> group = stackalloc int[5];
        var groupLen = 0;

        for (var i = 0; i < payload.Length; i++)
        {
            var c = payload[i];
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            // Group abbreviations expand to a full word and may not appear mid-group.
            if (!useRfc && (c == 'z' || c == 'y'))
            {
                if (groupLen != 0)
                {
                    error = Error(c, i, "Abbreviation inside a group");
                    return false;
                }

                var fill = c == 'z' ? (byte)0 : (byte)0x20;
                output.Add(fill);
                output.Add(fill);
                output.Add(fill);
                output.Add(fill);
                continue;
            }

            if (!TryMapDigit(c, useRfc, out var digit))
            {
                error = Error(c, i, $"'{c}' is not valid for this variant");
                return false;
            }

            group[groupLen++] = digit;
            if (groupLen == 5)
            {
                EmitGroup(group, 5, output);
                groupLen = 0;
            }
        }

        if (groupLen == 1)
        {
            // A single leftover digit cannot represent any byte.
            error = new Ascii85DecodeError('\0', payload.Length, "Truncated final group");
            return false;
        }

        if (groupLen > 0)
        {
            EmitGroup(group, groupLen, output);
        }

        bytes = [.. output];
        return true;
    }

    private static void EmitGroup(Span<int> group, int len, List<byte> output)
    {
        // Pad a partial group with the maximum digit (84), decode 4 bytes, keep len-1.
        uint tuple = 0;
        for (var k = 0; k < 5; k++)
        {
            var digit = k < len ? (uint)group[k] : 84u;
            tuple += digit * Pow85[k];
        }

        var produce = len - 1;
        for (var b = 0; b < produce; b++)
        {
            output.Add((byte)(tuple >> (24 - b * 8)));
        }
    }

    private static bool TryMapDigit(char c, bool useRfc, out int digit)
    {
        if (useRfc)
        {
            if (c < Rfc1924Decode.Length && Rfc1924Decode[c] >= 0)
            {
                digit = Rfc1924Decode[c];
                return true;
            }

            digit = -1;
            return false;
        }

        if (c is >= AdobeFirst and <= 'u')
        {
            digit = c - AdobeFirst;
            return true;
        }

        digit = -1;
        return false;
    }

    // ---- Z85 ----

    private static string EncodeZ85(byte[] data)
    {
        // Z85 proper requires a multiple-of-4 input and emits no padding marker — so an already-aligned
        // payload (e.g. the official vector) encodes to pure Z85. For arbitrary text we pad to a multiple
        // of 4 and prefix the pad count (1-3) as a single Z85 digit so decode can strip it and round-trip.
        var pad = (4 - data.Length % 4) % 4;
        var padded = new byte[data.Length + pad];
        Array.Copy(data, padded, data.Length);

        var builder = new StringBuilder(padded.Length * 5 / 4 + 1);
        if (pad > 0)
        {
            builder.Append(Z85Alphabet[pad]);
        }

        // One reusable 5-char scratch span; every iteration overwrites all five slots before reading them.
        Span<char> group = stackalloc char[5];
        for (var i = 0; i < padded.Length; i += 4)
        {
            uint value = ((uint)padded[i] << 24) | ((uint)padded[i + 1] << 16)
                | ((uint)padded[i + 2] << 8) | padded[i + 3];

            for (var k = 4; k >= 0; k--)
            {
                group[k] = Z85Alphabet[(int)(value % 85)];
                value /= 85;
            }

            builder.Append(group);
        }

        return builder.ToString();
    }

    private static bool TryDecodeZ85(string input, out byte[] bytes, out Ascii85DecodeError? error)
    {
        bytes = [];
        error = null;

        var payload = StripWhitespace(input);
        if (payload.Length == 0)
        {
            return true;
        }

        // First char is the pad-count digit written by EncodeZ85; recover it if present.
        var pad = 0;
        var body = payload;
        if (payload.Length % 5 == 1)
        {
            var first = payload[0];
            if (first < Z85Decode.Length && Z85Decode[first] is >= 0 and <= 3)
            {
                pad = Z85Decode[first];
                body = payload[1..];
            }
        }

        if (body.Length % 5 != 0)
        {
            error = new Ascii85DecodeError('\0', payload.Length, "Z85 length must be a multiple of 5 characters");
            return false;
        }

        var output = new List<byte>(body.Length / 5 * 4);
        for (var i = 0; i < body.Length; i += 5)
        {
            uint value = 0;
            for (var k = 0; k < 5; k++)
            {
                var c = body[i + k];
                if (c >= Z85Decode.Length || Z85Decode[c] < 0)
                {
                    var pos = payload.Length % 5 == 1 ? i + k + 1 : i + k;
                    error = Error(c, pos, $"'{c}' is not in the Z85 alphabet");
                    bytes = [];
                    return false;
                }

                value = value * 85 + (uint)Z85Decode[c];
            }

            output.Add((byte)(value >> 24));
            output.Add((byte)(value >> 16));
            output.Add((byte)(value >> 8));
            output.Add((byte)value);
        }

        if (pad > 0 && pad <= output.Count)
        {
            output.RemoveRange(output.Count - pad, pad);
        }

        bytes = [.. output];
        return true;
    }

    // ---- helpers ----

    private static Ascii85DecodeError Error(char c, int position, string message)
        => new(c, position, $"{message} (position {position})");

    private static string StripWhitespace(string input)
    {
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

    private static string StripAdobeDelimiters(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.StartsWith(AdobeOpen, StringComparison.Ordinal))
        {
            trimmed = trimmed[AdobeOpen.Length..];
        }

        var close = trimmed.IndexOf(AdobeClose, StringComparison.Ordinal);
        if (close >= 0)
        {
            trimmed = trimmed[..close];
        }

        return trimmed;
    }

    private static int[] BuildDecodeTable(string alphabet)
    {
        var table = new int[128];
        Array.Fill(table, -1);
        for (var i = 0; i < alphabet.Length; i++)
        {
            table[alphabet[i]] = i;
        }

        return table;
    }
}
