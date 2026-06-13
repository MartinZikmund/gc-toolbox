namespace GcToolkit.Core.Ciphers;

/// <summary>One letter pair of the Kamasutra key — <see cref="First"/> and <see cref="Second"/> swap.</summary>
public readonly record struct KamasutraPair(char First, char Second);

/// <summary>Why a Kamasutra key failed validation (see <see cref="KamasutraValidationResult"/>).</summary>
public enum KamasutraValidationError
{
    /// <summary>The key is well-formed.</summary>
    None,

    /// <summary>A pair contains a non-letter character (digit, punctuation, …).</summary>
    InvalidCharacter,

    /// <summary>A dangling letter has no partner (the parsed letters did not pair up evenly).</summary>
    OddLetterCount,

    /// <summary>A letter appears in more than one pair (or is paired with itself).</summary>
    DuplicateLetter,
}

/// <summary>The outcome of validating a Kamasutra key, with the offending pair for inline reporting.</summary>
/// <param name="Error">The failure reason, or <see cref="KamasutraValidationError.None"/> when valid.</param>
/// <param name="OffendingPair">The two-letter pair that triggered the failure (e.g. <c>"AC"</c>), or <see langword="null"/>.</param>
public readonly record struct KamasutraValidationResult(KamasutraValidationError Error, string? OffendingPair)
{
    /// <summary>A passing result.</summary>
    public static readonly KamasutraValidationResult Valid = new(KamasutraValidationError.None, null);

    /// <summary><see langword="true"/> when the key is well-formed.</summary>
    public bool IsValid => Error == KamasutraValidationError.None;
}

/// <summary>
/// A pure, stateless Kamasutra (Vatsyayana) cipher — a reciprocal substitution. The alphabet is split
/// into letter pairs and each letter is swapped with its partner, so the same key both encodes and
/// decodes (an involution; one <see cref="Transform"/> direction). Unpaired characters — digits,
/// punctuation, spaces and letters absent from the key — pass through unchanged and case is preserved.
/// This is the single source of truth for the transform (thin-VM convention).
/// </summary>
public sealed class KamasutraCipher
{
    /// <summary>Number of letters in the Latin alphabet.</summary>
    public const int AlphabetSize = 26;

    /// <summary>Number of pairs in a complete A–Z key (26 letters / 2).</summary>
    public const int CompletePairCount = AlphabetSize / 2;

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>Sentinel partner for a dangling, unpaired letter produced by <see cref="ParsePairs"/>.</summary>
    private const char NoPartner = '\0';

    /// <summary>
    /// Swaps every paired letter in <paramref name="text"/> with its partner. Letters not present in
    /// <paramref name="pairs"/> and all non-letter characters pass through unchanged; case is preserved.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the transformed text.</returns>
    public string Transform(string? text, IReadOnlyList<KamasutraPair> pairs)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var map = BuildMap(pairs);

