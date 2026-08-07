namespace GcToolkit.Core.Text;

/// <summary>
/// A character-to-value assignment for one <see cref="WordValueMethod"/>: the ordered list of scored
/// characters (for the conversion table) and their values (keyed by the lowercase character).
/// </summary>
public sealed class WordValueScheme
{
    public required WordValueMethod Method { get; init; }

    /// <summary>The scored characters in display order (lowercase), e.g. <c>a … z</c> (+ accents).</summary>
    public required IReadOnlyList<char> Characters { get; init; }

    /// <summary>Maps each lowercase scored character to its value.</summary>
    public required IReadOnlyDictionary<char, int> Values { get; init; }

    /// <summary>
    /// <see langword="false"/> for the diacritic-extended schemes (German/Swedish), where the accented
    /// letters carry their own values and so must not be folded away.
    /// </summary>
    public required bool AllowsDiacriticRemoval { get; init; }

    /// <summary>The value of <paramref name="c"/> (case-insensitive), or 0 if it is not scored by this scheme.</summary>
    public int ValueOf(char c) => Values.TryGetValue(char.ToLowerInvariant(c), out var value) ? value : 0;

    /// <summary><see langword="true"/> when <paramref name="c"/> (case-insensitive) is scored by this scheme.</summary>
    public bool Scores(char c) => Values.ContainsKey(char.ToLowerInvariant(c));
}

/// <summary>Builds and caches the <see cref="WordValueScheme"/> for every <see cref="WordValueMethod"/>.</summary>
public static class WordValueSchemes
{
    private const string Latin = "abcdefghijklmnopqrstuvwxyz";
    private const string German = Latin + "äöüß";
    private const string Swedish = Latin + "åäö";

    private static readonly Dictionary<WordValueMethod, WordValueScheme> _cache =
        Enum.GetValues<WordValueMethod>().ToDictionary(m => m, Build);

    /// <summary>Every method in declaration (dropdown) order.</summary>
    public static IReadOnlyList<WordValueMethod> All { get; } = Enum.GetValues<WordValueMethod>();

    public static WordValueScheme For(WordValueMethod method) => _cache[method];

    private static WordValueScheme Build(WordValueMethod method) => method switch
    {
        WordValueMethod.A1Z26 => Linear(method, Latin, i => i + 1),
        WordValueMethod.A0Z25 => Linear(method, Latin, i => i),
        WordValueMethod.A26Z1 => Linear(method, Latin, i => 26 - i),
        WordValueMethod.A25Z0 => Linear(method, Latin, i => 25 - i),
        WordValueMethod.Vanity => Table(method, Latin, [2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 9, 9, 9, 9]),
        WordValueMethod.ScrabbleDutch => Table(method, Latin, [1, 3, 5, 2, 1, 4, 3, 4, 1, 4, 3, 3, 3, 1, 1, 3, 10, 2, 2, 2, 4, 4, 5, 8, 8, 4]),
        WordValueMethod.ScrabbleEnglish => Table(method, Latin, [1, 3, 3, 2, 1, 4, 2, 4, 1, 8, 5, 1, 3, 1, 1, 3, 10, 1, 1, 1, 1, 4, 4, 8, 4, 10]),
        WordValueMethod.ScrabbleGerman => Table(method, Latin, [1, 3, 4, 1, 1, 4, 2, 2, 1, 6, 4, 2, 3, 1, 2, 4, 10, 1, 1, 1, 1, 6, 3, 8, 10, 3]),
        WordValueMethod.Table0to9 => Table(method, Latin, [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3, 4, 5]),
        WordValueMethod.Table1to0 => Table(method, Latin, [1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3, 4, 5, 6]),
        WordValueMethod.Table1to9 => Table(method, Latin, [1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7, 8]),
        WordValueMethod.German1 => Linear(method, German, i => i + 1, allowsDiacriticRemoval: false),
        WordValueMethod.German0 => Linear(method, German, i => i, allowsDiacriticRemoval: false),
        WordValueMethod.Swedish1 => Linear(method, Swedish, i => i + 1, allowsDiacriticRemoval: false),
        WordValueMethod.Swedish0 => Linear(method, Swedish, i => i, allowsDiacriticRemoval: false),
        _ => Linear(method, Latin, i => i + 1),
    };

    private static WordValueScheme Linear(WordValueMethod method, string chars, Func<int, int> value, bool allowsDiacriticRemoval = true)
        => Table(method, chars, [.. Enumerable.Range(0, chars.Length).Select(value)], allowsDiacriticRemoval);

    private static WordValueScheme Table(WordValueMethod method, string chars, int[] values, bool allowsDiacriticRemoval = true)
    {
        var map = new Dictionary<char, int>(chars.Length);
        for (var i = 0; i < chars.Length; i++)
        {
            map[chars[i]] = values[i];
        }

        return new WordValueScheme
        {
            Method = method,
            Characters = [.. chars],
            Values = map,
            AllowsDiacriticRemoval = allowsDiacriticRemoval,
        };
    }
}
