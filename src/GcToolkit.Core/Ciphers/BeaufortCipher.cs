namespace GcToolkit.Core.Ciphers;

/// <summary>The three Beaufort flavours this codec supports.</summary>
public enum BeaufortVariant
{
    /// <summary>Classic Beaufort: <c>C = (K - P) mod N</c>. Self-reciprocal — the same op enciphers and deciphers.</summary>
    Standard,

    /// <summary>Variant / German Beaufort: encrypt <c>C = (P - K) mod N</c>, decrypt <c>P = (C + K) mod N</c>. Not reciprocal.</summary>
    Variant,

    /// <summary>Autokey Beaufort: the plaintext (encrypt) or recovered plaintext (decrypt) extends the running key.</summary>
    Autokey,
}

/// <summary>
/// A pure, stateless Beaufort cipher — a reciprocal "reversed Vigenère". Supports the classic
/// <see cref="BeaufortVariant.Standard"/> (<c>C = K - P</c>, self-reciprocal), the
/// <see cref="BeaufortVariant.Variant"/> "German" form (<c>C = P - K</c>), and
/// <see cref="BeaufortVariant.Autokey"/> mode where the message itself extends the key. Uses a
/// repeating, case-insensitive keyword; non-alphabet characters pass through unchanged and letter
/// case is preserved. An optional custom alphabet replaces the default A–Z, and the tabula recta
/// can be exposed for display.
/// </summary>
public sealed class BeaufortCipher
{
    /// <summary>The default cipher alphabet (A–Z).</summary>
    public const string DefaultAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly string _alphabet;
    private readonly Dictionary<char, int> _index;

    /// <summary>Creates a cipher over <paramref name="alphabet"/> (defaults to A–Z). Matching is case-insensitive.</summary>
    /// <exception cref="ArgumentException">The alphabet is empty or contains a duplicate letter (case-insensitively).</exception>
    public BeaufortCipher(string? alphabet = null)
    {
        var upper = (alphabet ?? DefaultAlphabet).ToUpperInvariant();
        if (upper.Length == 0)
        {
            throw new ArgumentException("Alphabet must not be empty.", nameof(alphabet));
        }

        _index = new Dictionary<char, int>(upper.Length);
        for (var i = 0; i < upper.Length; i++)
        {
            if (!_index.TryAdd(upper[i], i))
            {
                throw new ArgumentException($"Alphabet contains a duplicate letter '{upper[i]}'.", nameof(alphabet));
            }
        }

        _alphabet = upper;
    }

    /// <summary>The (upper-case) cipher alphabet in use.</summary>
    public string Alphabet => _alphabet;

    /// <summary>The number of symbols in the active alphabet.</summary>
    public int Size => _alphabet.Length;

    /// <summary>
    /// Enciphers <paramref name="text"/> with <paramref name="keyword"/> under <paramref name="variant"/>.
    /// Non-alphabet characters pass through and case is preserved.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="keyword"/> is empty or has a character outside the alphabet.</exception>
    public string Encrypt(string? text, string keyword, BeaufortVariant variant = BeaufortVariant.Standard)
        => Transform(text, keyword, variant, decrypt: false);

    /// <summary>
    /// Deciphers <paramref name="text"/> with <paramref name="keyword"/> under <paramref name="variant"/>.
    /// For <see cref="BeaufortVariant.Standard"/> this is identical to <see cref="Encrypt"/> (reciprocal).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="keyword"/> is empty or has a character outside the alphabet.</exception>
    public string Decrypt(string? text, string keyword, BeaufortVariant variant = BeaufortVariant.Standard)
        => Transform(text, keyword, variant, decrypt: true);

    private string Transform(string? text, string keyword, BeaufortVariant variant, bool decrypt)
    {
        var keyIndices = NormalizeKeyword(keyword);

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        // Running key positions; for autokey these get appended with message symbols as we go.
        List<int> stream = [.. keyIndices];
        var keyPos = 0;

        foreach (var ch in text)
        {
            if (!TryIndexOf(ch, out var p, out var wasLower))
            {
                builder.Append(ch);
                continue;
            }

            var k = stream[keyPos % stream.Count];
            var c = variant switch
            {
                BeaufortVariant.Variant => decrypt ? Mod(p + k) : Mod(p - k),
                _ => Mod(k - p), // Standard and Autokey share the K - P core.
            };

            builder.Append(ToCase(_alphabet[c], wasLower));

            // Autokey extends the running key with each consumed message symbol:
            // encrypt feeds the plaintext (p); decrypt feeds the just-recovered plaintext (c).
            if (variant == BeaufortVariant.Autokey)
            {
                stream.Add(decrypt ? c : p);
            }

            keyPos++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// The tabula recta as a square: row <c>r</c> is the alphabet rotated left by <c>r</c>, so
    /// <c>Tableau[k][p]</c> is the Standard-Beaufort cipher letter for plaintext <c>p</c> under key <c>k</c>.
    /// </summary>
    public IReadOnlyList<string> Tableau()
    {
        var rows = new string[Size];
        for (var r = 0; r < Size; r++)
        {
            rows[r] = string.Create(Size, r, (span, row) =>
            {
                for (var col = 0; col < span.Length; col++)
                {
                    span[col] = _alphabet[(row + col) % span.Length];
                }
            });
        }

        return rows;
    }

    /// <summary>Whether <paramref name="keyword"/> is non-empty and every letter is in the alphabet.</summary>
    public bool IsValidKeyword(string? keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return false;
        }

        foreach (var ch in keyword)
        {
            if (!_index.ContainsKey(char.ToUpperInvariant(ch)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Maps a keyword to alphabet indices (case-insensitive), validating each letter.</summary>
    private int[] NormalizeKeyword(string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            throw new ArgumentException("Keyword must not be empty.", nameof(keyword));
        }

        var indices = new int[keyword.Length];
        for (var i = 0; i < keyword.Length; i++)
        {
            if (!_index.TryGetValue(char.ToUpperInvariant(keyword[i]), out var idx))
            {
                throw new ArgumentException($"Keyword character '{keyword[i]}' is not in the alphabet.", nameof(keyword));
            }

            indices[i] = idx;
        }

        return indices;
    }

    private bool TryIndexOf(char ch, out int index, out bool wasLower)
    {
        var upper = char.ToUpperInvariant(ch);
        if (_index.TryGetValue(upper, out index))
        {
            wasLower = char.IsLower(ch) && upper != ch;
            return true;
        }

        wasLower = false;
        return false;
    }

    private static char ToCase(char upper, bool lower) => lower ? char.ToLowerInvariant(upper) : upper;

    private int Mod(int value) => ((value % Size) + Size) % Size;
}
