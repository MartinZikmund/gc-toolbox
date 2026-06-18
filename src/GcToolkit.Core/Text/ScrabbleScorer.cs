using System.Globalization;

namespace GcToolkit.Core.Text;

/// <summary>
/// The letter-value system used to score a word. Covers Scrabble tile sets for several languages,
/// the alphabet-position families (A1Z26 and its reversed/zero-based variants), the phone-keypad
/// "vanity code" mapping, and a user-supplied custom table.
/// </summary>
public enum LetterValueSystem
{
    /// <summary>English Scrabble tile values.</summary>
    ScrabbleEnglish,

    /// <summary>Dutch Scrabble tile values.</summary>
    ScrabbleDutch,

    /// <summary>German Scrabble tile values.</summary>
    ScrabbleGerman,

    /// <summary>French Scrabble tile values.</summary>
    ScrabbleFrench,

    /// <summary>Spanish Scrabble tile values.</summary>
    ScrabbleSpanish,

    /// <summary>Italian Scrabble tile values.</summary>
    ScrabbleItalian,

    /// <summary>Alphabet position, A=1 … Z=26.</summary>
    A1Z26,

    /// <summary>Zero-based alphabet position, A=0 … Z=25.</summary>
    A0Z25,

    /// <summary>Reversed alphabet position, A=26 … Z=1.</summary>
    ReversedA26Z1,

    /// <summary>Reversed zero-based alphabet position, A=25 … Z=0.</summary>
    ReversedA25Z0,

    /// <summary>Phone-keypad vanity code, A,B,C=2 … W,X,Y,Z=9.</summary>
    PhoneKeypad,

    /// <summary>A custom A–Z table supplied by the caller.</summary>
    Custom,
}

/// <summary>One scored letter: the source <see cref="Letter"/> (as typed) and its <see cref="Value"/>
/// in the active table. <see cref="HasValue"/> is <see langword="false"/> for a letter the active
/// table doesn't define (still listed in the breakdown, but flagged).</summary>
public readonly record struct LetterScore(char Letter, int Value, bool HasValue);

/// <summary>The handful of geocaching follow-up reductions applied to a score.</summary>
/// <param name="Total">The raw sum of tile values.</param>
/// <param name="DigitalRoot">Repeated digit-sum down to a single digit (1–9, or 0 for total 0).</param>
/// <param name="DigitSum">Sum of the decimal digits of <see cref="Total"/> (one pass).</param>
/// <param name="Mod26">Total modulo 26.</param>
/// <param name="Mod10">Total modulo 10.</param>
/// <param name="ReversedTotal">The total with its decimal digits reversed.</param>
public readonly record struct ScoreReductions(int Total, int DigitalRoot, int DigitSum, int Mod26, int Mod10, int ReversedTotal);

/// <summary>The score of one word: its <see cref="Text"/>, per-letter <see cref="Letters"/>, the
/// <see cref="Reductions"/>, and whether it contained an <see cref="HasUnknown"/> letter the table
/// doesn't define.</summary>
public sealed class WordScore
{
    public WordScore(string text, IReadOnlyList<LetterScore> letters)
    {
        Text = text;
        Letters = letters;
        var total = 0;
        var hasUnknown = false;
        foreach (var l in letters)
        {
            total += l.Value;
            hasUnknown |= !l.HasValue;
        }

        Total = total;
        HasUnknown = hasUnknown;
        Reductions = ScrabbleScorer.Reduce(total);
    }

    public string Text { get; }

    public IReadOnlyList<LetterScore> Letters { get; }

    public int Total { get; }

    public bool HasUnknown { get; }

    public ScoreReductions Reductions { get; }

    /// <summary>A compact breakdown like <c>Q=10 + U=1 + I=1 + Z=10 = 22</c> for the scored letters.</summary>
    public string Breakdown
    {
        get
        {
            if (Letters.Count == 0)
            {
                return "0";
            }

            var terms = Letters.Select(l => l.HasValue
                ? $"{char.ToUpperInvariant(l.Letter)}={l.Value}"
                : $"{char.ToUpperInvariant(l.Letter)}=?");
            return $"{string.Join(" + ", terms)} = {Total}";
        }
    }
}

