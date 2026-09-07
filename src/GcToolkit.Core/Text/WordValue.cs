using System.Text;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Text;

/// <summary>One whitespace-separated word and its computed value: the contributing per-character
/// <see cref="Terms"/>, their <see cref="Total"/>, and the reduction chain down to the digital root.</summary>
public sealed record WordValueWord(string Text, IReadOnlyList<int> Terms, long Total, IReadOnlyList<long> ReductionSteps);

/// <summary>
/// The full word-value analysis of a block of text: the contributing terms and grand total, the
/// reduction chain to the digital root, the per-word breakdown, and — when numbers are summed
/// separately — the plain numbers found and their own total / reduction.
/// </summary>
public sealed class WordValueAnalysis
{
    /// <summary>The contributing terms of the whole text, in order. Skipped characters (spaces,
    /// punctuation, uncounted digits) produce no term, so the calculation reads cleanly.</summary>
    public required IReadOnlyList<int> Terms { get; init; }

    /// <summary>Sum of every term — the headline "word value".</summary>
    public required long Total { get; init; }

    /// <summary>The reduction chain for <see cref="Total"/>, e.g. <c>[782, 17, 8]</c>.</summary>
    public required IReadOnlyList<long> ReductionSteps { get; init; }

    /// <summary>The per-word breakdown (one entry per whitespace-separated token).</summary>
    public required IReadOnlyList<WordValueWord> Words { get; init; }

    /// <summary>The plain numbers found, when <see cref="NumberHandling.NumbersSeparate"/> is used; empty otherwise.</summary>
    public required IReadOnlyList<long> Numbers { get; init; }

    /// <summary>Sum of <see cref="Numbers"/>.</summary>
    public required long NumberTotal { get; init; }

    /// <summary>The reduction chain for <see cref="NumberTotal"/>.</summary>
    public required IReadOnlyList<long> NumberReductionSteps { get; init; }

    /// <summary><see langword="true"/> when at least one term was counted.</summary>
    public bool HasContent => Terms.Count > 0;

    /// <summary><see langword="true"/> when at least one separate number was captured.</summary>
    public bool HasNumbers => Numbers.Count > 0;
}

/// <summary>
/// A pure, stateless calculator for the geocaching "word value (digital root)" method — the single
/// source of truth for the transform. For a chosen <see cref="WordValueScheme"/> it sums each scored
/// character's value, optionally folding diacritics first and optionally counting digits, then reduces
/// the total via repeated digit-sums to its digital root. It also produces a per-word breakdown and,
/// when asked, sums plain multi-digit numbers separately.
/// </summary>
public sealed class WordValueCalculator
{
    // The digit arithmetic lives in ChecksumCalculator so the two tools cannot drift apart; these
    // stay as instance methods because the calculator is injected and callers bind to them.

    /// <summary>The cross-sum of <paramref name="value"/>: a single pass summing its decimal digits.
    /// The sign is ignored.</summary>
    public int CrossSum(long value) => ChecksumCalculator.DigitSum(value);

    /// <summary>The digital root of <paramref name="value"/>: the cross-sum applied until one digit
    /// remains. The sign is ignored.</summary>
    public int DigitalRoot(long value) => ChecksumCalculator.DigitalRoot(value);

    /// <summary>The reduction chain from <paramref name="value"/> to its digital root (the magnitude
    /// itself, then each successive cross-sum). A single-digit value yields a one-element list.</summary>
    public IReadOnlyList<long> ReductionSteps(long value) => ChecksumCalculator.ReduceMagnitude(value);

    /// <summary>
    /// Analyzes <paramref name="text"/> with the given <paramref name="scheme"/>, <paramref name="numbers"/>
    /// handling, and optional diacritic folding. Returns the contributing terms, the total and its
    /// reduction, the per-word breakdown, and any separately-summed numbers.
    /// </summary>
    public WordValueAnalysis Analyze(string? text, WordValueScheme scheme, NumberHandling numbers, bool removeDiacritics)
    {
        var working = text ?? string.Empty;
        if (removeDiacritics && scheme.AllowsDiacriticRemoval)
        {
            working = DiacriticFolder.Fold(working);
        }

        var includeDigits = numbers == NumberHandling.DigitsInTotal;

        var (terms, total) = ComputeTerms(working, scheme, includeDigits);

        var words = new List<WordValueWord>();
        foreach (var token in working.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var (wordTerms, wordTotal) = ComputeTerms(token, scheme, includeDigits);
            words.Add(new WordValueWord(token, wordTerms, wordTotal, ReductionSteps(wordTotal)));
        }

        var foundNumbers = new List<long>();
        long numberTotal = 0;
        if (numbers == NumberHandling.NumbersSeparate)
        {
            CollectNumbers(working, foundNumbers);
            foreach (var number in foundNumbers)
            {
                numberTotal += number;
            }
        }

        return new WordValueAnalysis
        {
            Terms = terms,
            Total = total,
            ReductionSteps = ReductionSteps(total),
            Words = words,
            Numbers = foundNumbers,
            NumberTotal = numberTotal,
            NumberReductionSteps = ReductionSteps(numberTotal),
        };
    }

    /// <summary>Walks <paramref name="text"/> collecting a term for every scored character (and, when
    /// <paramref name="includeDigits"/> is set, every digit). Uncounted characters produce no term.</summary>
    private static (List<int> Terms, long Total) ComputeTerms(string text, WordValueScheme scheme, bool includeDigits)
    {
        var terms = new List<int>();
        long total = 0;

        foreach (var ch in text)
        {
            var lower = char.ToLowerInvariant(ch);
            if (scheme.Values.TryGetValue(lower, out var value))
            {
                terms.Add(value);
                total += value;
            }
            else if (includeDigits && lower is >= '0' and <= '9')
            {
                var digit = lower - '0';
                terms.Add(digit);
                total += digit;
            }
        }

        return (terms, total);
    }

    /// <summary>Captures every maximal run of ASCII digits as a number; runs that overflow are skipped.</summary>
    private static void CollectNumbers(string text, List<long> numbers)
    {
        var run = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch is >= '0' and <= '9')
            {
                run.Append(ch);
            }
            else if (run.Length > 0)
            {
                FlushNumber(run, numbers);
            }
        }

        if (run.Length > 0)
        {
            FlushNumber(run, numbers);
        }
    }

    private static void FlushNumber(StringBuilder run, List<long> numbers)
    {
        if (long.TryParse(run.ToString(), out var number))
        {
            numbers.Add(number);
        }

        run.Clear();
    }
}
