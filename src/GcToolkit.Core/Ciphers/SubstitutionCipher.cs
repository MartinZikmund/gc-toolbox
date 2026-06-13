using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>How characters that have no mapping in the key are emitted.</summary>
public enum UnknownCharacterHandling
{
    /// <summary>Emit the character unchanged (the default pass-through behaviour).</summary>
    Preserve,

    /// <summary>Drop the character entirely.</summary>
    Remove,

    /// <summary>Replace the character with an asterisk (<c>*</c>).</summary>
    Replace,
}

/// <summary>The reason a substitution key is rejected by <see cref="SubstitutionCipher.Validate"/>.</summary>
public enum KeyValidationError
{
    /// <summary>The key is well-formed.</summary>
    None,

    /// <summary>The key does not contain exactly 26 letters.</summary>
    WrongLength,

    /// <summary>A letter appears more than once.</summary>
    DuplicateLetter,

    /// <summary>The key contains a character that is not an A–Z letter.</summary>
    NonLetter,
}

/// <summary>
/// The outcome of validating a substitution key: whether it is usable and, if not, the first problem
/// found together with the character that triggered it (for an inline, human-readable message).
/// </summary>
public readonly record struct KeyValidationResult(bool IsValid, KeyValidationError Error, char OffendingCharacter)
{
    /// <summary>A successful validation.</summary>
    public static KeyValidationResult Valid { get; } = new(true, KeyValidationError.None, '\0');
}

/// <summary>
/// A pure, stateless monoalphabetic substitution cipher — the single source of truth for the
/// transform. Each plain letter A–Z maps to the letter at the same position of a user-supplied
/// 26-letter substitution alphabet (the key); decoding applies the inverse mapping. Case is preserved
/// by default (both cases of a plain letter map through the same key, keeping the original case in the
/// output); a "case sensitive" mode instead treats the key as covering only the case it is written in.
/// Characters with no mapping are preserved, removed, or replaced by <c>*</c> per
/// <see cref="UnknownCharacterHandling"/>. Beyond reference parity, this codec exposes
/// <see cref="InvertKey"/>, keyword→alphabet generation (<see cref="GenerateFromKeyword"/>) and
/// partial-key auto-completion (<see cref="AutoComplete"/>).
/// </summary>
public sealed class SubstitutionCipher
{
    /// <summary>The ordered plain alphabet A–Z that a key maps from.</summary>
    public const string PlainAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>The number of letters a complete substitution key must contain.</summary>
    public const int AlphabetSize = 26;

