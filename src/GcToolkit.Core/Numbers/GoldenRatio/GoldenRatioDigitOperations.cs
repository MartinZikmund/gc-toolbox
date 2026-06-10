namespace GcToolkit.Core.Numbers.GoldenRatio;

/// <summary>
/// Pure lookups over the φ decimal string: digit at a position, an inclusive range, and
/// digit-sequence search (overlapping matches count). Positions are 1-based: position 1 is the
/// first digit after the decimal point, matching geocachingtoolbox.com conventions.
/// </summary>
public static class GoldenRatioDigitOperations
{
    /// <summary>Digits of surrounding context captured on each side of a search match.</summary>
    public const int ContextRadius = 10;

    public static char DigitAt(string decimals, int position)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, decimals.Length);
        return decimals[position - 1];
    }

    /// <summary>Decimals from <paramref name="from"/> up to and including <paramref name="to"/>.</summary>
    public static string Slice(string decimals, int from, int to)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(from, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(to, decimals.Length, nameof(to));
        ArgumentOutOfRangeException.ThrowIfLessThan(to, from, nameof(to));
        return decimals[(from - 1)..to];
    }

    /// <summary>Up to <paramref name="radius"/> digits on each side of the run starting at
    /// <paramref name="position"/> (1-based) with <paramref name="length"/> digits, clamped to the
    /// available decimals.</summary>
    public static (string Before, string After) Context(string decimals, int position, int length, int radius)
    {
        var start = position - 1;
        var beforeStart = Math.Max(0, start - radius);
        var afterStart = Math.Min(decimals.Length, start + length);
        var afterEnd = Math.Min(decimals.Length, afterStart + radius);
        return (decimals[beforeStart..start], decimals[afterStart..afterEnd]);
    }

    /// <summary>Finds every (overlapping) occurrence of <paramref name="pattern"/>; at most
    /// <paramref name="maxOccurrences"/> are materialized with context, but all are counted.</summary>
    public static GoldenRatioSearchResult FindOccurrences(string decimals, string pattern, int maxOccurrences)
    {
        if (pattern.Length == 0 || !pattern.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("The pattern must be one or more digits 0–9.", nameof(pattern));
        }

        var total = 0;
        List<GoldenRatioOccurrence> occurrences = [];
        var index = decimals.IndexOf(pattern, StringComparison.Ordinal);
        while (index >= 0)
        {
            total++;
            if (occurrences.Count < maxOccurrences)
            {
                var (before, after) = Context(decimals, index + 1, pattern.Length, ContextRadius);
                occurrences.Add(new GoldenRatioOccurrence(index + 1, before, pattern, after));
            }

            index = index + 1 > decimals.Length - pattern.Length
                ? -1
                : decimals.IndexOf(pattern, index + 1, StringComparison.Ordinal);
        }

        return new GoldenRatioSearchResult(total, occurrences, total > occurrences.Count);
    }
}

/// <summary>One match of a digit sequence: its 1-based position plus surrounding digits.</summary>
public sealed record GoldenRatioOccurrence(int Position, string Before, string Match, string After);

/// <summary>Search outcome: the full match count and the materialized (possibly capped) matches.</summary>
public sealed record GoldenRatioSearchResult(
    int TotalCount,
    IReadOnlyList<GoldenRatioOccurrence> Occurrences,
    bool IsTruncated);
