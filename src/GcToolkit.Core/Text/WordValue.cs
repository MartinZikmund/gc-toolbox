using System.Text;

namespace GcToolkit.Core.Text;

/// <summary>One word and its letter-sum value (A=1 … Z=26, non-letters ignored).</summary>
public readonly record struct WordLetterValue(string Text, int Value);

/// <summary>
/// The full word-value analysis of a block of text: the per-word letter sums and their grand total,
/// any plain numbers found in the input, and the cross-sum / digital-root reductions of both totals.
/// </summary>
public sealed class WordValueAnalysis
{
    /// <summary>The words found (tokens containing at least one letter), each with its letter sum.</summary>
    public required IReadOnlyList<WordLetterValue> Words { get; init; }

    /// <summary>The plain numbers found in the input (each maximal run of digits), in order.
    /// Empty when number counting is off.</summary>
    public required IReadOnlyList<long> Numbers { get; init; }

    /// <summary>Sum of every word's letter value — the headline "word value".</summary>
    public required long LetterTotal { get; init; }

    /// <summary>Cross-sum (single pass of summing the digits) of <see cref="LetterTotal"/>.</summary>
    public required int LetterCrossSum { get; init; }

    /// <summary>Digital root (repeated digit-sum) of <see cref="LetterTotal"/>; 1–9, or 0 when the total is 0.</summary>
    public required int LetterDigitalRoot { get; init; }

    /// <summary>The reduction chain for <see cref="LetterTotal"/>, e.g. <c>[12345, 15, 6]</c>.</summary>
    public required IReadOnlyList<long> LetterReductionSteps { get; init; }

    /// <summary>Sum of every number found in the input. 0 when number counting is off or none were found.</summary>
    public required long NumberTotal { get; init; }

    /// <summary>Cross-sum of <see cref="NumberTotal"/>.</summary>
    public required int NumberCrossSum { get; init; }

    /// <summary>Digital root of <see cref="NumberTotal"/>.</summary>
    public required int NumberDigitalRoot { get; init; }

    /// <summary>The reduction chain for <see cref="NumberTotal"/>.</summary>
    public required IReadOnlyList<long> NumberReductionSteps { get; init; }

    /// <summary><see langword="true"/> when at least one letter-bearing word was found.</summary>
    public bool HasContent => Words.Count > 0;

    /// <summary><see langword="true"/> when at least one number was captured.</summary>
    public bool HasNumbers => Numbers.Count > 0;
}

/// <summary>
/// A pure, stateless calculator for the geocaching "word value (digital root)" method — the single
/// source of truth for the transform. It sums letter positions (A=1 … Z=26, case-insensitive,
/// non-letters ignored) per word and overall, and reduces any total via its cross-sum (one digit-sum
/// pass) to its digital root (repeated until a single digit remains). Beyond geocachingtoolbox.com
/// parity (total value, per-word values, digital root), it also captures plain numbers in the input
/// and reduces them too, and exposes the full reduction chain for a "show your work" display.
/// </summary>
public sealed class WordValueCalculator
{
    /// <summary>The position of <paramref name="c"/> in the alphabet (A/a = 1 … Z/z = 26), or 0 if it is not a Latin letter.</summary>
    public int LetterValue(char c) => c switch
    {
        >= 'A' and <= 'Z' => c - 'A' + 1,
        >= 'a' and <= 'z' => c - 'a' + 1,
        _ => 0,
    };

    /// <summary>The letter sum of <paramref name="word"/>: every Latin letter's position added up, others ignored.</summary>
    public int WordValue(string? word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return 0;
        }

        var sum = 0;
        foreach (var c in word)
        {
            sum += LetterValue(c);
        }

        return sum;
    }

    /// <summary>The cross-sum of <paramref name="value"/>: a single pass summing its decimal digits.</summary>
    public int CrossSum(long value)
    {
        var n = Math.Abs(value);
        var sum = 0;
        while (n > 0)
        {
            sum += (int)(n % 10);
            n /= 10;
        }

        return sum;
    }

    /// <summary>
    /// The digital root of <paramref name="value"/>: the cross-sum applied repeatedly until a single
    /// digit remains. Always 1–9 for a positive value; 0 only for 0.
    /// </summary>
    public int DigitalRoot(long value)
    {
        var n = Math.Abs(value);
        while (n >= 10)
        {
            n = CrossSum(n);
        }

        return (int)n;
    }

    /// <summary>
    /// The reduction chain from <paramref name="value"/> down to its digital root — the value itself,
    /// then each successive cross-sum, ending on a single digit. A single-digit value yields a one-element list.
    /// </summary>
    public IReadOnlyList<long> ReductionSteps(long value)
    {
        var n = Math.Abs(value);
        var steps = new List<long> { n };
        while (n >= 10)
        {
            n = CrossSum(n);
            steps.Add(n);
        }

        return steps;
    }

    /// <summary>
    /// Analyzes <paramref name="text"/>: splits it into whitespace-separated words, computes each word's
    /// letter value and the grand total, reduces that total to its digital root, and — when
    /// <paramref name="countNumbers"/> is <see langword="true"/> — also collects every digit-run as a
    /// number and reduces their sum.
    /// </summary>
    public WordValueAnalysis Analyze(string? text, bool countNumbers)
    {
        var words = new List<WordLetterValue>();
        var numbers = new List<long>();
        long letterTotal = 0;

        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var value = WordValue(token);
                if (value > 0)
                {
                    words.Add(new WordLetterValue(token, value));
                    letterTotal += value;
                }
            }

            if (countNumbers)
            {
                CollectNumbers(text, numbers);
            }
        }

        long numberTotal = 0;
        foreach (var number in numbers)
        {
            numberTotal += number;
        }

        return new WordValueAnalysis
        {
            Words = words,
            Numbers = numbers,
            LetterTotal = letterTotal,
            LetterCrossSum = CrossSum(letterTotal),
            LetterDigitalRoot = DigitalRoot(letterTotal),
            LetterReductionSteps = ReductionSteps(letterTotal),
            NumberTotal = numberTotal,
            NumberCrossSum = CrossSum(numberTotal),
            NumberDigitalRoot = DigitalRoot(numberTotal),
            NumberReductionSteps = ReductionSteps(numberTotal),
        };
    }

    /// <summary>Captures every maximal run of ASCII digits in <paramref name="text"/> as a number.
    /// Runs that overflow <see cref="long"/> are skipped rather than throwing.</summary>
    private static void CollectNumbers(string text, List<long> numbers)
    {
        var run = new StringBuilder();
        foreach (var c in text)
        {
            if (c is >= '0' and <= '9')
            {
                run.Append(c);
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
