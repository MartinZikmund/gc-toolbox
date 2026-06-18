namespace GcToolkit.Core.Text;

/// <summary>
/// Reference letter-frequency distributions (percent of letters) for overlaying the observed
/// distribution and for the suggested-mapping alignment. Values are the conventional published
/// approximations; each table sums to roughly 100%.
/// </summary>
public static class FrequencyTables
{
    /// <summary>Expected English letter frequencies (%), A–Z.</summary>
    public static IReadOnlyDictionary<char, double> English { get; } = new Dictionary<char, double>
    {
        ['A'] = 8.17, ['B'] = 1.49, ['C'] = 2.78, ['D'] = 4.25, ['E'] = 12.70,
        ['F'] = 2.23, ['G'] = 2.02, ['H'] = 6.09, ['I'] = 6.97, ['J'] = 0.15,
        ['K'] = 0.77, ['L'] = 4.03, ['M'] = 2.41, ['N'] = 6.75, ['O'] = 7.51,
        ['P'] = 1.93, ['Q'] = 0.10, ['R'] = 5.99, ['S'] = 6.33, ['T'] = 9.06,
        ['U'] = 2.76, ['V'] = 0.98, ['W'] = 2.36, ['X'] = 0.15, ['Y'] = 1.97,
        ['Z'] = 0.07,
    };

    /// <summary>Expected Czech letter frequencies (%), A–Z (diacritics folded to base letters, normalized to 100%).</summary>
    public static IReadOnlyDictionary<char, double> Czech { get; } = new Dictionary<char, double>
    {
        ['A'] = 9.48, ['B'] = 1.60, ['C'] = 3.04, ['D'] = 3.66, ['E'] = 11.88,
        ['F'] = 0.19, ['G'] = 0.25, ['H'] = 1.80, ['I'] = 6.56, ['J'] = 1.93,
        ['K'] = 3.66, ['L'] = 4.51, ['M'] = 2.98, ['N'] = 6.57, ['O'] = 8.72,
        ['P'] = 3.24, ['Q'] = 0.01, ['R'] = 4.48, ['S'] = 5.19, ['T'] = 5.91,
        ['U'] = 3.76, ['V'] = 4.98, ['W'] = 0.02, ['X'] = 0.04, ['Y'] = 2.82,
        ['Z'] = 2.71,
    };

    /// <summary>English letters ordered most-common → least-common (E, T, A, O …) — the mapping target order.</summary>
    public static char[] EnglishOrder { get; } =
        [.. English.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key).Select(kvp => kvp.Key)];

    /// <summary>Czech letters ordered most-common → least-common — the alternative mapping target order.</summary>
    public static char[] CzechOrder { get; } =
        [.. Czech.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key).Select(kvp => kvp.Key)];
}
