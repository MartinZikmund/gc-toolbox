using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Text;

/// <summary>How the per-character frequency table is ordered.</summary>
public enum FrequencySort
{
    /// <summary>By character (A, B, C …).</summary>
    Alphabetical,

    /// <summary>Highest count first — the codebreaker's default view.</summary>
    MostCommonFirst,

    /// <summary>Lowest count first.</summary>
    LeastCommonFirst,
}

/// <summary>Which characters are counted in the frequency table and n-grams.</summary>
public enum FrequencyScope
{
    /// <summary>Every non-whitespace character (letters, digits, symbols).</summary>
    All,

    /// <summary>Letters only.</summary>
    LettersOnly,

    /// <summary>Digits only.</summary>
    DigitsOnly,

    /// <summary>Symbols/punctuation only.</summary>
    SymbolsOnly,
}

/// <summary>Tuning knobs for a single analysis run.</summary>
public sealed record FrequencyOptions
{
    /// <summary>When <see langword="false"/> (default) <c>A</c> and <c>a</c> fold together.</summary>
    public bool CaseSensitive { get; init; }

    /// <summary>Ordering of the character frequency table.</summary>
    public FrequencySort Sort { get; init; } = FrequencySort.MostCommonFirst;

    /// <summary>Which character classes feed the frequency table and n-grams.</summary>
    public FrequencyScope Scope { get; init; } = FrequencyScope.All;

    /// <summary>Fold accented letters to their base letter (é → e) before counting.</summary>
    public bool StripAccents { get; init; }
}

/// <summary>One row of the character (or n-gram, or word) frequency table.</summary>
/// <param name="Display">The character/sequence shown to the user.</param>
/// <param name="Count">How many times it occurs.</param>
/// <param name="Percentage">Its share of the counted total, 0..100.</param>
public readonly record struct FrequencyEntry(string Display, int Count, double Percentage);

/// <summary>The category totals reported above the frequency table.</summary>
public readonly record struct FrequencyTotals(
    int Characters,
    int Letters,
    int Digits,
    int Symbols,
    int Spaces,
    int Words,
    int Lines,
    int UniqueCharacters);

/// <summary>One suggested cipher→plain letter substitution, by frequency rank alignment.</summary>
/// <param name="Cipher">The observed (ciphertext) letter.</param>
/// <param name="Plain">The guessed plaintext letter (English frequency order).</param>
public readonly record struct SubstitutionGuess(char Cipher, char Plain);

/// <summary>The full, immutable result of analysing one block of text.</summary>
public sealed class FrequencyResult
{
    internal FrequencyResult(
        FrequencyTotals totals,
        IReadOnlyList<FrequencyEntry> characterFrequencies,
        IReadOnlyList<FrequencyEntry> bigrams,
        IReadOnlyList<FrequencyEntry> trigrams,
        IReadOnlyList<FrequencyEntry> words,
        double indexOfCoincidence,
        int estimatedKeyLength,
        IReadOnlyList<SubstitutionGuess> suggestedMapping)
    {
        Totals = totals;
        CharacterFrequencies = characterFrequencies;
        Bigrams = bigrams;
        Trigrams = trigrams;
        Words = words;
        IndexOfCoincidence = indexOfCoincidence;
        EstimatedKeyLength = estimatedKeyLength;
        SuggestedMapping = suggestedMapping;
    }

    public FrequencyTotals Totals { get; }

    public IReadOnlyList<FrequencyEntry> CharacterFrequencies { get; }

    public IReadOnlyList<FrequencyEntry> Bigrams { get; }

    public IReadOnlyList<FrequencyEntry> Trigrams { get; }

    public IReadOnlyList<FrequencyEntry> Words { get; }

    /// <summary>Index of Coincidence over the counted letters (0 = uniform, ~0.067 = English).</summary>
    public double IndexOfCoincidence { get; }

    /// <summary>Friedman key-length estimate (1 ≈ monoalphabetic; 0 when there are no letters).</summary>
    public int EstimatedKeyLength { get; }

    /// <summary>First-guess monoalphabetic key: observed order aligned to English frequency order.</summary>
    public IReadOnlyList<SubstitutionGuess> SuggestedMapping { get; }

    /// <summary>The character frequency table as CSV (header + one row per character).</summary>
    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.Append("Character,Count,Percentage\n");
        foreach (var entry in CharacterFrequencies)
        {
            var display = entry.Display.Contains(',') || entry.Display.Contains('"')
                ? $"\"{entry.Display.Replace("\"", "\"\"")}\""
                : entry.Display;
            sb.Append(display).Append(',')
              .Append(entry.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(entry.Percentage.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
        }

        return sb.ToString();
    }
}

