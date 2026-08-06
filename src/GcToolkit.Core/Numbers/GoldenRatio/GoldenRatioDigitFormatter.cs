using System.Text;

namespace GcToolkit.Core.Numbers.GoldenRatio;

/// <summary>
/// Formats a run of φ decimals for display: optional 10-digit blocks (geocachingtoolbox.com
/// convention) and optional 50-digit lines, each labeled with the absolute 1-based position of its
/// last digit.
/// </summary>
public static class GoldenRatioDigitFormatter
{
    public const int BlockSize = 10;
    public const int DigitsPerLine = 50;

    public static string Format(string decimals, int startPosition, bool groupBlocks, bool lineNumbers)
    {
        if (decimals.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(decimals.Length + decimals.Length / BlockSize + 64);
        if (!lineNumbers)
        {
            AppendDigits(builder, decimals, 0, decimals.Length, groupBlocks);
            return builder.ToString();
        }

        for (var offset = 0; offset < decimals.Length; offset += DigitsPerLine)
        {
            var lineLength = Math.Min(DigitsPerLine, decimals.Length - offset);
            if (offset > 0)
            {
                builder.Append('\n');
            }

            AppendDigits(builder, decimals, offset, lineLength, groupBlocks);
            builder.Append("  (").Append(startPosition + offset + lineLength - 1).Append(')');
        }

        return builder.ToString();
    }

    private static void AppendDigits(StringBuilder builder, string decimals, int offset, int length, bool groupBlocks)
    {
        if (!groupBlocks)
        {
            builder.Append(decimals, offset, length);
            return;
        }

        for (var i = 0; i < length; i += BlockSize)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(decimals, offset + i, Math.Min(BlockSize, length - i));
        }
    }
}
