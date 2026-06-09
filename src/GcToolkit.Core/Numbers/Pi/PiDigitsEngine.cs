namespace GcToolkit.Core.Numbers.Pi;

/// <summary>
/// Pure lookup/search operations over a string of π decimals. Positions are 1-based:
/// position 1 is the first digit after the decimal point (geocachingtoolbox.com convention).
/// </summary>
public static class PiDigitsEngine
{
    /// <summary>Returns the first <paramref name="count"/> decimals.</summary>
    public static string First(string decimals, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, decimals.Length);
        return decimals[..count];
    }

    /// <summary>Returns the decimal at the 1-based <paramref name="position"/>.</summary>
    public static char DigitAt(string decimals, int position)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, decimals.Length);
        return decimals[position - 1];
    }

    /// <summary>Returns the decimals from <paramref name="fromPosition"/> up to and including <paramref name="toPosition"/>.</summary>
    public static string Range(string decimals, int fromPosition, int toPosition)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fromPosition, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(toPosition, decimals.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(toPosition, fromPosition);
        return decimals[(fromPosition - 1)..toPosition];
    }

    /// <summary>
    /// Finds all (overlapping) occurrences of a digit sequence, returning up to
    /// <paramref name="maxPositions"/> 1-based positions plus the total occurrence count.
    /// </summary>
    public static PiOccurrences Find(string decimals, string pattern, int maxPositions)
    {
        if (pattern.Length == 0 || !pattern.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Pattern must be one or more digits.", nameof(pattern));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxPositions, 1);

        List<int> positions = [];
        var totalCount = 0;

        // Overlapping search: advance by one so e.g. "99" is found twice in "999".
        var index = decimals.IndexOf(pattern, StringComparison.Ordinal);
        while (index >= 0)
        {
            totalCount++;
            if (positions.Count < maxPositions)
            {
                positions.Add(index + 1);
            }

            index = index + 1 > decimals.Length - pattern.Length
                ? -1
                : decimals.IndexOf(pattern, index + 1, StringComparison.Ordinal);
        }

        return new PiOccurrences(positions, totalCount);
    }

    /// <summary>
    /// Returns up to <paramref name="radius"/> digits on each side of the match starting at
    /// 1-based <paramref name="fromPosition"/> with the given <paramref name="length"/>.
    /// </summary>
    public static PiDigitsContext Context(string decimals, int fromPosition, int length, int radius)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fromPosition, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fromPosition + length - 1, decimals.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        var startIndex = fromPosition - 1;
        var beforeStart = Math.Max(0, startIndex - radius);
        var afterEnd = Math.Min(decimals.Length, startIndex + length + radius);

        return new PiDigitsContext(
            Before: decimals[beforeStart..startIndex],
            Match: decimals.Substring(startIndex, length),
            After: decimals[(startIndex + length)..afterEnd],
            HasMoreBefore: beforeStart > 0,
            HasMoreAfter: afterEnd < decimals.Length);
    }
}

/// <summary>Positions (1-based, possibly truncated) and the total count of a digit-sequence search.</summary>
public sealed record PiOccurrences(IReadOnlyList<int> Positions, int TotalCount)
{
    public bool IsTruncated => TotalCount > Positions.Count;
}

/// <summary>Digits surrounding a match, for "show in context" displays.</summary>
public sealed record PiDigitsContext(string Before, string Match, string After, bool HasMoreBefore, bool HasMoreAfter);
