using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// Bidirectional tap- / knock-code translator built on a Polybius square. A letter maps to a
/// <c>(row, column)</c> cell that can be spoken as two numbers (e.g. <c>O → "34"</c>) or knocked out as
/// row-taps then column-taps (e.g. <c>O → "... ...."</c>). Matches the cachesleuth.com tap-code tool: the
/// 5×5 square drops <c>K</c> (it shares the <c>C</c> cell), the optional 6×6 square adds the digits
/// <c>0–9</c>, and the three representations (letters, number pairs, dot taps) are fully interchangeable.
/// </summary>
/// <remarks>
/// Conventions:
/// <list type="bullet">
///   <item><description>Encoding folds accents and case (the Czech <c>Č</c> → <c>C</c>) and silently drops
///   any character that is not in the chosen square — spaces and punctuation have no tap.</description></item>
///   <item><description>Numbers join each <c>(row, column)</c> pair as two adjacent digits, pairs are
///   space-separated.</description></item>
///   <item><description>Dots render <c>row</c> taps, a single space, then <c>column</c> taps; letters are
///   separated by <b>two</b> spaces.</description></item>
///   <item><description>Decoding is lenient: digits drive number decoding; dots/bullets/slashes drive tap
///   decoding; any other separator is ignored, so messy copied input still decodes.</description></item>
/// </list>
/// </remarks>
public sealed class TapCode
{
    private const char Tap = '.';

    // Letters in cell order for the 5×5 square: the alphabet without K (K shares C's cell).
    private const string FiveByFiveLetters = "ABCDEFGHIJLMNOPQRSTUVWXYZ";

    // 6×6 square: the 26 letters (no merge) followed by the ten digits.
    private const string SixBySixLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>Encodes <paramref name="text"/> to space-separated <c>row column</c> number pairs.</summary>
    public string EncodeNumbers(string? text, TapCodeGrid grid) =>
        Encode(text, grid, static (row, column) => $"{row}{column}");

    /// <summary>Encodes <paramref name="text"/> to dot taps: row taps, a space, column taps; letters split by
    /// two spaces.</summary>
    public string EncodeDots(string? text, TapCodeGrid grid) =>
        Encode(text, grid, static (row, column) => $"{new string(Tap, row)} {new string(Tap, column)}", separator: "  ");

    /// <summary>Decodes space/punctuation-separated number pairs back to letters; lenient about
    /// separators.</summary>
    public string DecodeNumbers(string? numbers, TapCodeGrid grid)
    {
        if (string.IsNullOrWhiteSpace(numbers))
        {
            return string.Empty;
        }

        var digits = new List<int>();
        foreach (var ch in numbers)
        {
            if (char.IsDigit(ch))
            {
                digits.Add(ch - '0');
            }
        }

        var size = SizeOf(grid);
        var letters = LettersFor(grid);
        var builder = new StringBuilder(digits.Count / 2);

        // Every consecutive pair of digits is one (row, column) cell.
        for (var i = 0; i + 1 < digits.Count; i += 2)
        {
            AppendCell(builder, digits[i], digits[i + 1], size, letters);
        }

        return builder.ToString();
    }

    /// <summary>Decodes dot/tap groups back to letters. A tap is any of <c>. • · *</c>; runs of taps are split
    /// into row then column by any non-tap run, and read two groups at a time.</summary>
    public string DecodeDots(string? dots, TapCodeGrid grid)
    {
        if (string.IsNullOrWhiteSpace(dots))
        {
            return string.Empty;
        }

        // Collapse each maximal run of tap glyphs to its length; everything else is just a separator.
        var counts = new List<int>();
        var run = 0;
        foreach (var ch in dots)
        {
            if (IsTap(ch))
            {
                run++;
            }
            else if (run > 0)
            {
                counts.Add(run);
                run = 0;
            }
        }

        if (run > 0)
        {
            counts.Add(run);
        }

        var size = SizeOf(grid);
        var letters = LettersFor(grid);
        var builder = new StringBuilder(counts.Count / 2);

        for (var i = 0; i + 1 < counts.Count; i += 2)
        {
            AppendCell(builder, counts[i], counts[i + 1], size, letters);
        }

        return builder.ToString();
    }

