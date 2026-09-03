using System.Text;
using GcToolkit.Core.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>One scored character and what it contributed. Letters are stored upper-cased so the
/// working reads the way a geocacher writes it out.</summary>
public readonly record struct ChecksumTerm(char Character, int Value);

/// <summary>One run of letters/digits between ignored characters, scored on its own — the way a
/// coordinate line is checked group by group.</summary>
public readonly record struct ChecksumPart(string Text, long DigitSum, long LetterSum, long Root)
{
    public long Total => DigitSum + LetterSum;
}

/// <summary>One side of the calculation (the digits or the letters): its total, the chain that
/// reduces it to a single digit, and the individual terms behind it.</summary>
/// <param name="Count">How many characters were scored.</param>
/// <param name="Value">Their sum.</param>
/// <param name="Reduction">The reduction chain starting at <paramref name="Value"/>, e.g. <c>[15, 6]</c>.</param>
/// <param name="Terms">The per-character terms, capped at <see cref="ChecksumCalculator.MaxDetailTerms"/>.</param>
/// <param name="TermsTruncated">Whether <paramref name="Terms"/> hit that cap.</param>
public sealed record ChecksumSum(
    int Count,
    long Value,
    IReadOnlyList<long> Reduction,
    IReadOnlyList<ChecksumTerm> Terms,
    bool TermsTruncated)
{
    public static ChecksumSum Empty { get; } = new(0, 0, [0], [], false);

    /// <summary>The single digit the sum reduces to.</summary>
    public long Root => Reduction[^1];

    public bool HasContent => Count > 0;
}

/// <summary>The full checksum reading of one input.</summary>
public sealed record ChecksumAnalysis(
    ChecksumSum Digits,
    ChecksumSum Letters,
    IReadOnlyList<long> TotalReduction,
    IReadOnlyList<ChecksumPart> Parts,
    int IgnoredCount,
    bool PartsTruncated)
{
    public static ChecksumAnalysis Empty { get; } = new(ChecksumSum.Empty, ChecksumSum.Empty, [0], [], 0, false);

    /// <summary>Digits and letters added together — the headline checksum.</summary>
    public long Total => Digits.Value + Letters.Value;

    public long TotalRoot => TotalReduction[^1];

    public bool HasContent => Digits.HasContent || Letters.HasContent;

    /// <summary>Both sides contributed, so the split into digits and letters is worth showing.</summary>
    public bool IsMixed => Digits.HasContent && Letters.HasContent;
}

/// <summary>
/// The geocaching checksum: the cross sum of a number's digits, the letter-value sum of its text
/// (A=1 … Z=26), and the iterated digital root of either. Punctuation, whitespace and anything else
/// that is not a Latin letter or an ASCII digit is skipped; accented letters fold to their base
/// letter by default (Ž → Z) so Czech and German words score like their plain spelling.
/// Pure and static — no UI, no I/O.
/// </summary>
public static class ChecksumCalculator
{
    /// <summary>How many per-character terms are kept for the "show the working" display. A cache
    /// description pasted whole must not build a hundred thousand terms; the sums stay exact.</summary>
    public const int MaxDetailTerms = 400;

    /// <summary>How many per-part rows are kept. The totals still count every part.</summary>
    public const int MaxParts = 200;

    /// <summary>The cross sum of <paramref name="value"/>'s digits. The sign is ignored.</summary>
    public static int DigitSum(long value)
    {
        var magnitude = Magnitude(value);
        var sum = 0;
        do
        {
            sum += (int)(magnitude % 10);
            magnitude /= 10;
        }
        while (magnitude > 0);

        return sum;
    }

    /// <summary>Sums the digits over and over until one digit is left. The sign is ignored.</summary>
    public static int DigitalRoot(long value)
    {
        var current = DigitSum(value);
        while (current >= 10)
        {
            current = DigitSum(current);
        }

        return current;
    }

