using System.Globalization;
using System.Numerics;
using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>The base a numeric string is read from / written to.</summary>
public enum NumberRadix
{
    Binary = 2,
    Octal = 8,
    Decimal = 10,
    Hexadecimal = 16,
}

/// <summary>
/// One step of the Gray-code encode transform <c>gray = value ^ (value &gt;&gt; 1)</c>, rendered as
/// fixed-width bit strings so a solver can see the XOR cascade line up column by column.
/// </summary>
public readonly record struct GrayEncodeExplanation(
    ulong Value,
    ulong Gray,
    string ValueBits,
    string ShiftedBits,
    string GrayBits);

/// <summary>
/// Pure bit math for reflected binary (Gray) code. Encoding is <c>gray = value ^ (value &gt;&gt; 1)</c>;
/// decoding is the cumulative-XOR inverse. Operates on <see cref="ulong"/> and provides robust
/// parsing/formatting across bases plus a per-value XOR-cascade explanation — no exceptions reach the UI.
/// </summary>
public sealed class GrayCodeCodec
{
    /// <summary>Highest puzzle letter position (Z) when mapping A=1..Z=26.</summary>
    private const int LetterCount = 26;

    /// <summary>Encodes <paramref name="value"/> to its reflected-binary (Gray) code.</summary>
    public ulong Encode(ulong value) => value ^ (value >> 1);

    /// <summary>Decodes a Gray-code value back to the original via cumulative XOR.</summary>
    public ulong Decode(ulong gray)
    {
        var value = gray;
        for (var shift = gray >> 1; shift != 0; shift >>= 1)
        {
            value ^= shift;
        }

        return value;
    }

    /// <summary>Encodes <paramref name="value"/> and renders the Gray code as a binary string, optionally
    /// zero-padded to <paramref name="bitWidth"/> (0 = auto, the minimum width that fits the value).</summary>
    public string EncodeToBinary(ulong value, int bitWidth = 0)
        => Format(Encode(value), NumberRadix.Binary, bitWidth);

    /// <summary>
    /// Parses <paramref name="text"/> in the given <paramref name="radix"/>. Accepts surrounding
    /// whitespace and the conventional <c>0b</c>/<c>0o</c>/<c>0x</c> prefix matching the radix. Returns
    /// <see langword="false"/> (never throws) for empty, malformed, or out-of-range (&gt; <see cref="ulong.MaxValue"/>) input.
    /// </summary>
    public bool TryParse(string? text, NumberRadix radix, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var token = StripPrefix(text.Trim(), radix);
        if (token.Length == 0)
        {
            return false;
        }

        if (radix == NumberRadix.Decimal)
        {
            return ulong.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        var radixValue = (ulong)(int)radix;
        ulong acc = 0;
        foreach (var c in token)
        {
            if (!TryDigit(c, radixValue, out var digit))
            {
                value = 0;
                return false;
            }

            // Guard against overflow before it wraps.
            if (acc > (ulong.MaxValue - digit) / radixValue)
            {
                value = 0;
                return false;
            }

            acc = acc * radixValue + digit;
        }

        value = acc;
        return true;
    }

    /// <summary>
    /// Parses with auto radix detection: a <c>0b</c>/<c>0o</c>/<c>0x</c> prefix wins, otherwise
    /// <paramref name="fallback"/> is used. Reports the radix actually applied via <paramref name="detected"/>.
    /// </summary>
    public bool TryParseAuto(string? text, NumberRadix fallback, out ulong value, out NumberRadix detected)
    {
        detected = fallback;
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '0')
        {
            detected = char.ToLowerInvariant(trimmed[1]) switch
            {
                'b' => NumberRadix.Binary,
                'o' => NumberRadix.Octal,
                'x' => NumberRadix.Hexadecimal,
                _ => fallback,
            };
        }

        return TryParse(trimmed, detected, out value);
    }

