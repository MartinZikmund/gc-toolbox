namespace GcToolkit.Core.Ciphers;

/// <summary>One key-length candidate from the assisted solver: the <see cref="Length"/> tried and the
/// averaged per-column <see cref="IndexOfCoincidence"/> it produced (values near ~0.067 indicate the
/// likely true length or a multiple of it).</summary>
public readonly record struct VigenereKeyLengthCandidate(int Length, double IndexOfCoincidence);

/// <summary>
/// The outcome of an assisted (unknown-key) decode: the recovered <see cref="Key"/>, the resulting
/// <see cref="PlainText"/> (with the original formatting preserved), the <see cref="KeyLength"/> the
/// solver settled on, and the ranked <see cref="KeyLengthCandidates"/> it weighed.
/// </summary>
public sealed partial record VigenereSolveResult(
    string Key,
    string PlainText,
    int KeyLength,
    IReadOnlyList<VigenereKeyLengthCandidate> KeyLengthCandidates);

/// <summary>
/// A pure, stateless Vigenère (polyalphabetic shift) cipher over A–Z — the single source of truth for
/// the transform and the assisted solver. Encoding/decoding preserve letter case, pass every non-letter
/// through unchanged, and advance the key only on letters (so spaces and punctuation in either the text
/// or the key are skipped in the key cycle). Beyond geocachingtoolbox.com parity (encode/decode with a
/// keyword, and an assisted unknown-key solver) this codec preserves case &amp; full formatting, exposes
/// the substitution alphabet for a "key" display, and recovers an unknown key via the Index of
/// Coincidence (key-length estimate) plus chi-squared scoring against English letter frequencies.
/// </summary>
public sealed class VigenereCipher
{
    /// <summary>Number of symbols in the alphabet (A–Z).</summary>
    public const int AlphabetSize = 26;

    /// <summary>The expected Index of Coincidence of normal English text (~0.0667).</summary>
    public const double EnglishIndexOfCoincidence = 0.0667;

    /// <summary>The Index of Coincidence of a uniformly random 26-letter text (1/26 ≈ 0.0385).</summary>
    public const double RandomIndexOfCoincidence = 1.0 / AlphabetSize;

    /// <summary>Shortest key length the solver will search (matches the reference site's minimum).</summary>
    public const int MinSupportedKeyLength = 1;

    /// <summary>Longest key length the solver will search (matches the reference site's maximum).</summary>
    public const int MaxSupportedKeyLength = 50;

    /// <summary>The ordered plain alphabet (<c>ABC…Z</c>) for the "key" display.</summary>
    public string PlainAlphabet { get; } = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// Enciphers <paramref name="text"/> with <paramref name="key"/>: each plaintext letter is shifted
    /// forward by the position of the corresponding key letter. Case is preserved, non-letters pass
    /// through unchanged, and the key advances only on letters. A key with no letters returns the text
    /// unchanged; <see langword="null"/>/empty text returns <see cref="string.Empty"/>.
    /// </summary>
    public string Encode(string? text, string? key) => Crypt(text, key, decrypt: false);

    /// <summary>The inverse of <see cref="Encode"/>: shifts each ciphertext letter back by the key.</summary>
    public string Decode(string? text, string? key) => Crypt(text, key, decrypt: true);

    /// <summary>Uppercases <paramref name="key"/> and strips every non-letter — the canonical key the
    /// cipher actually cycles through (e.g. <c>"l E-m.o N" → "LEMON"</c>).</summary>
    public string NormalizeKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        Span<char> buffer = key.Length <= 256 ? stackalloc char[key.Length] : new char[key.Length];
        var count = 0;
        foreach (var c in key)
        {
            if (IsLetter(c))
            {
                buffer[count++] = ToUpper(c);
            }
        }