    /// <summary>
    /// The whole reduction written out — <c>Reduce(12345)</c> is <c>[12345, 15, 6]</c> — so the UI can
    /// show every intermediate sum instead of only the answer. A single digit yields one step.
    /// </summary>
    public static IReadOnlyList<long> Reduce(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        List<long> steps = [value];
        var current = value;
        while (current >= 10)
        {
            current = DigitSum(current);
            steps.Add(current);
        }

        return steps;
    }

    /// <summary>The A=1 … Z=26 value of <paramref name="character"/>, or 0 when it is not a Latin letter.</summary>
    public static int LetterValue(char character)
        => char.IsAsciiLetter(character) ? char.ToUpperInvariant(character) - 'A' + 1 : 0;

    /// <summary>
    /// Reads <paramref name="input"/> as a checksum: digits summed, letters scored A=1 … Z=26, each
    /// side reduced to its digital root, plus a per-part breakdown.
    /// </summary>
    /// <param name="input">Any mix of text, digits and punctuation.</param>
    /// <param name="foldDiacritics">
    /// Fold accented letters onto their base letter (Ž → Z, é → e, ß → s) before scoring. Turn it off
    /// to have them skipped instead.
    /// </param>
    public static ChecksumAnalysis Analyze(string? input, bool foldDiacritics = true)
    {
        if (string.IsNullOrEmpty(input))
        {
            return ChecksumAnalysis.Empty;
        }

        var source = foldDiacritics ? DiacriticFolder.Fold(input) : input;

        List<ChecksumTerm> digitTerms = [];
        List<ChecksumTerm> letterTerms = [];
        List<ChecksumPart> parts = [];
        StringBuilder partText = new();

        int digitCount = 0, letterCount = 0, ignored = 0, partCount = 0;
        long digitSum = 0, letterSum = 0, partDigits = 0, partLetters = 0;

        foreach (var character in source)
        {
            if (char.IsAsciiDigit(character))
            {
                var value = character - '0';
                digitCount++;
                digitSum += value;
                partDigits += value;
                partText.Append(character);
                Collect(digitTerms, character, value);
                continue;
            }

            var letterValue = LetterValue(character);
            if (letterValue > 0)
            {
                var upper = char.ToUpperInvariant(character);
                letterCount++;
                letterSum += letterValue;
                partLetters += letterValue;
                partText.Append(upper);
                Collect(letterTerms, upper, letterValue);
                continue;
            }

            ignored++;
            FlushPart();
        }

        FlushPart();

        if (digitCount == 0 && letterCount == 0)
        {
            return ChecksumAnalysis.Empty with { IgnoredCount = ignored };
        }

        return new ChecksumAnalysis(
            new ChecksumSum(digitCount, digitSum, Reduce(digitSum), digitTerms, digitTerms.Count < digitCount),
            new ChecksumSum(letterCount, letterSum, Reduce(letterSum), letterTerms, letterTerms.Count < letterCount),
            Reduce(digitSum + letterSum),
            parts,
            ignored,
            partCount > parts.Count);

        static void Collect(List<ChecksumTerm> terms, char character, int value)
        {
            if (terms.Count < MaxDetailTerms)
            {
                terms.Add(new ChecksumTerm(character, value));
            }
        }

        void FlushPart()
        {
            if (partText.Length == 0)
            {
                return;
            }

            partCount++;
            if (parts.Count < MaxParts)
            {
                var total = partDigits + partLetters;
                parts.Add(new ChecksumPart(partText.ToString(), partDigits, partLetters, DigitalRoot(total)));
            }

            partText.Clear();
            partDigits = 0;
            partLetters = 0;
        }
    }

    /// <summary>Absolute value as an unsigned magnitude, so <see cref="long.MinValue"/> — which has no
    /// positive counterpart — does not overflow.</summary>
    private static ulong Magnitude(long value)
        => value < 0 ? (ulong)(-(value + 1)) + 1 : (ulong)value;
}
