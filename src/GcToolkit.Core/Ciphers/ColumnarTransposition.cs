using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>Raised when a columnar key is empty, malformed, or not a valid 1..n column permutation.</summary>
public sealed class ColumnarException(string message) : Exception(message);

/// <summary>
/// Tunable options for a columnar transposition. Padding makes the grid a complete rectangle (with
/// <see cref="PadChar"/>); <see cref="StripPadding"/> removes trailing pad characters after a decode.
/// A non-empty <see cref="SecondKey"/> turns the transform into a double (two-stage) transposition.
/// </summary>
public sealed record ColumnarOptions
{
    /// <summary>Pad the plaintext so the grid is a complete rectangle.</summary>
    public bool Pad { get; init; }

    /// <summary>Character used to fill the trailing cells when <see cref="Pad"/> is on.</summary>
    public char PadChar { get; init; } = 'X';

    /// <summary>On decode, drop trailing <see cref="PadChar"/> characters from the result.</summary>
    public bool StripPadding { get; init; } = true;

    /// <summary>Optional second key for a double transposition (applied after the first on encode).</summary>
    public string? SecondKey { get; init; }

    /// <summary>The default options: irregular grid, no second pass, strip padding if any.</summary>
    public static ColumnarOptions Default { get; } = new();
}

/// <summary>The outcome of a transform: the resulting <see cref="Text"/>.</summary>
public readonly record struct ColumnarResult(string Text);

/// <summary>
/// A rectangular layout of the plaintext for preview: row-major cells (with <see langword="null"/>
/// for empty trailing cells of an irregular grid), plus the keyword letters and their resolved
/// numeric read order per column.
/// </summary>
public sealed class ColumnarGrid
{
    private readonly char?[,] _cells;

    internal ColumnarGrid(char?[,] cells, IReadOnlyList<char> columnLetters, IReadOnlyList<int> columnOrder)
    {
        _cells = cells;
        RowCount = cells.GetLength(0);
        ColumnCount = cells.GetLength(1);
        ColumnLetters = columnLetters;
        ColumnOrder = columnOrder;
    }

    public int RowCount { get; }

    public int ColumnCount { get; }

    /// <summary>The keyword letter shown above each column (a digit's char for a numeric key).</summary>
    public IReadOnlyList<char> ColumnLetters { get; }

    /// <summary>The 1-based read order of each column.</summary>
    public IReadOnlyList<int> ColumnOrder { get; }

    /// <summary>The character at <paramref name="row"/>/<paramref name="col"/>, or <see langword="null"/> when empty.</summary>
    public char? Cell(int row, int col) => _cells[row, col];
}

/// <summary>
/// A pure, stateless columnar transposition cipher — the single source of truth for the transform.
/// Plaintext is written row-by-row into a grid whose width is the key length; the columns are read
/// out in the order given by the alphabetical rank of each keyword letter (duplicates resolved
/// left-to-right) or by an explicit numeric permutation. Decode inverts this, correctly handling
/// the short last row of an irregular (unpadded) grid. Beyond the reference site it also supports a
/// numeric-key mode, optional padding with a configurable character, double transposition, and a
/// grid model for a live preview.
/// </summary>
public sealed class ColumnarTransposition
{
    /// <summary>
    /// Resolves the 1-based column read order for <paramref name="key"/>. A keyword is ranked by its
    /// letters alphabetically (ties left-to-right); a numeric key (e.g. <c>"3 1 4 2"</c>) is taken as
    /// the permutation directly. Throws <see cref="ColumnarException"/> for an empty or invalid key.
    /// </summary>
    public IReadOnlyList<int> ColumnOrder(string key)
    {
        var (order, _) = ResolveKey(key);
        return order;
    }

