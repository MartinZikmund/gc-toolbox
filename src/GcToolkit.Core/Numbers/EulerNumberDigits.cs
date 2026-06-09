using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>
/// Lookup, slicing and search operations over the decimal expansion of Euler's number e
/// (the digits after "2."). Positions are 1-based: position 1 is the first decimal, 7.
/// </summary>
public sealed class EulerNumberDigits(string decimals)
{
    private const int DigitsPerGroup = 10;
    private const int GroupsPerLine = 5;
    private const int DigitsPerLine = DigitsPerGroup * GroupsPerLine;

    /// <summary>The integer part of e, shown before the decimal point.</summary>
    public const string IntegerPart = "2";

    private readonly string _decimals = decimals;

    /// <summary>Number of available decimals.</summary>
    public int Count => _decimals.Length;

    /// <summary>The first <paramref name="count"/> decimals.</summary>
    public string First(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Count);
        return _decimals[..count];
    }

    /// <summary>The decimal at 1-based <paramref name="position"/>.</summary>
    public char DigitAt(int position)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, Count);
        return _decimals[position - 1];
    }

    /// <summary>The decimals from <paramref name="fromPosition"/> up to and including <paramref name="toPosition"/>.</summary>
    public string Range(int fromPosition, int toPosition)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fromPosition, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(toPosition, Count);
        ArgumentOutOfRangeException.ThrowIfLessThan(toPosition, fromPosition);
        return _decimals[(fromPosition - 1)..toPosition];
    }

    /// <summary>
    /// Finds every (overlapping) occurrence of <paramref name="pattern"/>. All hits are counted;
    /// at most <paramref name="maxMatches"/> are materialized, each with up to
    /// <paramref name="contextLength"/> digits of surrounding context.
    /// </summary>
    public EulerNumberSearchResult Find(string pattern, int maxMatches, int contextLength)
    {
        if (string.IsNullOrEmpty(pattern) || !pattern.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Pattern must be one or more digits.", nameof(pattern));
        }

        List<EulerNumberMatch> matches = [];
        var totalCount = 0;

        var index = _decimals.IndexOf(pattern, StringComparison.Ordinal);
        while (index >= 0)
        {
            totalCount++;
            if (matches.Count < maxMatches)
            {
                var beforeStart = Math.Max(0, index - contextLength);
                var afterEnd = Math.Min(_decimals.Length, index + pattern.Length + contextLength);
                matches.Add(new EulerNumberMatch(
                    Position: index + 1,
                    Before: _decimals[beforeStart..index],
                    Match: pattern,
                    After: _decimals[(index + pattern.Length)..afterEnd]));
            }

            // Advance by one so overlapping occurrences are found too.
            index = _decimals.IndexOf(pattern, index + 1, StringComparison.Ordinal);
        }

        return new EulerNumberSearchResult(totalCount, matches);
    }

    /// <summary>
    /// Formats a digit run for display: blocks of 10 digits, 50 digits per line, with an optional
    /// position label at the start of each line. With both options off the raw digits are returned.
    /// </summary>
    public static string FormatGrouped(string digits, int startPosition, bool groupDigits, bool showPositions)
    {
        if (!groupDigits && !showPositions)
        {
            return digits;
        }

        if (digits.Length == 0)
        {
            return string.Empty;
        }

        var labelWidth = (startPosition + digits.Length - 1).ToString().Length;
        StringBuilder builder = new(digits.Length + digits.Length / DigitsPerGroup + (digits.Length / DigitsPerLine + 1) * (labelWidth + 4));

        for (var lineStart = 0; lineStart < digits.Length; lineStart += DigitsPerLine)
        {
            if (lineStart > 0)
            {
                builder.AppendLine();
            }

            if (showPositions)
            {
                builder.Append((startPosition + lineStart).ToString().PadLeft(labelWidth)).Append(": ");
            }

            var lineEnd = Math.Min(lineStart + DigitsPerLine, digits.Length);
            for (var groupStart = lineStart; groupStart < lineEnd; groupStart += DigitsPerGroup)
            {
                if (groupDigits && groupStart > lineStart)
                {
                    builder.Append(' ');
                }

                builder.Append(digits, groupStart, Math.Min(DigitsPerGroup, lineEnd - groupStart));
            }
        }

        return builder.ToString();
    }
}
