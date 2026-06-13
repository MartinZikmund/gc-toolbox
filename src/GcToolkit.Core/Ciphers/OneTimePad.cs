using System.Security.Cryptography;
using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// The combining rule applied to each aligned plaintext/key pair. All three are classic
/// polyalphabetic tabula-recta variants; with a key as long as the message and used once they form a
/// true one-time pad.
/// </summary>
public enum OneTimePadVariant
{
    /// <summary>Vigenère / Vernam: encrypt = <c>(P + K)</c>, decrypt = <c>(C − K)</c>. The default.</summary>
    Vigenere,

    /// <summary>Beaufort: <c>(K − P)</c> in both directions — its own inverse (an involution).</summary>
    Beaufort,

    /// <summary>Variant Beaufort (German): encrypt = <c>(P − K)</c>, decrypt = <c>(C + K)</c>.</summary>
    VariantBeaufort,
}

/// <summary>
/// The symbol set the cipher operates over. Symbols outside the active charset either pass through
/// unchanged (beyond-parity preserve mode) or are dropped (letters-only parity mode).
/// </summary>
public enum OneTimePadCharset
{
    /// <summary>The 26 Latin letters A–Z (mod 26) — the classic one-time pad alphabet.</summary>
    Letters,

    /// <summary>Letters then digits: A–Z (0..25), 0–9 (26..35), mod 36.</summary>
    LettersAndDigits,

    /// <summary>The 95 printable ASCII characters <c>' '</c>–<c>'~'</c> (32–126), mod 95.</summary>
    PrintableAscii,
}

/// <summary>The outcome of a transform: the resulting <see cref="Text"/> and whether the key had to be
/// cycled because it was shorter than the processed message (a true OTP needs key length ≥ message).</summary>
public readonly record struct OneTimePadResult(string Text, bool KeyTooShort);

/// <summary>
/// A pure, stateless one-time pad (Vernam) cipher — the single source of truth for the transform. Each
/// processed symbol is combined with the aligned key symbol modulo the active charset size. Beyond
/// geocachingtoolbox.com parity (letters-only A–Z, <c>(P+K)/(C−K)</c> mod 26, non-letters ignored), this
/// codec adds the Beaufort and variant-Beaufort tabula-recta variants, letters+digits (mod 36) and
/// printable-ASCII (mod 95) charsets, an opt-in case- and punctuation-preserving mode, a key-too-short
/// warning, and cryptographically-random pad generation.
/// </summary>
public sealed class OneTimePad
{
    private const char AsciiFirst = ' ';  // 32
    private const char AsciiLast = '~';    // 126
    private const int AsciiSize = AsciiLast - AsciiFirst + 1; // 95

    /// <summary>The number of symbols in <paramref name="charset"/>.</summary>
    public static int CharsetSize(OneTimePadCharset charset) => charset switch
    {
        OneTimePadCharset.LettersAndDigits => 36,
        OneTimePadCharset.PrintableAscii => AsciiSize,
        _ => 26,
    };

    /// <summary>
    /// Combines every processed symbol of <paramref name="text"/> with the aligned symbol of
    /// <paramref name="key"/> per <paramref name="variant"/>, modulo the <paramref name="charset"/> size.
    /// The key advances only on processed symbols and cycles if shorter than the message (setting
    /// <see cref="OneTimePadResult.KeyTooShort"/>). When <paramref name="preserveNonLetters"/> is
    /// <see langword="false"/> (parity), out-of-charset symbols are dropped and letters upper-cased;
    /// when <see langword="true"/>, they pass through and original case is restored.
    /// </summary>
    public OneTimePadResult Transform(
        string? text,
        string? key,
        bool decrypt,
        OneTimePadVariant variant = OneTimePadVariant.Vigenere,
        OneTimePadCharset charset = OneTimePadCharset.Letters,
        bool preserveNonLetters = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new OneTimePadResult(string.Empty, false);
        }

        var size = CharsetSize(charset);

        // Pre-extract the key's usable symbol values once; the key never preserves punctuation.
        var keyValues = ExtractKeyValues(key, charset);
        if (keyValues.Count == 0)
        {
            // No usable key symbols: nothing can be shifted. Leave the text as-is but flag it.
            var passthrough = preserveNonLetters ? text : FilterToCharset(text, charset);
            return new OneTimePadResult(passthrough, true);
        }

