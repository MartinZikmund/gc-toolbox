using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>The letter-to-number lookup table used to score a word.</summary>
public enum NumerologySystem
{
    /// <summary>Letters mapped cyclically 1..9 in alphabetical order (A=1 … I=9, J=1 …). The classic Pythagorean table.</summary>
    Pythagorean1To9,

    /// <summary>Letters mapped cyclically 1..0 (A=1 … I=9, J=0, K=1 …). The Pythagorean "1–0" variant.</summary>
    Pythagorean1To0,

    /// <summary>The Chaldean phonetic table — never assigns 9 (A,I,J,Q,Y=1; B,K,R=2; … F,P,S,T=8).</summary>
    Chaldean,

    /// <summary>Simple/Ordinal gematria: A=1 … Z=26.</summary>
    Simple,
}

/// <summary>How the per-word or whole-string total is reduced to its final value.</summary>
public enum ReductionMode
{
    /// <summary>Repeatedly sum the digits down to a single digit (digital root).</summary>
    FullReduce,

    /// <summary>Sum the digits exactly once (e.g. 199 → 19), without reducing further.</summary>
    SingleStep,

    /// <summary>Keep the raw total untouched.</summary>
    RawTotal,
}

/// <summary>One letter and the value it contributes in the active system.</summary>
public readonly record struct LetterValue(char Letter, int Value);

/// <summary>A single word/line: its letters, raw <see cref="Total"/>, and <see cref="Reduced"/> result.</summary>
public sealed record WordResult(string Word, int Total, int Reduced, IReadOnlyList<LetterValue> Letters);

/// <summary>
/// The full analysis for one system: every contributing <see cref="Letters">letter</see>, the
/// per-word <see cref="Words">totals</see>, the whole-string <see cref="Total"/> and its
/// <see cref="Reduced"/> value. <see cref="HasLetters"/> is <see langword="false"/> when the input
/// contained no scorable letters.
/// </summary>
public sealed record NumerologyResult(
    NumerologySystem System,
    int Total,
    int Reduced,
    IReadOnlyList<WordResult> Words,
    IReadOnlyList<LetterValue> Letters,
    bool HasLetters);

/// <summary>
/// A pure, stateless numerology engine — the single source of truth for letter→number scoring and
/// reduction. Maps each letter with a chosen <see cref="NumerologySystem"/>, sums the values, and
/// reduces to a digital root (or single step / raw total) with optional master-number (11/22/33)
/// preservation. Goes beyond cachesleuth.com parity by exposing every system at once
/// (<see cref="AnalyzeAll"/>), a per-letter breakdown, and per-word/per-line totals. Non-letters are
/// ignored; the result reports whether any letters were found. Fully offline and head-independent.
/// </summary>
public sealed class NumerologyCodec
{
    /// <summary>All systems in display order.</summary>
    public static readonly IReadOnlyList<NumerologySystem> AllSystems =
    [
        NumerologySystem.Pythagorean1To9,
        NumerologySystem.Pythagorean1To0,
        NumerologySystem.Chaldean,
        NumerologySystem.Simple,
    ];

    // Chaldean digit per A..Z (index 0 = A). Never 9.
    private static readonly int[] ChaldeanTable =
    [
        1, // A
        2, // B
        3, // C
        4, // D
        5, // E
        8, // F
        3, // G
        5, // H
        1, // I
        1, // J
        2, // K
        3, // L
        4, // M
        5, // N
        7, // O
        8, // P
        1, // Q
        2, // R
        8, // S
        8, // T
        6, // U
        4, // V
        6, // W
        5, // X
        1, // Y
        7, // Z
    ];

    /// <summary>The value of <paramref name="letter"/> in <paramref name="system"/>; 0 for any non-letter.</summary>
    public int ValueOf(char letter, NumerologySystem system)
    {
        var upper = char.ToUpperInvariant(letter);
        if (upper is < 'A' or > 'Z')
        {
            return 0;
        }

        var index = upper - 'A'; // 0..25
        return system switch
        {
            NumerologySystem.Pythagorean1To9 => (index % 9) + 1,
            NumerologySystem.Pythagorean1To0 => (index + 1) % 10,
            NumerologySystem.Chaldean => ChaldeanTable[index],
            NumerologySystem.Simple => index + 1,
            _ => 0,
        };
    }