/// <summary>
/// Pure, deterministic frequency analyser — the codebreaking workhorse behind the Frequency Analysis
/// tool (issue #182). Counts category totals, builds a per-character frequency table (case-folded by
/// default, with sort/scope options), extracts bigrams/trigrams/configurable n-grams and whole-word
/// frequencies, and computes the Index of Coincidence, a Friedman key-length estimate, and a suggested
/// monoalphabetic mapping. Goes well beyond the single-character counts of cachesleuth.com.
/// </summary>
public sealed class FrequencyAnalyzer
{
    /// <summary>Expected English IoC used by the Friedman key-length estimate.</summary>
    private const double EnglishIoc = 0.0667;

    /// <summary>Expected IoC of a uniform 26-letter distribution.</summary>
    private const double RandomIoc = 0.0385;

    /// <summary>Runs a full analysis of <paramref name="text"/> with default options.</summary>
    public FrequencyResult Analyze(string? text) => Analyze(text, new FrequencyOptions());

    /// <summary>Runs a full analysis of <paramref name="text"/> with the supplied <paramref name="options"/>.</summary>
    public FrequencyResult Analyze(string? text, FrequencyOptions options)
    {
        text ??= string.Empty;

        var totals = ComputeTotals(text, options);
        var characterFrequencies = BuildCharacterFrequencies(text, options);
        var prepared = PrepareForGrams(text, options);
        var bigrams = NGramsFromTokens(prepared, 2);
        var trigrams = NGramsFromTokens(prepared, 3);
        var words = BuildWordFrequencies(text, options);
        var letterCounts = LetterCounts(text, options.StripAccents);
        var ioc = ComputeIoC(letterCounts);
        var keyLength = EstimateKeyLength(letterCounts, ioc);
        var mapping = BuildSuggestedMapping(letterCounts);

        return new FrequencyResult(totals, characterFrequencies, bigrams, trigrams, words, ioc, keyLength, mapping);
    }

    /// <summary>Standalone n-gram extraction for any window size <paramref name="n"/> (≥ 1), default options.</summary>
    public IReadOnlyList<FrequencyEntry> NGrams(string? text, int n)
        => NGramsFromTokens(PrepareForGrams(text ?? string.Empty, new FrequencyOptions()), n);

    // ---- Totals ----

    private static FrequencyTotals ComputeTotals(string text, FrequencyOptions options)
    {
        var letters = 0;
        var digits = 0;
        var symbols = 0;
        var spaces = 0;
        var unique = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                if (rune.Value != '\n' && rune.Value != '\r')
                {
                    spaces++;
                }

                continue;
            }

            if (Rune.IsLetter(rune))
            {
                letters++;
            }
            else if (Rune.IsDigit(rune))
            {
                digits++;
            }
            else
            {
                symbols++;
            }