/// <summary>The score of an entire input: every <see cref="Words"/> plus the whole-text
/// <see cref="GrandTotal"/> and its <see cref="Reductions"/>.</summary>
public sealed class TextScore
{
    public TextScore(IReadOnlyList<WordScore> words)
    {
        Words = words;
        var total = 0;
        var hasUnknown = false;
        foreach (var w in words)
        {
            total += w.Total;
            hasUnknown |= w.HasUnknown;
        }

        GrandTotal = total;
        HasUnknown = hasUnknown;
        Reductions = ScrabbleScorer.Reduce(total);
    }

    public IReadOnlyList<WordScore> Words { get; }

    public int GrandTotal { get; }

    public bool HasUnknown { get; }

    public ScoreReductions Reductions { get; }
}

/// <summary>
/// A pure, head-independent word-value scorer. Maps each letter of a word (or whole text) to its
/// value in a chosen <see cref="LetterValueSystem"/> and sums them — per word and over the whole
/// input — exposing a per-letter breakdown plus the usual geocaching reductions (digital root,
/// digit sum, mod-26 / mod-10, reversed total). Non-letter characters are ignored; letters absent
/// from the active table are flagged. Scoring is unidirectional (score-only). Beyond
/// cachesleuth.com parity it ships six Scrabble languages, the four alphabet-position variants,
/// the phone-keypad vanity table, and arbitrary custom tables, and scores batches line by line.
/// </summary>
public sealed class ScrabbleScorer
{
    /// <summary>The 26 English Scrabble tile values, indexed A→Z.</summary>
    private static readonly int[] _english =
        [1, 3, 3, 2, 1, 4, 2, 4, 1, 8, 5, 1, 3, 1, 1, 3, 10, 1, 1, 1, 1, 4, 4, 8, 4, 10];

    //          A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q   R  S  T  U  V  W  X  Y  Z
    private static readonly int[] _dutch =
        [1, 3, 5, 2, 1, 4, 3, 2, 1, 4, 3, 3, 3, 1, 1, 3, 10, 2, 2, 2, 4, 4, 5, 8, 8, 4];

    //          A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q   R  S  T  U  V  W  X  Y   Z
    private static readonly int[] _german =
        [1, 3, 4, 1, 1, 4, 2, 2, 1, 6, 4, 2, 3, 1, 2, 4, 10, 1, 1, 1, 1, 6, 3, 8, 10, 3];

    //          A  B  C  D  E  F  G  H  I  J  K   L  M  N  O  P  Q  R  S  T  U  V  W   X   Y   Z
    private static readonly int[] _french =
        [1, 3, 3, 2, 1, 4, 2, 4, 1, 8, 10, 1, 2, 1, 1, 3, 8, 1, 1, 1, 1, 4, 10, 10, 10, 10];

    //          A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q  R  S  T  U  V  W  X  Y  Z
    private static readonly int[] _spanish =
        [1, 3, 3, 2, 1, 4, 2, 4, 1, 8, 5, 1, 3, 1, 1, 3, 5, 1, 1, 1, 1, 4, 4, 8, 4, 10];

    // Italian has no J, K, W, X, Y tiles — those positions score 0.
    //          A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q   R  S  T  U  V  W  X  Y  Z
    private static readonly int[] _italian =
        [1, 5, 2, 5, 1, 5, 8, 8, 1, 0, 0, 3, 3, 3, 1, 5, 10, 2, 2, 2, 3, 5, 0, 0, 0, 8];

    /// <summary>The English Scrabble tile distribution (count per letter) for the reference panel.</summary>
    private static readonly int[] _englishDistribution =
        [9, 2, 2, 4, 12, 2, 3, 2, 9, 1, 1, 4, 2, 6, 8, 2, 1, 6, 4, 6, 4, 2, 2, 1, 2, 1];