    /// <summary>Builds the displayable Polybius square as rows of <see cref="TapCodeCell"/> (row 1 first).</summary>
    public static IReadOnlyList<IReadOnlyList<TapCodeCell>> BuildGrid(TapCodeGrid grid)
    {
        var size = SizeOf(grid);
        var letters = LettersFor(grid);
        var rows = new List<IReadOnlyList<TapCodeCell>>(size);

        for (var row = 0; row < size; row++)
        {
            var cells = new List<TapCodeCell>(size);
            for (var column = 0; column < size; column++)
            {
                var r = row + 1;
                var c = column + 1;
                // The 5×5 square shows the merged C cell as "C/K"; everything else is its single glyph.
                var label = grid == TapCodeGrid.FiveByFive && r == 1 && c == 3
                    ? "C/K"
                    : letters[row * size + column].ToString();
                cells.Add(new TapCodeCell(r, c, label, $"{r}{c}", $"{new string(Tap, r)} {new string(Tap, c)}"));
            }

            rows.Add(cells);
        }

        return rows;
    }

    private string Encode(string? text, TapCodeGrid grid, Func<int, int, string> render, string separator = " ")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var size = SizeOf(grid);
        var letters = LettersFor(grid);
        var parts = new List<string>();

        foreach (var raw in text)
        {
            if (!TryNormalize(raw, grid, out var symbol))
            {
                continue;
            }

            var index = letters.IndexOf(symbol);
            if (index < 0)
            {
                continue;
            }

            var row = index / size + 1;
            var column = index % size + 1;
            parts.Add(render(row, column));
        }

        return string.Join(separator, parts);
    }

    private static void AppendCell(StringBuilder builder, int row, int column, int size, string letters)
    {
        if (row < 1 || row > size || column < 1 || column > size)
        {
            return;
        }

        builder.Append(letters[(row - 1) * size + (column - 1)]);
    }

    /// <summary>Folds a character to the symbol stored in the square: upper-cased, accents stripped, and in
    /// the 5×5 square <c>K</c> mapped onto <c>C</c>. Returns <see langword="false"/> for anything not in the
    /// square (spaces, punctuation, digits in the 5×5 square).</summary>
    private static bool TryNormalize(char raw, TapCodeGrid grid, out char symbol)
    {
        symbol = char.ToUpperInvariant(raw);

        if (grid == TapCodeGrid.SixBySix && symbol is >= '0' and <= '9')
        {
            return true;
        }

        if (symbol is < 'A' or > 'Z')
        {
            // Try folding an accented Latin letter (Č → C, Á → A) to its base before giving up.
            var folded = FoldToBaseLetter(raw);
            if (folded is not char baseLetter)
            {
                return false;
            }

            symbol = baseLetter;
        }

        if (symbol is < 'A' or > 'Z')
        {
            return false;
        }

        if (grid == TapCodeGrid.FiveByFive && symbol == 'K')
        {
            symbol = 'C';
        }

        return true;
    }

    private static char? FoldToBaseLetter(char character)
    {
        foreach (var candidate in character.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(candidate) != UnicodeCategory.NonSpacingMark)
            {
                return char.ToUpperInvariant(candidate);
            }
        }

        return null;
    }

    private static bool IsTap(char ch) => ch is '.' or '•' or '·' or '*' or '○' or '●' or '◦';

    private static int SizeOf(TapCodeGrid grid) => grid == TapCodeGrid.SixBySix ? 6 : 5;

    private static string LettersFor(TapCodeGrid grid) =>
        grid == TapCodeGrid.SixBySix ? SixBySixLetters : FiveByFiveLetters;
}