            unique.Add(NormalizeKey(rune, options));
        }

        return new FrequencyTotals(
            Characters: letters + digits + symbols,
            Letters: letters,
            Digits: digits,
            Symbols: symbols,
            Spaces: spaces,
            Words: CountWords(text),
            Lines: CountLines(text),
            UniqueCharacters: unique.Count);
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    // ---- Per-character frequencies ----

    private IReadOnlyList<FrequencyEntry> BuildCharacterFrequencies(string text, FrequencyOptions options)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune) || !InScope(rune, options.Scope))
            {
                continue;
            }

            var key = NormalizeKey(rune, options);
            counts[key] = counts.GetValueOrDefault(key) + 1;
            total++;
        }

        return BuildSortedEntries(counts, total, options.Sort);
    }

    private static IReadOnlyList<FrequencyEntry> BuildSortedEntries(
        Dictionary<string, int> counts, int total, FrequencySort sort)
    {
        if (counts.Count == 0)
        {
            return [];
        }

        var entries = counts.Select(kvp =>
            new FrequencyEntry(kvp.Key, kvp.Value, total == 0 ? 0 : kvp.Value * 100.0 / total));

        // Ties always resolve alphabetically so the output is stable.
        IEnumerable<FrequencyEntry> ordered = sort switch
        {
            FrequencySort.MostCommonFirst => entries
                .OrderByDescending(e => e.Count)
                .ThenBy(e => e.Display, StringComparer.Ordinal),
            FrequencySort.LeastCommonFirst => entries
                .OrderBy(e => e.Count)
                .ThenBy(e => e.Display, StringComparer.Ordinal),
            _ => entries.OrderBy(e => e.Display, StringComparer.Ordinal),
        };

        return [.. ordered];
    }

    // ---- N-grams ----

    /// <summary>
    /// Splits the text into in-scope token strings (one per run of non-whitespace, scope-filtered,
    /// case-folded/accent-stripped per options) so n-grams never span a whitespace boundary.
    /// </summary>
    private static IReadOnlyList<string> PrepareForGrams(string text, FrequencyOptions options)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                Flush();
                continue;
            }

            if (!InScope(rune, options.Scope))
            {
                Flush();
                continue;
            }

            current.Append(NormalizeKey(rune, options));
        }

        Flush();
        return tokens;
    }

    private static IReadOnlyList<FrequencyEntry> NGramsFromTokens(IReadOnlyList<string> tokens, int n)
    {
        if (n < 1)
        {
            return [];
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = 0;

        foreach (var token in tokens)
        {
            for (var i = 0; i + n <= token.Length; i++)
            {
                var gram = token.Substring(i, n);
                counts[gram] = counts.GetValueOrDefault(gram) + 1;
                total++;
            }
        }

        return BuildSortedEntries(counts, total, FrequencySort.MostCommonFirst);
    }

    private static IReadOnlyList<FrequencyEntry> BuildWordFrequencies(string text, FrequencyOptions options)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = 0;

        foreach (var raw in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var word = options.StripAccents ? RemoveAccents(raw) : raw;
            if (!options.CaseSensitive)
            {
                word = word.ToLowerInvariant();
            }

            counts[word] = counts.GetValueOrDefault(word) + 1;
            total++;
        }

        return BuildSortedEntries(counts, total, FrequencySort.MostCommonFirst);
    }

    // ---- Index of Coincidence & key length ----

    private static double ComputeIoC(IReadOnlyDictionary<char, int> letterCounts)
    {
        var n = letterCounts.Values.Sum();
        if (n < 2)
        {
            return n == 1 ? 1.0 : 0.0;
        }

        double sum = letterCounts.Values.Sum(c => (long)c * (c - 1));
        return sum / ((double)n * (n - 1));
    }

    private static int EstimateKeyLength(IReadOnlyDictionary<char, int> letterCounts, double ioc)
    {
        var n = letterCounts.Values.Sum();
        if (n == 0)
        {
            return 0;
        }

        if (ioc <= RandomIoc || ioc <= 0)
        {
            // Flat distribution: assume a long polyalphabetic key; cap at the sample size.
            return Math.Max(1, Math.Min(n, 20));
        }

        // Friedman estimate: k = (0.0667 - 0.0385) / (IoC - 0.0385).
        var denominator = ioc - RandomIoc;
        var estimate = (EnglishIoc - RandomIoc) / denominator;
        return Math.Max(1, (int)Math.Round(estimate, MidpointRounding.AwayFromZero));
    }

    // ---- Suggested mapping ----

    private static IReadOnlyList<SubstitutionGuess> BuildSuggestedMapping(IReadOnlyDictionary<char, int> letterCounts)
    {
        if (letterCounts.Count == 0)
        {
            return [];
        }

        // Observed letters most-common first (ties alphabetical), aligned to the English order.
        var observed = letterCounts
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .Select(kvp => kvp.Key)
            .ToList();

        var result = new List<SubstitutionGuess>(observed.Count);
        for (var i = 0; i < observed.Count && i < FrequencyTables.EnglishOrder.Length; i++)
        {
            result.Add(new SubstitutionGuess(observed[i], FrequencyTables.EnglishOrder[i]));
        }

        return result;
    }

    // ---- Helpers ----

    /// <summary>Per-letter A–Z counts after case folding and (optional) accent stripping; the IoC basis.</summary>
    private static IReadOnlyDictionary<char, int> LetterCounts(string text, bool stripAccents)
    {
        var counts = new Dictionary<char, int>();
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
            {
                continue;
            }

            var folded = Fold(rune, stripAccents);
            if (folded is >= 'A' and <= 'Z')
            {
                counts[folded] = counts.GetValueOrDefault(folded) + 1;
            }
        }

        return counts;
    }

    /// <summary>Folds a letter rune to a single upper-case A–Z char for IoC/mapping, or '\0' if not mappable.</summary>
    private static char Fold(Rune rune, bool stripAccents)
    {
        var upper = Rune.ToUpperInvariant(rune);
        if (upper.Value is >= 'A' and <= 'Z')
        {
            return (char)upper.Value;
        }

        if (stripAccents)
        {
            var stripped = RemoveAccents(upper.ToString());
            if (stripped.Length == 1 && stripped[0] is >= 'A' and <= 'Z')
            {
                return stripped[0];
            }
        }

        return '\0';
    }

    /// <summary>The display/grouping key for a rune under the current case/accent options.</summary>
    private static string NormalizeKey(Rune rune, FrequencyOptions options)
    {
        var working = rune;
        if (options.StripAccents && Rune.IsLetter(rune))
        {
            var stripped = RemoveAccents(rune.ToString());
            if (stripped.Length > 0)
            {
                working = stripped.EnumerateRunes().First();
            }
        }

        if (!options.CaseSensitive)
        {
            working = Rune.ToUpperInvariant(working);
        }

        return working.ToString();
    }

    private static bool InScope(Rune rune, FrequencyScope scope) => scope switch
    {
        FrequencyScope.LettersOnly => Rune.IsLetter(rune),
        FrequencyScope.DigitsOnly => Rune.IsDigit(rune),
        FrequencyScope.SymbolsOnly => !Rune.IsLetter(rune) && !Rune.IsDigit(rune),
        _ => true,
    };

    private static string RemoveAccents(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