    /// <summary>
    /// Formats <paramref name="value"/> in <paramref name="radix"/>. When <paramref name="bitWidth"/> &gt; 0
    /// the binary form is zero-padded to that width (never truncated); other bases ignore the width.
    /// </summary>
    public string Format(ulong value, NumberRadix radix, int bitWidth = 0)
    {
        var text = radix switch
        {
            NumberRadix.Binary => ToBaseString(value, 2),
            NumberRadix.Octal => ToBaseString(value, 8),
            NumberRadix.Decimal => value.ToString(CultureInfo.InvariantCulture),
            NumberRadix.Hexadecimal => value.ToString("X", CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

        if (radix == NumberRadix.Binary && bitWidth > text.Length)
        {
            text = text.PadLeft(bitWidth, '0');
        }

        return text;
    }

    /// <summary>
    /// Builds the XOR-cascade explanation for a single value: its bits, the right-shifted bits, and the
    /// resulting Gray bits, all padded to the same width (auto = the wider of value/gray's significant bits).
    /// </summary>
    public GrayEncodeExplanation ExplainEncode(ulong value, int bitWidth = 0)
    {
        var gray = Encode(value);
        var shifted = value >> 1;

        var width = bitWidth > 0
            ? bitWidth
            : Math.Max(1, Math.Max(SignificantBits(value), SignificantBits(gray)));

        return new GrayEncodeExplanation(
            value,
            gray,
            Format(value, NumberRadix.Binary, width),
            Format(shifted, NumberRadix.Binary, width),
            Format(gray, NumberRadix.Binary, width));
    }

    /// <summary>Maps a letter to its position (A=1..Z=26) and Gray-encodes it.</summary>
    public bool TryEncodeLetter(char letter, out ulong gray)
    {
        gray = 0;
        if (!TryLetterPosition(letter, out var position))
        {
            return false;
        }

        gray = Encode((ulong)position);
        return true;
    }

    /// <summary>Decodes a Gray value to a letter position (1..26) and maps it back to A–Z.</summary>
    public bool TryDecodeLetter(ulong gray, out char letter)
    {
        letter = '\0';
        var position = Decode(gray);
        if (position is < 1 or > LetterCount)
        {
            return false;
        }

        letter = (char)('A' + (int)position - 1);
        return true;
    }

    /// <summary>
    /// Gray-encodes a letter through its A=1..Z=26 position and returns the letter sitting at the
    /// Gray-coded position (e.g. <c>B</c>→pos 2→gray 3→<c>C</c>) — the common "Gray code hides a word"
    /// puzzle case. Returns <see langword="false"/> for non-letters and for the few letters whose Gray
    /// position falls outside A–Z, so the caller can pass those through unchanged.
    /// </summary>
    public bool TryEncodeLetterToLetter(char letter, out char encoded)
    {
        encoded = '\0';
        if (!TryLetterPosition(letter, out var position))
        {
            return false;
        }

        var gray = Encode((ulong)position);
        if (gray is < 1 or > LetterCount)
        {
            return false;
        }

        encoded = (char)('A' + (int)gray - 1);
        return true;
    }

    /// <summary>
    /// Inverse of <see cref="TryEncodeLetterToLetter"/>: treats the letter's A=1..Z=26 position as a Gray
    /// code, decodes it, and returns the letter at the original position (e.g. <c>C</c>→pos 3→decode 2→<c>B</c>).
    /// </summary>
    public bool TryDecodeLetterToLetter(char letter, out char decoded)
    {
        decoded = '\0';
        if (!TryLetterPosition(letter, out var position))
        {
            return false;
        }

        var original = Decode((ulong)position);
        if (original is < 1 or > LetterCount)
        {
            return false;
        }

        decoded = (char)('A' + (int)original - 1);
        return true;
    }

    private static bool TryLetterPosition(char letter, out int position)
    {
        var upper = char.ToUpperInvariant(letter);
        if (upper is >= 'A' and <= 'Z')
        {
            position = upper - 'A' + 1;
            return true;
        }

        position = 0;
        return false;
    }

    private static int SignificantBits(ulong value)
        => value == 0 ? 1 : 64 - BitOperations.LeadingZeroCount(value);

    private static string ToBaseString(ulong value, int radix)
    {
        if (value == 0)
        {
            return "0";
        }

        const string Digits = "0123456789ABCDEF";
        var builder = new StringBuilder();
        var r = (ulong)radix;
        while (value > 0)
        {
            builder.Insert(0, Digits[(int)(value % r)]);
            value /= r;
        }

        return builder.ToString();
    }

    private static string StripPrefix(string token, NumberRadix radix)
    {
        if (token.Length < 2 || token[0] != '0')
        {
            return token;
        }

        var marker = char.ToLowerInvariant(token[1]);
        var matches = radix switch
        {
            NumberRadix.Binary => marker == 'b',
            NumberRadix.Octal => marker == 'o',
            NumberRadix.Hexadecimal => marker == 'x',
            _ => false,
        };

        return matches ? token[2..] : token;
    }

    private static bool TryDigit(char c, ulong radix, out ulong digit)
    {
        digit = c switch
        {
            >= '0' and <= '9' => (ulong)(c - '0'),
            >= 'a' and <= 'f' => (ulong)(c - 'a' + 10),
            >= 'A' and <= 'F' => (ulong)(c - 'A' + 10),
            _ => ulong.MaxValue,
        };

        return digit < radix;
    }
}