        var builder = new StringBuilder(text.Length);
        var keyIndex = 0;
        var processed = 0;

        foreach (var c in text)
        {
            if (TryGetValue(c, charset, out var value, out var wasLower))
            {
                var k = keyValues[keyIndex % keyValues.Count];
                var combined = Combine(value, k, decrypt, variant, size);
                builder.Append(ToChar(combined, charset, preserveNonLetters && wasLower));
                keyIndex++;
                processed++;
            }
            else if (preserveNonLetters)
            {
                builder.Append(c);
            }
            // else: parity mode drops out-of-charset characters.
        }

        var keyTooShort = keyValues.Count < processed;
        return new OneTimePadResult(builder.ToString(), keyTooShort);
    }

    /// <summary>
    /// A cryptographically-random pad of <paramref name="length"/> symbols drawn uniformly from
    /// <paramref name="charset"/> — long enough to be a true one-time pad for a message of that length.
    /// </summary>
    public string GeneratePad(int length, OneTimePadCharset charset = OneTimePadCharset.Letters)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        var size = CharsetSize(charset);
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(ToChar(RandomNumberGenerator.GetInt32(size), charset, lower: false));
        }

        return builder.ToString();
    }

    /// <summary>Applies the variant's combining rule (encrypt or its inverse) within <paramref name="size"/>.</summary>
    private static int Combine(int p, int k, bool decrypt, OneTimePadVariant variant, int size) => variant switch
    {
        // Beaufort is an involution — the same K − value both ways.
        OneTimePadVariant.Beaufort => Mod(k - p, size),
        OneTimePadVariant.VariantBeaufort => decrypt ? Mod(p + k, size) : Mod(p - k, size),
        _ => decrypt ? Mod(p - k, size) : Mod(p + k, size),
    };

    private static List<int> ExtractKeyValues(string? key, OneTimePadCharset charset)
    {
        var values = new List<int>(key?.Length ?? 0);
        if (string.IsNullOrEmpty(key))
        {
            return values;
        }

        foreach (var c in key)
        {
            if (TryGetValue(c, charset, out var value, out _))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static string FilterToCharset(string text, OneTimePadCharset charset)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (TryGetValue(c, charset, out var value, out _))
            {
                builder.Append(ToChar(value, charset, lower: false));
            }
        }

        return builder.ToString();
    }

    /// <summary>Maps a character to its 0-based value in <paramref name="charset"/>, reporting whether a letter was lower-case.</summary>
    private static bool TryGetValue(char c, OneTimePadCharset charset, out int value, out bool wasLower)
    {
        wasLower = false;
        switch (charset)
        {
            case OneTimePadCharset.PrintableAscii:
                if (c is >= AsciiFirst and <= AsciiLast)
                {
                    value = c - AsciiFirst;
                    return true;
                }

                break;

            case OneTimePadCharset.LettersAndDigits:
                if (c is >= 'A' and <= 'Z')
                {
                    value = c - 'A';
                    return true;
                }

                if (c is >= 'a' and <= 'z')
                {
                    wasLower = true;
                    value = c - 'a';
                    return true;
                }

                if (c is >= '0' and <= '9')
                {
                    value = 26 + (c - '0');
                    return true;
                }

                break;

            default: // Letters
                if (c is >= 'A' and <= 'Z')
                {
                    value = c - 'A';
                    return true;
                }

                if (c is >= 'a' and <= 'z')
                {
                    wasLower = true;
                    value = c - 'a';
                    return true;
                }

                break;
        }

        value = 0;
        return false;
    }

    /// <summary>Maps a 0-based value back to its character; letter values render lower-case when <paramref name="lower"/>.</summary>
    private static char ToChar(int value, OneTimePadCharset charset, bool lower) => charset switch
    {
        OneTimePadCharset.PrintableAscii => (char)(AsciiFirst + value),
        OneTimePadCharset.LettersAndDigits when value >= 26 => (char)('0' + (value - 26)),
        OneTimePadCharset.LettersAndDigits => (char)((lower ? 'a' : 'A') + value),
        _ => (char)((lower ? 'a' : 'A') + value),
    };

    private static int Mod(int value, int size) => ((value % size) + size) % size;
}