    /// <summary>
    /// Builds the per-letter value table (length 26, indexed A→Z) for a built-in
    /// <paramref name="system"/>. Throws for <see cref="LetterValueSystem.Custom"/>, which has no
    /// built-in table — supply one via the custom-table overloads instead.
    /// </summary>
    public static IReadOnlyList<int> GetTable(LetterValueSystem system) => system switch
    {
        LetterValueSystem.ScrabbleEnglish => _english,
        LetterValueSystem.ScrabbleDutch => _dutch,
        LetterValueSystem.ScrabbleGerman => _german,
        LetterValueSystem.ScrabbleFrench => _french,
        LetterValueSystem.ScrabbleSpanish => _spanish,
        LetterValueSystem.ScrabbleItalian => _italian,
        LetterValueSystem.A1Z26 => BuildPositional(start: 1, step: 1),
        LetterValueSystem.A0Z25 => BuildPositional(start: 0, step: 1),
        LetterValueSystem.ReversedA26Z1 => BuildPositional(start: 26, step: -1),
        LetterValueSystem.ReversedA25Z0 => BuildPositional(start: 25, step: -1),
        LetterValueSystem.PhoneKeypad => BuildPhoneKeypad(),
        _ => throw new ArgumentOutOfRangeException(nameof(system), system, "Custom has no built-in table."),
    };

    /// <summary>The English Scrabble tile distribution (count per letter), indexed A→Z.</summary>
    public static IReadOnlyList<int> EnglishDistribution => _englishDistribution;

    /// <summary>Scores <paramref name="text"/> as a whole, splitting on whitespace into words.</summary>
    /// <param name="text">The input to score; <see langword="null"/> is treated as empty.</param>
    /// <param name="system">The built-in letter-value system to use.</param>
    public TextScore Score(string? text, LetterValueSystem system)
        => Score(text, GetTable(system));

    /// <summary>Scores <paramref name="text"/> using an explicit A–Z value table (the custom path).</summary>
    /// <param name="table">26 values indexed A→Z. Must contain exactly 26 entries.</param>
    public TextScore Score(string? text, IReadOnlyList<int> table)
    {
        EnsureTable(table);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TextScore([]);
        }

