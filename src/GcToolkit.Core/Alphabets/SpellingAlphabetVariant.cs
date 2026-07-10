using System.Diagnostics.CodeAnalysis;

namespace GcToolkit.Core.Alphabets;

/// <summary>One letter and its spoken code word in a spelling alphabet (e.g. <c>A → Alfa</c>).</summary>
public sealed record SpellingAlphabetEntry(char Letter, string Word);

/// <summary>
/// A single spelling-alphabet variant: an ordered letter → code-word chart plus the decode aliases
/// (alternative spellings such as Alpha/Alfa or Juliet/Juliett) that map back to a letter. Variants are
/// pure data so the tables can be asserted directly in unit tests. <see cref="Id"/> is also the
/// localization key for the variant's display name (<c>SpellingAlphabetVariant_&lt;Id&gt;</c>).
/// </summary>
public sealed class SpellingAlphabetVariant
{
    private readonly Dictionary<char, string> _byLetter;
    private readonly Dictionary<string, char> _byWord;

    public SpellingAlphabetVariant(
        string id,
        bool isCyrillic,
        IReadOnlyList<SpellingAlphabetEntry> chart,
        IReadOnlyDictionary<string, char>? aliases = null)
    {
        Id = id;
        IsCyrillic = isCyrillic;
        Chart = chart;

        _byLetter = chart.ToDictionary(e => e.Letter, e => e.Word);
        _byWord = new(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in chart)
        {
            // Each code word is unique within a variant; TryAdd is just defensive against a future duplicate.
            _byWord.TryAdd(Normalize(entry.Word), entry.Letter);
        }

        if (aliases is not null)
        {
            foreach (var (alias, letter) in aliases)
            {
                _byWord[Normalize(alias)] = letter;
            }
        }
    }

    /// <summary>Stable identifier and localization key suffix.</summary>
    public string Id { get; }

    /// <summary><see langword="true"/> for the Cyrillic Russian variants (different base alphabet).</summary>
    public bool IsCyrillic { get; }

    /// <summary>The ordered letter → code-word chart, for display and table assertions.</summary>
    public IReadOnlyList<SpellingAlphabetEntry> Chart { get; }

    /// <summary>The letters covered by this variant, in chart order.</summary>
    public IEnumerable<char> Keys => Chart.Select(e => e.Letter);

    public bool TryGetWord(char letter, [NotNullWhen(true)] out string? word)
        => _byLetter.TryGetValue(char.ToUpperInvariant(letter), out word);

    /// <summary>Resolves a code word (or known alias) back to its letter, ignoring case and punctuation.</summary>
    public bool TryGetLetter(string word, out char letter)
        => _byWord.TryGetValue(Normalize(word), out letter);

    /// <summary>Folds a word for comparison: trims, upper-cases, and drops hyphens/spaces (X-ray ≡ Xray).</summary>
    internal static string Normalize(string word)
    {
        Span<char> buffer = stackalloc char[word.Length];
        var length = 0;
        foreach (var c in word)
        {
            if (c is '-' or ' ' or '\'' or '.')
            {
                continue;
            }

            buffer[length++] = char.ToUpperInvariant(c);
        }

        return new string(buffer[..length]);
    }
}
