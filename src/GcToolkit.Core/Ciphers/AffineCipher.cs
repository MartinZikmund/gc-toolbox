namespace GcToolkit.Core.Ciphers;

/// <summary>One brute-force candidate: the key (<see cref="A"/>, <see cref="B"/>) tried and the resulting <see cref="Text"/>.</summary>
public readonly record struct AffineCandidate(int A, int B, string Text);

/// <summary>
/// A pure, stateless affine (monoalphabetic substitution) cipher over the 26 Latin letters A–Z
/// (A=0 … Z=25). Encoding is <c>E(x) = (a·x + b) mod 26</c>; decoding is
/// <c>D(y) = aⁱⁿᵛ·(y − b) mod 26</c> where <c>aⁱⁿᵛ</c> is the modular inverse of <c>a</c> mod 26.
/// The multiplier <c>a</c> must be coprime to 26 (one of <see cref="ValidMultipliers"/>) for the map
/// to be invertible. Letter case is preserved and every non-letter (spaces, digits, punctuation)
/// passes through unchanged. Beyond geocachingtoolbox.com parity, this codec exposes the modular
/// inverse, the substitution alphabet for a "key" display, and a full <see cref="BruteForce"/>
/// auto-solve over every valid key.
/// </summary>
public sealed class AffineCipher
{
    /// <summary>Number of symbols in the alphabet (A–Z).</summary>
    public const int AlphabetSize = 26;

    /// <summary>
    /// The 12 multipliers coprime to 26 (gcd(a, 26) == 1). Only these make the affine map invertible;
    /// the geocachingtoolbox.com tool offers exactly this set.
    /// </summary>
    public static readonly IReadOnlyList<int> ValidMultipliers = [1, 3, 5, 7, 9, 11, 15, 17, 19, 21, 23, 25];

    /// <summary>The largest offset <c>b</c> the UI offers (0–26; 26 ≡ 0 mod 26, mirroring the reference site).</summary>
    public const int MaxOffset = 26;

    /// <summary><see langword="true"/> when <paramref name="a"/> is coprime to 26 (gcd(a, 26) == 1), i.e. a valid multiplier.</summary>
    public bool IsCoprime(int a) => Gcd(Normalize(a), AlphabetSize) == 1;

    /// <summary>
    /// The modular multiplicative inverse of <paramref name="a"/> modulo 26 — the value <c>aⁱⁿᵛ</c>
    /// with <c>a·aⁱⁿᵛ ≡ 1 (mod 26)</c>, used to decode.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="a"/> is not coprime to 26, so no inverse exists.</exception>
    public int ModInverse(int a)
    {
        var normalized = Normalize(a);
        if (Gcd(normalized, AlphabetSize) != 1)
        {
            throw new ArgumentException($"Multiplier {a} is not coprime to {AlphabetSize}, so it has no modular inverse.", nameof(a));
        }

        for (var x = 1; x < AlphabetSize; x++)
        {
            if (normalized * x % AlphabetSize == 1)
            {
                return x;
            }
        }

        return 1; // Unreachable for coprime a; satisfies the compiler.
    }

    /// <summary>
    /// Encodes (<paramref name="decrypt"/> = <see langword="false"/>) or decodes
    /// (<paramref name="decrypt"/> = <see langword="true"/>) <paramref name="text"/> with the affine key
    /// (<paramref name="a"/>, <paramref name="b"/>). Letters keep their case; non-letters pass through.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the transformed text.</returns>
    /// <exception cref="ArgumentException"><paramref name="a"/> is not coprime to 26.</exception>
    public string Transform(string? text, int a, int b, bool decrypt)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var an = Normalize(a);
        var bn = Normalize(b);

        // Decoding multiplies by the inverse and shifts by −b; both reduce to a single (mul·x + add) map.
        var (mul, add) = decrypt
            ? (ModInverse(an), Normalize(-ModInverse(an) * bn))
            : (an, bn);

        return string.Create(text.Length, (text, mul, add), static (span, state) =>
        {
            var (source, mul, add) = state;
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = MapChar(source[i], mul, add);
            }
        });
    }

    /// <summary>
    /// The substitution row for a "key" display: the plain alphabet A–Z encoded with the key
    /// (<paramref name="a"/>, <paramref name="b"/>). Pairing this with the plain alphabet shows exactly
    /// how each letter maps. Returned upper-case.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="a"/> is not coprime to 26.</exception>
    public string SubstitutionAlphabet(int a, int b)
    {
        var an = Normalize(a);
        var bn = Normalize(b);
        if (Gcd(an, AlphabetSize) != 1)
        {
            throw new ArgumentException($"Multiplier {a} is not coprime to {AlphabetSize}.", nameof(a));
        }

        return string.Create(AlphabetSize, (an, bn), static (span, state) =>
        {
            var (mul, add) = state;
            for (var x = 0; x < span.Length; x++)
            {
                span[x] = (char)('A' + (mul * x + add) % AlphabetSize);
            }
        });
    }

    /// <summary>
    /// Every candidate decryption of <paramref name="text"/> across all valid keys — each of the 12
    /// coprime multipliers paired with every offset 0–26 — i.e. <c>12 × 27 = 324</c> rows, for
    /// brute-forcing an unknown affine key (the beyond-parity auto-solve). Decodes with each key.
    /// </summary>
    public IReadOnlyList<AffineCandidate> BruteForce(string? text)
    {
        var results = new List<AffineCandidate>(ValidMultipliers.Count * (MaxOffset + 1));
        foreach (var a in ValidMultipliers)
        {
            for (var b = 0; b <= MaxOffset; b++)
            {
                results.Add(new AffineCandidate(a, b, Transform(text, a, b, decrypt: true)));
            }
        }

        return results;
    }

    private static char MapChar(char c, int mul, int add)
    {
        if (c is >= 'A' and <= 'Z')
        {
            return (char)('A' + (mul * (c - 'A') + add) % AlphabetSize);
        }

        if (c is >= 'a' and <= 'z')
        {
            return (char)('a' + (mul * (c - 'a') + add) % AlphabetSize);
        }

        return c;
    }

    /// <summary>Reduces an arbitrary signed value into <c>[0, 26)</c>.</summary>
    private static int Normalize(int value) => ((value % AlphabetSize) + AlphabetSize) % AlphabetSize;

    private static int Gcd(int x, int y)
    {
        while (y != 0)
        {
            (x, y) = (y, x % y);
        }

        return x;
    }
}