    /// <summary>
    /// Validates <paramref name="key"/> without throwing. On success reports the column count in
    /// <paramref name="length"/>; on failure returns a human-readable reason in <paramref name="error"/>.
    /// </summary>
    public bool TryValidateKey(string key, out int length, out string error)
    {
        try
        {
            var (order, _) = ResolveKey(key);
            length = order.Count;
            error = string.Empty;
            return true;
        }
        catch (ColumnarException ex)
        {
            length = 0;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Writes <paramref name="text"/> into rows and reads the columns in key order.</summary>
    public ColumnarResult Encrypt(string? text, string key, ColumnarOptions? options = null)
    {
        options ??= ColumnarOptions.Default;
        var prepared = Prepare(text, key, options);
        if (prepared.Length == 0)
        {
            return new ColumnarResult(string.Empty);
        }

        var first = EncryptOnce(prepared, key);
        if (string.IsNullOrWhiteSpace(options.SecondKey))
        {
            return new ColumnarResult(first);
        }

        // Double transposition: feed the first ciphertext through the second key.
        return new ColumnarResult(EncryptOnce(first, options.SecondKey));
    }

    /// <summary>Inverts <see cref="Encrypt(string?, string, ColumnarOptions?)"/>.</summary>
    public ColumnarResult Decrypt(string? text, string key, ColumnarOptions? options = null)
    {
        options ??= ColumnarOptions.Default;
        if (string.IsNullOrEmpty(text))
        {
            // Still validate the key so empty + bad key surfaces an error.
            _ = ResolveKey(key);
            return new ColumnarResult(string.Empty);
        }

        // Undo the second pass first (encryption applied it last).
        var stage = text;
        if (!string.IsNullOrWhiteSpace(options.SecondKey))
        {
            stage = DecryptOnce(stage, options.SecondKey);
        }

        var plain = DecryptOnce(stage, key);
        if (options is { Pad: true, StripPadding: true })
        {
            plain = plain.TrimEnd(options.PadChar);
        }

        return new ColumnarResult(plain);
    }

    /// <summary>Builds the rectangular preview grid the plaintext is written into for <paramref name="key"/>.</summary>
    public ColumnarGrid BuildGrid(string? text, string key)
    {
        var (order, letters) = ResolveKey(key);
        var cols = order.Count;
        var content = text ?? string.Empty;
        var rows = content.Length == 0 ? 0 : (content.Length + cols - 1) / cols;

        var cells = new char?[rows, cols];
        for (var i = 0; i < content.Length; i++)
        {
            cells[i / cols, i % cols] = content[i];
        }

        return new ColumnarGrid(cells, letters, order);
    }

    private string EncryptOnce(string text, string key)
    {
        var (order, _) = ResolveKey(key);
        var cols = order.Count;
        var rows = (text.Length + cols - 1) / cols;

        // Length of each column: the first (text.Length % cols) columns get an extra (last-row) char.
        var remainder = text.Length % cols;

        var builder = new StringBuilder(text.Length);
        // Read columns in ascending read-order (1..cols).
        for (var rank = 1; rank <= cols; rank++)
        {
            var col = order.IndexOf(rank);
            var height = rows - (remainder == 0 || col < remainder ? 0 : 1);
            for (var row = 0; row < height; row++)
            {
                var index = row * cols + col;
                if (index < text.Length)
                {
                    builder.Append(text[index]);
                }
            }
        }

        return builder.ToString();
    }

    private string DecryptOnce(string text, string key)
    {
        var (order, _) = ResolveKey(key);
        var cols = order.Count;
        var rows = (text.Length + cols - 1) / cols;
        var remainder = text.Length % cols;

        var grid = new char[text.Length];
        var read = 0;
        // Refill columns in the same read order the ciphertext was produced in.
        for (var rank = 1; rank <= cols; rank++)
        {
            var col = order.IndexOf(rank);
            var height = rows - (remainder == 0 || col < remainder ? 0 : 1);
            for (var row = 0; row < height; row++)
            {
                grid[row * cols + col] = text[read++];
            }
        }

        return new string(grid);
    }

    private string Prepare(string? text, string key, ColumnarOptions options)
    {
        var content = text ?? string.Empty;
        var (order, _) = ResolveKey(key);
        if (content.Length == 0)
        {
            return string.Empty;
        }

        if (!options.Pad)
        {
            return content;
        }

        var cols = order.Count;
        var remainder = content.Length % cols;
        if (remainder == 0)
        {
            return content;
        }

        return content + new string(options.PadChar, cols - remainder);
    }

    /// <summary>
    /// Resolves a key to its 1-based column order plus the per-column display letters. Accepts a
    /// keyword (ranked alphabetically, case-insensitive, ties left-to-right) or a numeric permutation
    /// delimited by spaces/commas. Throws <see cref="ColumnarException"/> on an empty/invalid key.
    /// </summary>
    private (IReadOnlyList<int> Order, IReadOnlyList<char> Letters) ResolveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ColumnarException("The key is empty.");
        }

        var trimmed = key.Trim();
        if (LooksNumeric(trimmed))
        {
            return ResolveNumericKey(trimmed);
        }

        return ResolveKeyword(trimmed);
    }

    private static bool LooksNumeric(string key) => key.Any(char.IsDigit) && key.All(c => char.IsDigit(c) || c is ' ' or ',' or '\t');

    private static (IReadOnlyList<int> Order, IReadOnlyList<char> Letters) ResolveNumericKey(string key)
    {
        var tokens = key.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var order = new int[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!int.TryParse(tokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                throw new ColumnarException($"'{tokens[i]}' is not a valid column number.");
            }

            order[i] = value;
        }

        // Must be a permutation of 1..n.
        var seen = new bool[order.Length];
        foreach (var value in order)
        {
            if (value < 1 || value > order.Length || seen[value - 1])
            {
                throw new ColumnarException($"The numeric key must list each of 1…{order.Length} exactly once.");
            }

            seen[value - 1] = true;
        }

        var letters = tokens.Select(t => t[0]).ToArray();
        return (order, letters);
    }

    private static (IReadOnlyList<int> Order, IReadOnlyList<char> Letters) ResolveKeyword(string keyword)
    {
        var letters = keyword.ToCharArray();
        var n = letters.Length;

        // Stable sort of positions by upper-cased letter; ties keep original order (left-to-right).
        var positions = Enumerable.Range(0, n)
            .OrderBy(i => char.ToUpperInvariant(letters[i]))
            .ThenBy(i => i)
            .ToArray();

        // Assign 1-based read order back to each original column position.
        var order = new int[n];
        for (var rank = 0; rank < n; rank++)
        {
            order[positions[rank]] = rank + 1;
        }

        return (order, letters);
    }
}
