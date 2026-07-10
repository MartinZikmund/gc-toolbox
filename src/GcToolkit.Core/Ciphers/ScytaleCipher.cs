namespace GcToolkit.Core.Ciphers;

/// <summary>One brute-force candidate: the <see cref="Columns"/> tried and the resulting <see cref="Text"/>.</summary>
public readonly record struct ScytaleSolveResult(int Columns, string Text);

/// <summary>
/// A pure, stateless Scytale (rod) cipher — the single source of truth for the transform. The Scytale
/// is an ancient columnar transposition: the plaintext is written across a fixed number of
/// <c>columns</c> (the rod's diameter) row-by-row, then read off column-by-column. It only reorders
/// characters — nothing is added or dropped unless <c>ignoreSpaces</c> strips spaces or a
/// <c>padChar</c> completes the final row. Beyond cachesleuth.com parity (which makes you guess the
/// column count by hand), <see cref="AutoSolve"/> brute-forces every plausible column count and labels
/// each candidate decoding.
/// </summary>
public sealed class ScytaleCipher
{
    /// <summary>The smallest meaningful rod diameter — one column would be a no-op.</summary>
    public const int MinColumns = 2;

    /// <summary>
    /// Encrypts <paramref name="text"/> by writing it across <paramref name="columns"/> columns
    /// row-by-row, then reading down each column in turn. When <paramref name="ignoreSpaces"/> is set,
    /// spaces are removed before transposing. An optional <paramref name="padChar"/> fills the final
    /// incomplete row so the grid is rectangular.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the ciphertext.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="columns"/> is below <see cref="MinColumns"/>.</exception>
    public string Encrypt(string? text, int columns, bool ignoreSpaces, char? padChar = null)
    {
        EnsureColumns(columns);

        var source = Prepare(text, ignoreSpaces);
        if (source.Length == 0)
        {
            return string.Empty;
        }

        if (padChar is { } pad)
        {
            source = Pad(source, columns, pad);
        }

        var n = source.Length;
        var result = new char[n];
        var index = 0;

        // Read down each column: column j holds positions j, j+columns, j+2*columns, …
        for (var col = 0; col < columns; col++)
        {
            for (var pos = col; pos < n; pos += columns)
            {
                result[index++] = source[pos];
            }
        }

        return new string(result);
    }

    /// <summary>
    /// Decrypts <paramref name="text"/> — the exact inverse of <see cref="Encrypt"/> for the same
    /// <paramref name="columns"/>. When <paramref name="ignoreSpaces"/> is set, the recovered text has
    /// no spaces (they were never part of the transposition).
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the plaintext.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="columns"/> is below <see cref="MinColumns"/>.</exception>
    public string Decrypt(string? text, int columns, bool ignoreSpaces)
    {
        EnsureColumns(columns);

        var source = Prepare(text, ignoreSpaces);
        var n = source.Length;
        if (n == 0)
        {
            return string.Empty;
        }

        var result = new char[n];
        var index = 0;

        // Rebuild the row-major grid by walking the same column order Encrypt read in.
        for (var col = 0; col < columns; col++)
        {
            for (var pos = col; pos < n; pos += columns)
            {
                result[pos] = source[index++];
            }
        }

        return new string(result);
    }

    /// <summary>
    /// Brute-forces every plausible rod diameter for <paramref name="text"/> — column counts
    /// <c>2 … length</c> — and returns each candidate decoding labelled with its column count. The key
    /// "guess the columns" win over cachesleuth, which leaves that to the solver. Spaces are kept as
    /// part of the transposition (they would have been kept when the message was encoded if visible).
    /// </summary>
    public IReadOnlyList<ScytaleSolveResult> AutoSolve(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var max = text.Length;
        var results = new List<ScytaleSolveResult>(Math.Max(0, max - MinColumns + 1));
        for (var columns = MinColumns; columns <= max; columns++)
        {
            results.Add(new ScytaleSolveResult(columns, Decrypt(text, columns, ignoreSpaces: false)));
        }

        return results;
    }

    private static string Prepare(string? text, bool ignoreSpaces)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return ignoreSpaces ? text.Replace(" ", string.Empty) : text;
    }

    private static string Pad(string text, int columns, char pad)
    {
        var remainder = text.Length % columns;
        if (remainder == 0)
        {
            return text;
        }

        return text + new string(pad, columns - remainder);
    }

    private static void EnsureColumns(int columns)
    {
        if (columns < MinColumns)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, $"Columns must be at least {MinColumns}.");
        }
    }
}
