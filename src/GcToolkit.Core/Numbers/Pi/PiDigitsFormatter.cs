using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Numbers.Pi;

/// <summary>
/// Renders a run of π decimals for display: optional blocks of 10 digits, 50 digits per line,
/// and optional 1-based position labels at line starts (geocachingtoolbox.com-style output).
/// </summary>
public static class PiDigitsFormatter
{
    public const int GroupSize = 10;
    public const int GroupsPerLine = 5;
    public const int DigitsPerLine = GroupSize * GroupsPerLine;

    /// <summary>
    /// Formats <paramref name="digits"/> whose first digit sits at the absolute 1-based
    /// <paramref name="startPosition"/>. With both options off the raw digit string is returned.
    /// </summary>
    public static string Format(string digits, int startPosition, bool groupDigits, bool lineNumbers)
    {
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        if (!groupDigits && !lineNumbers)
        {
            return digits;
        }

        var lastLineStart = startPosition + ((digits.Length - 1) / DigitsPerLine) * DigitsPerLine;
        var labelWidth = lastLineStart.ToString(CultureInfo.InvariantCulture).Length;

        StringBuilder builder = new(digits.Length + digits.Length / GroupSize + 16);
        for (var lineStart = 0; lineStart < digits.Length; lineStart += DigitsPerLine)
        {
            if (lineStart > 0)
            {
                builder.Append('\n');
            }

            if (lineNumbers)
            {
                builder
                    .Append((startPosition + lineStart).ToString(CultureInfo.InvariantCulture).PadLeft(labelWidth))
                    .Append(": ");
            }

            var lineLength = Math.Min(DigitsPerLine, digits.Length - lineStart);
            if (groupDigits)
            {
                for (var blockStart = 0; blockStart < lineLength; blockStart += GroupSize)
                {
                    if (blockStart > 0)
                    {
                        builder.Append(' ');
                    }

                    builder.Append(digits, lineStart + blockStart, Math.Min(GroupSize, lineLength - blockStart));
                }
            }
            else
            {
                builder.Append(digits, lineStart, lineLength);
            }
        }

        return builder.ToString();
    }
}