    /// <summary>
    /// Substitutes every letter of <paramref name="text"/> through <paramref name="key"/>. When
    /// <paramref name="decrypt"/> is set the inverse key is applied. Out-of-key characters are handled
    /// per <paramref name="unknownHandling"/>; case follows <paramref name="caseSensitive"/>.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for null/empty input; the (possibly partial) key is used
    /// as-is, so callers should <see cref="Validate"/> first for a complete 1:1 mapping.</returns>
    public string Transform(
        string? text,
        string key,
        bool decrypt,
        bool caseSensitive = false,
        UnknownCharacterHandling unknownHandling = UnknownCharacterHandling.Preserve)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var effectiveKey = decrypt ? InvertKey(key) : NormalizeKey(key);
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (TryMap(c, effectiveKey, caseSensitive, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }

            switch (unknownHandling)
            {
                case UnknownCharacterHandling.Remove:
                    break;
                case UnknownCharacterHandling.Replace:
                    builder.Append('*');
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The reciprocal key: the alphabet that decodes what <paramref name="key"/> encodes. Encoding with
    /// the inverse of a key is the same as decoding with the key. Non-letters and missing letters in a
    /// partial key fall back to the plain letter so the result is always 26 characters long.
    /// </summary>
    public string InvertKey(string key)
    {
        var normalized = NormalizeKey(key);
        var inverse = new char[AlphabetSize];
        Array.Fill(inverse, '\0');

        for (var i = 0; i < AlphabetSize && i < normalized.Length; i++)
        {
            var c = normalized[i];
            if (c is >= 'A' and <= 'Z')
            {
                inverse[c - 'A'] = (char)('A' + i);
            }
        }

        // Any plain position with no inverse entry maps to itself (keeps length stable for partial keys).
        for (var i = 0; i < AlphabetSize; i++)
        {
            if (inverse[i] == '\0')
            {
                inverse[i] = (char)('A' + i);
            }
        }

        return new string(inverse);
    }

    /// <summary>
    /// Validates a substitution key, ignoring case and surrounding/interleaved whitespace. Reports the
    /// first problem found: wrong length, a duplicated letter, or a non-letter character.
    /// </summary>
    public KeyValidationResult Validate(string? key)
    {
        var compact = Compact(key);

        if (compact.Length != AlphabetSize)
        {
            return new KeyValidationResult(false, KeyValidationError.WrongLength, '\0');
        }

        Span<bool> seen = stackalloc bool[AlphabetSize];
        foreach (var c in compact)
        {
            var upper = char.ToUpperInvariant(c);
            if (upper is < 'A' or > 'Z')
            {
                return new KeyValidationResult(false, KeyValidationError.NonLetter, c);
            }

            if (seen[upper - 'A'])
            {
                return new KeyValidationResult(false, KeyValidationError.DuplicateLetter, upper);
            }

            seen[upper - 'A'] = true;
        }

        return KeyValidationResult.Valid;
    }

    /// <summary>
    /// Builds a substitution alphabet from a keyword (the ACA "K1" construction): the keyword's distinct
    /// letters in order, then the remaining A–Z letters in alphabetical order. Non-letters and case are
    /// ignored. An empty keyword yields the plain alphabet (identity).
    /// </summary>
    public string GenerateFromKeyword(string? keyword) => BuildFromSeed(keyword);

    /// <summary>
    /// Completes a partial key into a full 26-letter alphabet: keeps the distinct letters already typed
    /// (in order), then appends the unused A–Z letters alphabetically. Useful while a key is half-entered.
    /// </summary>
    public string AutoComplete(string? partialKey) => BuildFromSeed(partialKey);

    private static string BuildFromSeed(string? seed)
    {
        Span<bool> used = stackalloc bool[AlphabetSize];
        var result = new char[AlphabetSize];
        var count = 0;

        if (!string.IsNullOrEmpty(seed))
        {
            foreach (var c in seed)
            {
                var upper = char.ToUpperInvariant(c);
                if (upper is < 'A' or > 'Z')
                {
                    continue;
                }

                var index = upper - 'A';
                if (used[index])
                {
                    continue;
                }

                used[index] = true;
                result[count++] = upper;
            }
        }

        for (var i = 0; i < AlphabetSize; i++)
        {
            if (!used[i])
            {
                result[count++] = (char)('A' + i);
            }
        }

        return new string(result);
    }

    /// <summary>Maps a single character through <paramref name="key"/>, honouring case rules.</summary>
    private static bool TryMap(char c, string key, bool caseSensitive, out char mapped)
    {
        mapped = c;

        if (c is >= 'A' and <= 'Z')
        {
            var index = c - 'A';
            if (index >= key.Length)
            {
                return false;
            }

            mapped = key[index];
            return true;
        }

        if (c is >= 'a' and <= 'z')
        {
            // In case-sensitive mode an upper-case key has no lower-case entries, so lower-case is "unknown".
            if (caseSensitive)
            {
                return false;
            }

            var index = c - 'a';
            if (index >= key.Length)
            {
                return false;
            }

            mapped = char.ToLowerInvariant(key[index]);
            return true;
        }

        return false;
    }

    /// <summary>Upper-cases the key and strips whitespace, leaving letter positions intact.</summary>
    private static string NormalizeKey(string? key) => Compact(key).ToUpperInvariant();

    private static string Compact(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(key.Length);
        foreach (var c in key)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