        var words = text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => ScoreWord(word, table))
            .ToList();

        return new TextScore(words);
    }

    /// <summary>Scores each non-empty line of <paramref name="text"/> separately (batch mode).</summary>
    public IReadOnlyList<TextScore> ScoreBatch(string? text, LetterValueSystem system)
        => ScoreBatch(text, GetTable(system));

    /// <summary>Scores each non-empty line of <paramref name="text"/> with a custom table (batch mode).</summary>
    public IReadOnlyList<TextScore> ScoreBatch(string? text, IReadOnlyList<int> table)
    {
        EnsureTable(table);
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Score(line, table))
            .ToList();
    }

    /// <summary>Scores a single, already-tokenized <paramref name="word"/> with a custom table.</summary>
    public WordScore ScoreWord(string word, IReadOnlyList<int> table)
    {
        EnsureTable(table);
        var letters = new List<LetterScore>(word.Length);
        foreach (var c in word)
        {
            if (!char.IsLetter(c))
            {
                continue; // Non-letters are ignored when scoring.
            }

            if (TryLetterIndex(c, out var index))
            {
                letters.Add(new LetterScore(c, table[index], HasValue: true));
            }
            else
            {
                // A letter the active (A–Z) table can't map — flagged, contributes 0.
                letters.Add(new LetterScore(c, 0, HasValue: false));
            }
        }

        return new WordScore(word, letters);
    }

    /// <summary>Scores a single word with a built-in <paramref name="system"/>.</summary>
    public WordScore ScoreWord(string word, LetterValueSystem system)
        => ScoreWord(word, GetTable(system));

    /// <summary>The geocaching reductions for a raw <paramref name="total"/>.</summary>
    public static ScoreReductions Reduce(int total)
    {
        var magnitude = Math.Abs(total);
        return new ScoreReductions(
            Total: total,
            DigitalRoot: DigitalRootOf(magnitude),
            DigitSum: DigitSumOf(magnitude),
            Mod26: ((total % 26) + 26) % 26,
            Mod10: ((total % 10) + 10) % 10,
            ReversedTotal: ReverseDigits(total));
    }

    /// <summary>Repeated digit-sum down to a single digit (digital root). 0 → 0, multiples of 9 → 9.</summary>
    public static int DigitalRootOf(int value)
    {
        var magnitude = Math.Abs(value);
        return magnitude == 0 ? 0 : 1 + (magnitude - 1) % 9;
    }

    /// <summary>A single pass of summing the decimal digits of <paramref name="value"/>.</summary>
    public static int DigitSumOf(int value)
    {
        var magnitude = Math.Abs(value);
        var sum = 0;
        while (magnitude > 0)
        {
            sum += magnitude % 10;
            magnitude /= 10;
        }

        return sum;
    }

    private static int ReverseDigits(int value)
    {
        var sign = Math.Sign(value);
        var magnitude = Math.Abs(value);
        var reversed = 0;
        while (magnitude > 0)
        {
            reversed = (reversed * 10) + (magnitude % 10);
            magnitude /= 10;
        }

        return reversed * sign;
    }

    /// <summary>Maps an ASCII letter to its 0–25 alphabet index, after diacritic folding.</summary>
    private static bool TryLetterIndex(char c, out int index)
    {
        var folded = char.ToUpperInvariant(Fold(c));
        if (folded is >= 'A' and <= 'Z')
        {
            index = folded - 'A';
            return true;
        }

        index = -1;
        return false;
    }

    /// <summary>Strips a common diacritic to its base A–Z letter so accented input still scores.</summary>
    private static char Fold(char c)
    {
        var lower = char.ToLowerInvariant(c);
        var folded = lower switch
        {
            'á' or 'à' or 'â' or 'ä' or 'ã' or 'å' or 'ą' => 'a',
            'ç' or 'č' or 'ć' => 'c',
            'ď' => 'd',
            'é' or 'è' or 'ê' or 'ë' or 'ě' or 'ę' => 'e',
            'í' or 'ì' or 'î' or 'ï' => 'i',
            'ľ' or 'ł' => 'l',
            'ñ' or 'ň' => 'n',
            'ó' or 'ò' or 'ô' or 'ö' or 'õ' or 'ø' => 'o',
            'ř' => 'r',
            'š' or 'ś' => 's',
            'ť' => 't',
            'ú' or 'ù' or 'û' or 'ü' or 'ů' => 'u',
            'ý' or 'ÿ' => 'y',
            'ž' or 'ź' or 'ż' => 'z',
            _ => lower,
        };

        return folded == lower ? c : folded;
    }

    private static int[] BuildPositional(int start, int step)
    {
        var table = new int[26];
        for (var i = 0; i < 26; i++)
        {
            table[i] = start + (step * i);
        }

        return table;
    }

    private static int[] BuildPhoneKeypad()
    {
        // 2:ABC 3:DEF 4:GHI 5:JKL 6:MNO 7:PQRS 8:TUV 9:WXYZ
        int[] groupSizes = [3, 3, 3, 3, 3, 4, 3, 4];
        var table = new int[26];
        var letter = 0;
        for (var g = 0; g < groupSizes.Length; g++)
        {
            for (var k = 0; k < groupSizes[g]; k++)
            {
                table[letter++] = g + 2;
            }
        }

        return table;
    }

    private static void EnsureTable(IReadOnlyList<int> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Count != 26)
        {
            throw new ArgumentException(
                $"A letter-value table must have exactly 26 entries (A–Z); got {table.Count.ToString(CultureInfo.InvariantCulture)}.",
                nameof(table));
        }
    }
}