        return string.Create(text.Length, (text, map), static (span, state) =>
        {
            var (source, swap) = state;
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = SwapChar(source[i], swap);
            }
        });
    }

    /// <summary>
    /// The classic Kamasutra key: A–Z split into two 13-letter rows so the first half pairs with the
    /// second — A/N, B/O, … M/Z.
    /// </summary>
    public IReadOnlyList<KamasutraPair> BuildDefaultPairs() => PairColumns(Alphabet);

    /// <summary>
    /// A keyword-seeded key: dedupe <paramref name="keyword"/> (case-insensitive, letters only), append
    /// the remaining A–Z letters, then split the resulting 26-letter alphabet into two 13-letter rows and
    /// pair column-wise. An empty keyword yields <see cref="BuildDefaultPairs"/>.
    /// </summary>
    public IReadOnlyList<KamasutraPair> BuildFromKeyword(string? keyword) => PairColumns(SeedAlphabet(keyword));

    /// <summary>A random complete key (13 valid pairs) drawn from a shuffled A–Z.</summary>
    public IReadOnlyList<KamasutraPair> BuildRandomPairs() => BuildRandomPairs(Random.Shared);

    /// <summary>A random complete key using the supplied <paramref name="random"/> (deterministic for tests).</summary>
    public IReadOnlyList<KamasutraPair> BuildRandomPairs(Random random)
    {
        var letters = Alphabet.ToCharArray();
        for (var i = letters.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (letters[i], letters[j]) = (letters[j], letters[i]);
        }

        return PairColumns(new string(letters));
    }

    /// <summary>
    /// Leniently parses a user-entered pair list: letters are read in order (case-insensitive) and grouped
    /// two-by-two, ignoring spaces, commas, newlines and any other separators. A trailing odd letter is kept
    /// as a self-paired entry so <see cref="Validate"/> can report it as <see cref="KamasutraValidationError.OddLetterCount"/>.
    /// </summary>
    public IReadOnlyList<KamasutraPair> ParsePairs(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var letters = new List<char>(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                letters.Add(char.ToUpperInvariant(c));
            }
        }

        var pairs = new List<KamasutraPair>(letters.Count / 2 + 1);
        for (var i = 0; i < letters.Count; i += 2)
        {
            // A dangling final letter gets the '\0' "no partner" marker -> flagged as OddLetterCount.
            var second = i + 1 < letters.Count ? letters[i + 1] : NoPartner;
            pairs.Add(new KamasutraPair(letters[i], second));
        }

        return pairs;
    }

    /// <summary>
    /// Validates a key: every character must be a letter, every letter must appear at most once across all
    /// pairs (no letter paired with itself), and the parsed letters must pair up evenly. Returns the first
    /// failure with the offending pair, or <see cref="KamasutraValidationResult.Valid"/>.
    /// </summary>
    public KamasutraValidationResult Validate(IReadOnlyList<KamasutraPair> pairs)
    {
        var seen = new HashSet<char>();
        foreach (var pair in pairs)
        {
            // A '\0' marker means ParsePairs left a dangling letter with no partner.
            if (pair.Second == NoPartner || pair.First == NoPartner)
            {
                return new KamasutraValidationResult(KamasutraValidationError.OddLetterCount, PairText(pair));
            }

            if (!IsLetter(pair.First) || !IsLetter(pair.Second))
            {
                return new KamasutraValidationResult(KamasutraValidationError.InvalidCharacter, PairText(pair));
            }

            var first = char.ToUpperInvariant(pair.First);
            var second = char.ToUpperInvariant(pair.Second);

            // The same letter twice in one pair is a duplicate, not an involution.
            if (first == second || !seen.Add(first) || !seen.Add(second))
            {
                return new KamasutraValidationResult(KamasutraValidationError.DuplicateLetter, PairText(pair));
            }
        }

        return KamasutraValidationResult.Valid;
    }

    /// <summary>Renders the key as space-separated upper-case two-letter groups (e.g. <c>"AN BO …"</c>).</summary>
    public string FormatPairs(IReadOnlyList<KamasutraPair> pairs)
        => string.Join(' ', pairs.Select(PairText));

    private static string PairText(KamasutraPair pair)
    {
        var first = pair.First == NoPartner ? string.Empty : char.ToUpperInvariant(pair.First).ToString();
        var second = pair.Second == NoPartner ? string.Empty : char.ToUpperInvariant(pair.Second).ToString();
        return first + second;
    }

    private static bool IsLetter(char c) => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    /// <summary>Deduped keyword (letters only, case-insensitive) followed by the remaining A–Z letters.</summary>
    private static string SeedAlphabet(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Alphabet;
        }

        var seen = new HashSet<char>();
        var sb = new System.Text.StringBuilder(AlphabetSize);
        foreach (var c in keyword)
        {
            if (IsLetter(c))
            {
                var upper = char.ToUpperInvariant(c);
                if (seen.Add(upper))
                {
                    sb.Append(upper);
                }
            }
        }

        foreach (var c in Alphabet)
        {
            if (seen.Add(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>Splits a 26-letter alphabet into two 13-letter rows and pairs them column-wise.</summary>
    private static IReadOnlyList<KamasutraPair> PairColumns(string alphabet)
    {
        var pairs = new List<KamasutraPair>(CompletePairCount);
        for (var i = 0; i < CompletePairCount; i++)
        {
            pairs.Add(new KamasutraPair(alphabet[i], alphabet[i + CompletePairCount]));
        }

        return pairs;
    }

    /// <summary>Builds a case-aware swap lookup from the (validated-or-not) pair list.</summary>
    private static Dictionary<char, char> BuildMap(IReadOnlyList<KamasutraPair> pairs)
    {
        var map = new Dictionary<char, char>(pairs.Count * 4);
        foreach (var pair in pairs)
        {
            if (!IsLetter(pair.First) || !IsLetter(pair.Second))
            {
                continue;
            }

            AddSwap(map, pair.First, pair.Second);
            AddSwap(map, pair.Second, pair.First);
        }

        return map;
    }

    private static void AddSwap(Dictionary<char, char> map, char from, char to)
    {
        var upperFrom = char.ToUpperInvariant(from);
        var lowerFrom = char.ToLowerInvariant(from);
        map.TryAdd(upperFrom, char.ToUpperInvariant(to));
        map.TryAdd(lowerFrom, char.ToLowerInvariant(to));
    }

    private static char SwapChar(char c, Dictionary<char, char> map)
        => map.TryGetValue(c, out var swapped) ? swapped : c;
}
