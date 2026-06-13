namespace GcToolkit.Core.Ciphers;

/// <summary>One row of the digit→letter reference: a key <see cref="Digit"/> (0–9) and the
/// upper-case <see cref="Letter"/> it shifts by (0⇒A, 1⇒B … 9⇒J).</summary>
public readonly record struct GronsfeldDigitReference(int Digit, char Letter);

/// <summary>
/// A pure, stateless Gronsfeld cipher — the single source of truth for the transform. The Gronsfeld
/// cipher is a Vigenère variant with a purely numeric key: each successive letter is Caesar-shifted by
/// the next digit of the key (0–9), the key repeating cyclically. The key advances <b>only</b> on
/// alphabetic characters, so spaces, digits and punctuation pass through unchanged without consuming a
/// key position (the standard Vigenère/Gronsfeld convention). Letter case is preserved. Encrypting
/// shifts forward by the digit; decrypting shifts backward.
/// </summary>
public sealed class GronsfeldCipher
{
    private const int AlphabetSize = 26;

    /// <summary>The digit→letter reference (0⇒A … 9⇒J) shown in the UI to explain how each digit shifts.</summary>
    public static IReadOnlyList<GronsfeldDigitReference> DigitReference { get; } =
        [.. Enumerable.Range(0, 10).Select(d => new GronsfeldDigitReference(d, (char)('A' + d)))];

    /// <summary>A key is valid when it is non-empty and every character is an ASCII digit 0–9.</summary>
    public static bool IsValidKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        foreach (var c in key)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Shifts every letter of <paramref name="text"/> by the next digit of <paramref name="digitKey"/>
    /// (cycling), forward when <paramref name="decrypt"/> is <see langword="false"/> and backward when
    /// it is <see langword="true"/>. Non-letters are emitted unchanged and do not advance the key;
    /// letter case is preserved.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the transformed text.</returns>
    /// <exception cref="ArgumentException">The key is empty or contains a non-digit character.</exception>
    public string Transform(string? text, string? digitKey, bool decrypt)
    {
        if (!IsValidKey(digitKey))
        {
            throw new ArgumentException("Key must be non-empty and contain only digits 0-9.", nameof(digitKey));
        }

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var key = digitKey!;
        var result = new char[text.Length];
        var keyIndex = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var basis = c switch
            {
                >= 'A' and <= 'Z' => 'A',
                >= 'a' and <= 'z' => 'a',
                _ => '\0',
            };

            if (basis == '\0')
            {
                result[i] = c; // non-letter: pass through, key does not advance
                continue;
            }

            var shift = key[keyIndex] - '0';
            if (decrypt)
            {
                shift = -shift;
            }

            // Normalize into [0, 26) so backward shifts wrap correctly.
            var offset = ((c - basis + shift) % AlphabetSize + AlphabetSize) % AlphabetSize;
            result[i] = (char)(basis + offset);
            keyIndex = (keyIndex + 1) % key.Length;
        }

        return new string(result);
    }
}
