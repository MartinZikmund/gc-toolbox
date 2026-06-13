namespace GcToolkit.Core.Ciphers;

/// <summary>One auto-solve candidate: the <see cref="Rails"/> and <see cref="Offset"/> tried and the resulting <see cref="Text"/>.</summary>
public readonly record struct RailFenceSolveResult(int Rails, int Offset, string Text);

/// <summary>
/// A pure, stateless Rail Fence (zig-zag) transposition cipher — the single source of truth for the
/// transform. The plaintext is written diagonally down and up across a number of "rails" (rows), then
/// read off row by row. It is a *pure transposition*: every input character — letters of any case,
/// digits, spaces and punctuation — is one cell, nothing is dropped and case is preserved, so
/// coordinate strings such as <c>"N50 12.345 E014 23.456"</c> round-trip exactly.
/// Beyond geocachingtoolbox.com parity (number of rails + a starting offset), this codec also builds a
/// visual fence layout and brute-forces an unknown key via <see cref="AutoSolve"/>.
/// </summary>
public sealed class RailFenceCipher
{
    /// <summary>Placeholder used for empty cells in the <see cref="BuildFence"/> visualization.</summary>
    public const char EmptyCell = '.';

    /// <summary>
    /// Writes <paramref name="text"/> in a zig-zag down and up across <paramref name="rails"/> rails
    /// (an optional <paramref name="offset"/> shifts the starting position within the zig-zag cycle),
    /// then reads the rails off row by row.
    /// </summary>
    /// <param name="rails">Number of rails — must be at least 2.</param>
    /// <param name="offset">Starting position within the zig-zag cycle (length <c>2·(rails−1)</c>) — must be ≥ 0.</param>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the transposed text.</returns>
    public string Encrypt(string? text, int rails, int offset = 0)
    {
        Validate(rails, offset);
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var pattern = RailPattern(text.Length, rails, offset);
        var rows = new System.Text.StringBuilder[rails];
        for (var r = 0; r < rails; r++)
        {
            rows[r] = new System.Text.StringBuilder();
        }

        for (var i = 0; i < text.Length; i++)
        {
            rows[pattern[i]].Append(text[i]);
        }

        var result = new System.Text.StringBuilder(text.Length);
        foreach (var row in rows)
        {
            result.Append(row);
        }

        return result.ToString();
    }

    /// <summary>
    /// The exact inverse of <see cref="Encrypt"/>: rebuilds the zig-zag rail pattern for the given
    /// length/rails/offset, slices <paramref name="text"/> into the per-rail segments, then reads the
    /// pattern in zig-zag order to recover the plaintext.
    /// </summary>
    public string Decrypt(string? text, int rails, int offset = 0)
    {
        Validate(rails, offset);
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var pattern = RailPattern(text.Length, rails, offset);

        // How many characters land on each rail — these are the segment lengths of the ciphertext.
        var counts = new int[rails];
        foreach (var rail in pattern)
        {
            counts[rail]++;
        }

        // Where each rail's segment starts inside the read-off ciphertext.
        var cursor = new int[rails];
        var start = 0;
        for (var r = 0; r < rails; r++)
        {
            cursor[r] = start;
            start += counts[r];
        }

        var result = new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var rail = pattern[i];
            result[i] = text[cursor[rail]];
            cursor[rail]++;
        }

        return new string(result);
    }

    /// <summary>
    /// A rows-by-columns view of the zig-zag for visualization: <paramref name="rails"/> strings, each
    /// the full text length, with the character in its zig-zag cell and <paramref name="placeholder"/>
    /// (default <see cref="EmptyCell"/>) everywhere else.
    /// </summary>
    public IReadOnlyList<string> BuildFence(string? text, int rails, int offset = 0, char placeholder = EmptyCell)
    {
        Validate(rails, offset);
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var pattern = RailPattern(text.Length, rails, offset);
        var grid = new char[rails][];
        for (var r = 0; r < rails; r++)
        {
            grid[r] = new char[text.Length];
            Array.Fill(grid[r], placeholder);
        }

        for (var i = 0; i < text.Length; i++)
        {
            grid[pattern[i]][i] = text[i];
        }

        var rows = new string[rails];
        for (var r = 0; r < rails; r++)
        {
            rows[r] = new string(grid[r]);
        }

        return rows;
    }

    /// <summary>
    /// Brute-forces an unknown key: decrypts <paramref name="text"/> for every rail count
    /// <c>2 … length</c> and every offset within that rail count's cycle, returning each distinct
    /// (rails, offset) candidate. For an unknown rail fence the plaintext is one of these rows. With
    /// <paramref name="allOffsets"/> = <see langword="false"/> (the default) only the standard offset 0
    /// is tried — one candidate per rail count, which is the common case and keeps the list short;
    /// pass <see langword="true"/> to also sweep every starting offset.
    /// </summary>
    public IReadOnlyList<RailFenceSolveResult> AutoSolve(string? text, bool allOffsets = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var results = new List<RailFenceSolveResult>();
        var maxRails = text.Length;
        for (var rails = 2; rails <= maxRails; rails++)
        {
            var maxOffset = allOffsets ? 2 * (rails - 1) : 1;
            for (var offset = 0; offset < maxOffset; offset++)
            {
                results.Add(new RailFenceSolveResult(rails, offset, Decrypt(text, rails, offset)));
            }
        }

        return results;
    }

    /// <summary>
    /// The rail index each character lands on. The zig-zag walks <c>0 → rails-1 → 0</c> with cycle
    /// length <c>2·(rails−1)</c>; <paramref name="offset"/> advances the start within that cycle.
    /// </summary>
    private static int[] RailPattern(int length, int rails, int offset)
    {
        var cycle = 2 * (rails - 1);
        var pattern = new int[length];
        for (var i = 0; i < length; i++)
        {
            var pos = (i + offset) % cycle;
            pattern[i] = pos < rails ? pos : cycle - pos;
        }

        return pattern;
    }

    private static void Validate(int rails, int offset)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rails, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
    }
}