        return new string(buffer[..count]);
    }

    /// <summary>The substitution row for one key letter: the plain alphabet rotated by that letter's
    /// index (e.g. <c>'B' → BCD…ZA</c>). Pairing it with <see cref="PlainAlphabet"/> shows the mapping.</summary>
    public string CipherAlphabetForKeyLetter(char keyLetter)
    {
        if (!IsLetter(keyLetter))
        {
            return PlainAlphabet;
        }

        var shift = ToUpper(keyLetter) - 'A';
        return string.Create(AlphabetSize, shift, static (span, by) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = (char)('A' + (i + by) % AlphabetSize);
            }
        });
    }

    /// <summary>
    /// The Index of Coincidence of <paramref name="text"/> — the probability that two letters drawn at
    /// random (without replacement) are equal. Non-letters are ignored and case folded. English sits near
    /// <see cref="EnglishIndexOfCoincidence"/>; a polyalphabetic cipher trends toward
    /// <see cref="RandomIndexOfCoincidence"/>. Returns 0 for fewer than two letters.
    /// </summary>
    public double IndexOfCoincidence(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        Span<int> counts = stackalloc int[AlphabetSize];
        var n = CountLetters(text, counts);
        return IndexOfCoincidence(counts, n);
    }

    /// <summary>
    /// Estimates the most likely key length by trying every length in the requested range, splitting the
    /// ciphertext's letters into that many columns, and picking the length whose averaged per-column IC
    /// is closest to the target language. Returns the best length (1 if the text is too short to judge).
    /// </summary>
    public int EstimateKeyLength(
        string? cipherText,
        int maxKeyLength = 12,
        int minKeyLength = MinSupportedKeyLength,
        VigenereLanguageProfile? language = null)
    {
        var ranked = RankKeyLengths(cipherText, maxKeyLength, minKeyLength, language);
        return ranked.Count > 0 ? ranked[0].Length : 1;
    }

    /// <summary>
    /// Every candidate key length in <c>minKeyLength..maxKeyLength</c> scored by its averaged per-column
    /// Index of Coincidence, ordered best-first (nearest to the target language). The top entries are the
    /// true key length and its multiples. Empty when there are too few letters or the range is empty.
    /// </summary>
    public IReadOnlyList<VigenereKeyLengthCandidate> RankKeyLengths(
        string? cipherText,
        int maxKeyLength = 12,
        int minKeyLength = MinSupportedKeyLength,
        VigenereLanguageProfile? language = null)
    {
        var letters = ExtractLetters(cipherText);
        var target = (language ?? VigenereLanguageProfile.English).ExpectedIndexOfCoincidence;

        var low = Math.Max(MinSupportedKeyLength, minKeyLength);
        var high = Math.Min(Math.Min(maxKeyLength, MaxSupportedKeyLength), letters.Length);
        if (letters.Length < 2 || low > high)
        {
            return [];
        }

        var candidates = new List<VigenereKeyLengthCandidate>(high - low + 1);
        for (var length = low; length <= high; length++)
        {
            candidates.Add(new VigenereKeyLengthCandidate(length, AverageColumnIndexOfCoincidence(letters, length)));
        }

        // Closest averaged IC to the language wins; ties break toward the shorter (simpler) key.
        candidates.Sort((a, b) =>
        {
            var byCloseness = Math.Abs(a.IndexOfCoincidence - target)
                .CompareTo(Math.Abs(b.IndexOfCoincidence - target));
            return byCloseness != 0 ? byCloseness : a.Length.CompareTo(b.Length);
        });

        return candidates;
    }

    /// <summary>
    /// Assisted decode for an unknown key: estimates the key length via the Index of Coincidence, then
    /// recovers each key letter by chi-squared scoring of its column against the target language's letter
    /// frequencies, and decodes. Returns the derived key, the decoded text (original formatting
    /// preserved), the chosen key length, and the ranked length candidates. Too little text yields an
    /// empty key.
    /// </summary>
    public VigenereSolveResult Solve(
        string? cipherText,
        int maxKeyLength = 12,
        int minKeyLength = MinSupportedKeyLength,
        VigenereLanguageProfile? language = null)
    {
        var profile = language ?? VigenereLanguageProfile.English;
        var ranked = RankKeyLengths(cipherText, maxKeyLength, minKeyLength, profile);
        var letters = ExtractLetters(cipherText);

        if (ranked.Count == 0 || letters.Length == 0)
        {
            return new VigenereSolveResult(string.Empty, cipherText ?? string.Empty, 0, ranked);
        }

        // The length whose averaged per-column IC is closest to the language is the most likely key
        // length. The IC can't tell a key from its own repetition (a multiple length scores identically),
        // so fold the recovered key down to its fundamental period — KEYKEYKEY is reported as KEY.
        var keyLength = ranked[0].Length;
        var key = ReduceToFundamentalPeriod(RecoverKey(letters, keyLength, profile));
        var plain = Decode(cipherText, key);

        return new VigenereSolveResult(key, plain, key.Length, ranked);
    }

    private string Crypt(string? text, string? key, bool decrypt)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalizedKey = NormalizeKey(key);
        if (normalizedKey.Length == 0)
        {
            return text;
        }

        return string.Create(text.Length, (text, normalizedKey, decrypt), static (span, state) =>
        {
            var (source, k, dec) = state;
            var keyIndex = 0;
            for (var i = 0; i < span.Length; i++)
            {
                var c = source[i];
                if (!IsLetter(c))
                {
                    span[i] = c;
                    continue;
                }

                var shift = k[keyIndex % k.Length] - 'A';
                if (dec)
                {
                    shift = AlphabetSize - shift;
                }

                var baseChar = c is >= 'a' and <= 'z' ? 'a' : 'A';
                span[i] = (char)(baseChar + (c - baseChar + shift) % AlphabetSize);
                keyIndex++;
            }
        });
    }

    /// <summary>If <paramref name="key"/> is exactly a shorter string repeated (e.g. <c>"KEYKEY"</c>),
    /// returns that shortest repeating unit (<c>"KEY"</c>); otherwise returns the key unchanged.</summary>
    private static string ReduceToFundamentalPeriod(string key)
    {
        var n = key.Length;
        for (var period = 1; period < n; period++)
        {
            if (n % period != 0)
            {
                continue;
            }

            var repeats = true;
            for (var i = period; i < n && repeats; i++)
            {
                repeats = key[i] == key[i - period];
            }

            if (repeats)
            {
                return key[..period];
            }
        }

        return key;
    }

    /// <summary>Recovers the key for a known length: each column's shift is the one whose decoded letters
    /// best fit the target language by chi-squared.</summary>
    private static string RecoverKey(char[] letters, int keyLength, VigenereLanguageProfile language)
    {
        return string.Create(keyLength, (letters, keyLength, language.Weights), static (span, state) =>
        {
            var (text, length, weights) = state;
            for (var col = 0; col < length; col++)
            {
                span[col] = (char)('A' + BestShiftForColumn(text, col, length, weights));
            }
        });
    }

    /// <summary>The shift (0–25) that, when used to decode this column, yields the lowest chi-squared
    /// distance to the language's letter frequencies — i.e. the column's key letter index.</summary>
    private static int BestShiftForColumn(char[] letters, int column, int keyLength, double[] weights)
    {
        Span<int> counts = stackalloc int[AlphabetSize];
        var n = 0;
        for (var i = column; i < letters.Length; i += keyLength)
        {
            counts[letters[i] - 'A']++;
            n++;
        }

        if (n == 0)
        {
            return 0;
        }

        var bestShift = 0;
        var bestChi = double.MaxValue;
        for (var shift = 0; shift < AlphabetSize; shift++)
        {
            var chi = 0.0;
            for (var letter = 0; letter < AlphabetSize; letter++)
            {
                // Decoding this column by `shift` maps observed `letter` back to (letter - shift).
                var observed = counts[(letter + shift) % AlphabetSize];
                var expected = weights[letter] * n;
                var delta = observed - expected;
                chi += delta * delta / expected;
            }

            if (chi < bestChi)
            {
                bestChi = chi;
                bestShift = shift;
            }
        }

        return bestShift;
    }

    private static double AverageColumnIndexOfCoincidence(char[] letters, int keyLength)
    {
        Span<int> counts = stackalloc int[AlphabetSize];
        var sum = 0.0;
        for (var col = 0; col < keyLength; col++)
        {
            counts.Clear();
            var n = 0;
            for (var i = col; i < letters.Length; i += keyLength)
            {
                counts[letters[i] - 'A']++;
                n++;
            }

            sum += IndexOfCoincidence(counts, n);
        }

        return sum / keyLength;
    }

    private static double IndexOfCoincidence(ReadOnlySpan<int> counts, int n)
    {
        if (n < 2)
        {
            return 0;
        }

        long sum = 0;
        foreach (var count in counts)
        {
            sum += (long)count * (count - 1);
        }

        return (double)sum / ((long)n * (n - 1));
    }

    private static char[] ExtractLetters(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var buffer = new char[text.Length];
        var count = 0;
        foreach (var c in text)
        {
            if (IsLetter(c))
            {
                buffer[count++] = ToUpper(c);
            }
        }

        return count == buffer.Length ? buffer : buffer[..count];
    }

    private static int CountLetters(string text, Span<int> counts)
    {
        var n = 0;
        foreach (var c in text)
        {
            if (IsLetter(c))
            {
                counts[ToUpper(c) - 'A']++;
                n++;
            }
        }

        return n;
    }

    // ASCII-only A–Z so the transform is fully deterministic regardless of culture.
    private static bool IsLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static char ToUpper(char c) => c is >= 'a' and <= 'z' ? (char)(c - 32) : c;
}