    /// <summary>
    /// Reduces <paramref name="value"/> per <paramref name="mode"/>. When
    /// <paramref name="preserveMasterNumbers"/> is set, the full reduction stops if it ever lands on a
    /// master number (11, 22, 33) rather than collapsing it to a single digit.
    /// </summary>
    public int Reduce(int value, ReductionMode mode, bool preserveMasterNumbers)
    {
        var magnitude = Math.Abs(value);

        switch (mode)
        {
            case ReductionMode.RawTotal:
                return value;

            case ReductionMode.SingleStep:
                if (preserveMasterNumbers && IsMasterNumber(magnitude))
                {
                    return value;
                }

                return DigitSum(magnitude);

            case ReductionMode.FullReduce:
            default:
                while (magnitude > 9)
                {
                    if (preserveMasterNumbers && IsMasterNumber(magnitude))
                    {
                        break;
                    }

                    magnitude = DigitSum(magnitude);
                }

                return magnitude;
        }
    }

    /// <summary>Scores <paramref name="text"/> with one <paramref name="system"/>.</summary>
    public NumerologyResult Analyze(string? text, NumerologySystem system, ReductionMode mode, bool preserveMasterNumbers)
    {
        List<LetterValue> letters = [];
        List<WordResult> words = [];
        var grandTotal = 0;

        foreach (var word in SplitWords(text))
        {
            List<LetterValue> wordLetters = [];
            var wordTotal = 0;

            foreach (var c in word)
            {
                var value = ValueOf(c, system);
                if (char.IsLetter(c))
                {
                    var entry = new LetterValue(char.ToUpperInvariant(c), value);
                    wordLetters.Add(entry);
                    letters.Add(entry);
                    wordTotal += value;
                }
            }

            if (wordLetters.Count == 0)
            {
                continue; // a run of digits/punctuation contributes no word
            }

            grandTotal += wordTotal;
            words.Add(new WordResult(word, wordTotal, Reduce(wordTotal, mode, preserveMasterNumbers), wordLetters));
        }

        var hasLetters = letters.Count > 0;
        var reduced = hasLetters ? Reduce(grandTotal, mode, preserveMasterNumbers) : 0;
        return new NumerologyResult(system, grandTotal, reduced, words, letters, hasLetters);
    }

    /// <summary>Scores <paramref name="text"/> with every <see cref="NumerologySystem"/> at once.</summary>
    public IReadOnlyDictionary<NumerologySystem, NumerologyResult> AnalyzeAll(string? text, ReductionMode mode, bool preserveMasterNumbers)
    {
        Dictionary<NumerologySystem, NumerologyResult> map = new(AllSystems.Count);
        foreach (var system in AllSystems)
        {
            map[system] = Analyze(text, system, mode, preserveMasterNumbers);
        }

        return map;
    }

    /// <summary>The localization key suffix / human label hint for <paramref name="system"/>.</summary>
    public static string SystemKey(NumerologySystem system) => system switch
    {
        NumerologySystem.Pythagorean1To9 => "Pythagorean1To9",
        NumerologySystem.Pythagorean1To0 => "Pythagorean1To0",
        NumerologySystem.Chaldean => "Chaldean",
        NumerologySystem.Simple => "Simple",
        _ => system.ToString(),
    };

    private static bool IsMasterNumber(int value) => value is 11 or 22 or 33;

    private static int DigitSum(int value)
    {
        var sum = 0;
        while (value > 0)
        {
            sum += value % 10;
            value /= 10;
        }

        return sum;
    }

    /// <summary>Splits on any whitespace, dropping empty entries. A word may still contain non-letters.</summary>
    private static IEnumerable<string> SplitWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }
            }
            else
            {
                builder.Append(rune.ToString());
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }
}
