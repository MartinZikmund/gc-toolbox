using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// How the keyword + remaining alphabet are laid into the 27 cells of the 3×3×3 cube.
/// Each value fixes which axis varies fastest while the sequence is placed (geocachingtoolbox.com
/// offers exactly these four). All four produce a valid cube; only the cell ordering differs.
/// </summary>
public enum TrifidFillOrder
{
    /// <summary>Classic Delastelle layout: fill square 1 row-by-row, then square 2, then square 3
    /// (layer slowest, row, then column fastest). The default.</summary>
    SquareFirstByRow,

    /// <summary>Fill square 1 column-by-column, then square 2, then square 3 (layer slowest, column, then row fastest).</summary>
    SquareFirstByColumn,

    /// <summary>Fill row 1 across all three squares, then row 2, then row 3 (row slowest, layer, then column fastest).</summary>
    RowFirst,

    /// <summary>Fill column 1 down all three squares, then column 2, then column 3 (column slowest, layer, then row fastest).</summary>
    ColumnFirst,
}

/// <summary>
/// The order in which the three per-letter coordinate streams (Square/layer, Row, Column) are
/// concatenated before being re-read in triples. "There are different methods in use" — all six
/// permutations are supported so an unknown puzzle's variant can be matched. Square-Row-Column is
/// the classic Delastelle order and the default.
/// </summary>
public enum TrifidReadingOrder
{
    SquareRowColumn,
    SquareColumnRow,
    RowSquareColumn,
    RowColumnSquare,
    ColumnSquareRow,
    ColumnRowSquare,
}

/// <summary>The outcome of a transform: the produced <see cref="Text"/> and how many input characters
/// were dropped because they were outside the 27-symbol alphabet.</summary>
public readonly record struct TrifidResult(string Text, int DroppedCount);

/// <summary>One candidate produced by the offline auto-solver, tagged with the settings that made it
/// and a crude readability <see cref="Score"/> (higher = more letter-like for EN/CS).</summary>
public readonly record struct TrifidSolveCandidate(
    TrifidReadingOrder ReadingOrder,
    TrifidFillOrder FillOrder,
    string Text,
    double Score);

/// <summary>
/// A pure, stateless Trifid (Félix Delastelle, ~1901) fractionating cipher — the single source of
/// truth for the transform. A 27-symbol alphabet (A–Z plus one configurable filler) is laid into a
/// 3×3×3 cube; every letter becomes three coordinates (layer, row, column, each 1..3). Within a
/// fixed-length period the three coordinate streams are concatenated (per the chosen reading order)
/// and re-read in triples back through the cube. Supports all four fill orders, all six reading
/// orders, an arbitrary period (or the whole message as one block), an explicit cube, visible input
/// normalisation (case-fold, strip diacritics, drop out-of-alphabet), cube validation, and an
/// offline auto-solver. Beyond geocachingtoolbox.com parity, normalisation reports the dropped count
/// and the solver ranks candidates locally (no Cloud Solve).
/// </summary>
public sealed class TrifidCipher
{
    /// <summary>The number of cells in the cube (and symbols in the alphabet).</summary>
    public const int CubeSize = 27;

    /// <summary>The 26 Latin letters that always seed the alphabet before the filler.</summary>
    public const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>The conventional 27th filler symbol when none is specified.</summary>
    public const char DefaultFiller = '+';

    /// <summary>The standard A–Z+ alphabet in classic (square-first-by-row) order.</summary>
    public static string StandardAlphabet => Letters + DefaultFiller;

    /// <summary>
    /// Builds the 27-symbol cube alphabet from an optional <paramref name="keyword"/> and
    /// <paramref name="filler"/>, placed in the cells per <paramref name="fillOrder"/>. Keyword
    /// letters (case-folded, diacritics stripped, de-duplicated, out-of-alphabet dropped) seed the
    /// sequence; the remaining letters A–Z then the filler fill the rest. The returned string is the
    /// alphabet in <see cref="TrifidFillOrder.SquareFirstByRow"/> linear order (index =
    /// (layer-1)*9 + (row-1)*3 + (col-1)) regardless of the fill order, so it can be indexed directly.
    /// </summary>
    public string BuildAlphabet(
        string? keyword = null,
        char filler = DefaultFiller,
        TrifidFillOrder fillOrder = TrifidFillOrder.SquareFirstByRow)
    {
        // 1. Build the placement sequence: deduped keyword letters, then the rest of A–Z, then filler.
        var sequence = BuildSequence(keyword, filler);

        // 2. Scatter that sequence into cube cells in the requested fill order, returning the
        //    canonical square-first-by-row alphabet so callers always index it the same way.
        var canonical = new char[CubeSize];
        var seqIndex = 0;
        foreach (var linear in FillOrderCells(fillOrder))
        {
            canonical[linear] = sequence[seqIndex++];
        }

        return new string(canonical);
    }

    /// <summary>
    /// Validates an explicit 27-cell cube alphabet: it must be exactly 27 symbols with no duplicates.
    /// On success <paramref name="normalized"/> is the upper-cased alphabet ready for use.
    /// </summary>
    public bool TryValidateCube(string? alphabet, out string normalized, out string? error)
    {
        normalized = string.Empty;
        if (string.IsNullOrEmpty(alphabet))
        {
            error = "The cube must contain exactly 27 symbols.";
            return false;
        }

        var upper = alphabet.ToUpperInvariant();
        if (upper.Length != CubeSize)
        {
            error = "The cube must contain exactly 27 symbols.";
            return false;
        }

        var seen = new HashSet<char>();
        foreach (var c in upper)
        {
            if (!seen.Add(c))
            {
                error = $"Duplicate symbol '{c}' — every cube cell must be unique.";
                return false;
            }
        }

        normalized = upper;
        error = null;
        return true;
    }

    /// <summary>
    /// Encrypts <paramref name="text"/> with the given <paramref name="alphabet"/>, period and reading
    /// order. Input is normalised (case-fold, strip diacritics, drop symbols not in the alphabet) and
    /// the dropped count is reported. A <paramref name="period"/> of 0 or less treats the whole message
    /// as one block.
    /// </summary>
    public TrifidResult Encrypt(string? text, string alphabet, int period, TrifidReadingOrder readingOrder)
        => Run(text, alphabet, period, readingOrder, decrypt: false);

    /// <summary>The inverse of <see cref="Encrypt"/> with the same settings.</summary>
    public TrifidResult Decrypt(string? text, string alphabet, int period, TrifidReadingOrder readingOrder)
        => Run(text, alphabet, period, readingOrder, decrypt: true);

    /// <summary>
    /// Normalises raw input against <paramref name="alphabet"/>: upper-cases, strips diacritics to the
    /// base Latin letter, and drops every character not present in the alphabet. Reports how many were
    /// dropped so nothing silently disappears.
    /// </summary>
    public string Normalize(string? text, string alphabet, out int dropped)
    {
        dropped = 0;
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var lookup = BuildLookup(alphabet);
        var builder = new StringBuilder(text.Length);
        foreach (var raw in text)
        {
            var c = char.ToUpperInvariant(raw);
            if (lookup.ContainsKey(c))
            {
                builder.Append(c);
                continue;
            }

            if (FoldToBaseLetter(c) is char folded && lookup.ContainsKey(folded))
            {
                builder.Append(folded);
                continue;
            }

            // Whitespace and any other out-of-alphabet symbol is dropped (and counted).
            if (!char.IsWhiteSpace(raw))
            {
                dropped++;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Offline auto-solver: re-runs decryption across every reading order (and, when
    /// <paramref name="includeFillOrders"/> is set, every keyword fill order) and ranks the candidates
    /// by EN/CS letter-likeness so the readable plaintext stands out. The cube is rebuilt per fill
    /// order from <paramref name="keyword"/>/<paramref name="filler"/>.
    /// </summary>
    public IReadOnlyList<TrifidSolveCandidate> Solve(
        string? cipherText,
        string? keyword,
        char filler,
        int period,
        bool includeFillOrders = false)
    {
        var fillOrders = includeFillOrders
            ? Enum.GetValues<TrifidFillOrder>()
            : [TrifidFillOrder.SquareFirstByRow];

        var candidates = new List<TrifidSolveCandidate>();
        foreach (var fillOrder in fillOrders)
        {
            var alphabet = BuildAlphabet(keyword, filler, fillOrder);
            foreach (var readingOrder in Enum.GetValues<TrifidReadingOrder>())
            {
                var result = Decrypt(cipherText, alphabet, period, readingOrder);
                candidates.Add(new TrifidSolveCandidate(
                    readingOrder, fillOrder, result.Text, ScoreReadability(result.Text)));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        return candidates;
    }

    private TrifidResult Run(string? text, string alphabet, int period, TrifidReadingOrder readingOrder, bool decrypt)
    {
        var normalized = Normalize(text, alphabet, out var dropped);
        if (normalized.Length == 0)
        {
            return new TrifidResult(string.Empty, dropped);
        }

        var lookup = BuildLookup(alphabet);
        var builder = new StringBuilder(normalized.Length);

        var blockSize = period > 0 ? period : normalized.Length;
        for (var start = 0; start < normalized.Length; start += blockSize)
        {
            var length = Math.Min(blockSize, normalized.Length - start);
            TransformBlock(normalized, start, length, alphabet, lookup, readingOrder, decrypt, builder);
        }

        return new TrifidResult(builder.ToString(), dropped);
    }

    /// <summary>
    /// Transforms one period block. Encryption: stack each letter's three coordinates as columns,
    /// concatenate the three rows in the reading order, then re-read the flat stream in triples.
    /// Decryption walks the same wiring backwards.
    /// </summary>
    private void TransformBlock(
        string text,
        int start,
        int length,
        string alphabet,
        IReadOnlyDictionary<char, int> lookup,
        TrifidReadingOrder readingOrder,
        bool decrypt,
        StringBuilder output)
    {
        // axes[stream] tells which coordinate (0=layer,1=row,2=col) feeds output row `stream`.
        var axes = AxisOrder(readingOrder);

        // coords[letter] = (layer, row, col), each 1..3.
        var coords = new (int Layer, int Row, int Col)[length];
        for (var i = 0; i < length; i++)
        {
            var linear = lookup[text[start + i]];
            coords[i] = (linear / 9 + 1, linear / 3 % 3 + 1, linear % 3 + 1);
        }

        if (!decrypt)
        {
            // Flatten: row0 = all letters' first-stream digit, then row1, then row2.
            var flat = new int[length * 3];
            var k = 0;
            for (var stream = 0; stream < 3; stream++)
            {
                var axis = axes[stream];
                for (var i = 0; i < length; i++)
                {
                    flat[k++] = AxisValue(coords[i], axis);
                }
            }

            // Re-read in triples down the flat stream → one letter per triple.
            for (var i = 0; i < length; i++)
            {
                var layer = flat[i * 3] - 1;
                var row = flat[i * 3 + 1] - 1;
                var col = flat[i * 3 + 2] - 1;
                output.Append(alphabet[layer * 9 + row * 3 + col]);
            }
        }
        else
        {
            // Decrypt inverts: split each ciphertext letter into a triple, lay them flat, then read
            // the three thirds back out as the streams in the reading order.
            var flat = new int[length * 3];
            for (var i = 0; i < length; i++)
            {
                var linear = lookup[text[start + i]];
                flat[i * 3] = linear / 9 + 1;
                flat[i * 3 + 1] = linear / 3 % 3 + 1;
                flat[i * 3 + 2] = linear % 3 + 1;
            }

            // The three contiguous thirds of the flat stream are the streams; map each back to its axis.
            var perLetter = new int[length, 3]; // [letter, axis]
            var k = 0;
            for (var stream = 0; stream < 3; stream++)
            {
                var axis = axes[stream];
                for (var i = 0; i < length; i++)
                {
                    perLetter[i, axis] = flat[k++];
                }
            }

            for (var i = 0; i < length; i++)
            {
                var layer = perLetter[i, 0] - 1;
                var row = perLetter[i, 1] - 1;
                var col = perLetter[i, 2] - 1;
                output.Append(alphabet[layer * 9 + row * 3 + col]);
            }
        }
    }

    private static int AxisValue((int Layer, int Row, int Col) c, int axis) => axis switch
    {
        0 => c.Layer,
        1 => c.Row,
        _ => c.Col,
    };

    /// <summary>Maps a reading order to the axis (0=layer,1=row,2=col) emitted in each of the three streams.</summary>
    private static int[] AxisOrder(TrifidReadingOrder order) => order switch
    {
        TrifidReadingOrder.SquareRowColumn => [0, 1, 2],
        TrifidReadingOrder.SquareColumnRow => [0, 2, 1],
        TrifidReadingOrder.RowSquareColumn => [1, 0, 2],
        TrifidReadingOrder.RowColumnSquare => [1, 2, 0],
        TrifidReadingOrder.ColumnSquareRow => [2, 0, 1],
        TrifidReadingOrder.ColumnRowSquare => [2, 1, 0],
        _ => [0, 1, 2],
    };

    private string BuildSequence(string? keyword, char filler)
    {
        var seen = new HashSet<char>();
        var sequence = new StringBuilder(CubeSize);

        if (!string.IsNullOrEmpty(keyword))
        {
            foreach (var raw in keyword)
            {
                var c = char.ToUpperInvariant(raw);
                if (!IsAllowed(c, filler))
                {
                    if (FoldToBaseLetter(c) is char folded && IsAllowed(folded, filler))
                    {
                        c = folded;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (seen.Add(c))
                {
                    sequence.Append(c);
                }
            }
        }

        foreach (var c in Letters)
        {
            if (seen.Add(c))
            {
                sequence.Append(c);
            }
        }

        if (seen.Add(filler))
        {
            sequence.Append(filler);
        }

        return sequence.ToString();
    }

    private static bool IsAllowed(char c, char filler) => (c is >= 'A' and <= 'Z') || c == filler;

    /// <summary>Yields the canonical (square-first-by-row) linear cell indices in the order a given
    /// fill order places the alphabet sequence into them.</summary>
    private static IEnumerable<int> FillOrderCells(TrifidFillOrder order)
    {
        switch (order)
        {
            case TrifidFillOrder.SquareFirstByRow:
                for (var layer = 0; layer < 3; layer++)
                {
                    for (var row = 0; row < 3; row++)
                    {
                        for (var col = 0; col < 3; col++)
                        {
                            yield return layer * 9 + row * 3 + col;
                        }
                    }
                }

                break;

            case TrifidFillOrder.SquareFirstByColumn:
                for (var layer = 0; layer < 3; layer++)
                {
                    for (var col = 0; col < 3; col++)
                    {
                        for (var row = 0; row < 3; row++)
                        {
                            yield return layer * 9 + row * 3 + col;
                        }
                    }
                }

                break;

            case TrifidFillOrder.RowFirst:
                for (var row = 0; row < 3; row++)
                {
                    for (var layer = 0; layer < 3; layer++)
                    {
                        for (var col = 0; col < 3; col++)
                        {
                            yield return layer * 9 + row * 3 + col;
                        }
                    }
                }

                break;

            case TrifidFillOrder.ColumnFirst:
                for (var col = 0; col < 3; col++)
                {
                    for (var layer = 0; layer < 3; layer++)
                    {
                        for (var row = 0; row < 3; row++)
                        {
                            yield return layer * 9 + row * 3 + col;
                        }
                    }
                }

                break;
        }
    }

    private static Dictionary<char, int> BuildLookup(string alphabet)
    {
        var lookup = new Dictionary<char, int>(alphabet.Length);
        for (var i = 0; i < alphabet.Length; i++)
        {
            lookup[alphabet[i]] = i;
        }

        return lookup;
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

    /// <summary>A crude EN/CS readability heuristic: rewards common letters and a sane vowel ratio so
    /// the solver can float the legible candidate to the top. Not a language model — just a tie-breaker.</summary>
    private static double ScoreReadability(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        const string common = "ETAOINSHRDLU"; // most frequent English letters
        const string vowels = "AEIOUY";

        var commonHits = 0;
        var vowelHits = 0;
        foreach (var c in text)
        {
            if (common.IndexOf(c) >= 0)
            {
                commonHits++;
            }

            if (vowels.IndexOf(c) >= 0)
            {
                vowelHits++;
            }
        }

        var commonRatio = (double)commonHits / text.Length;
        var vowelRatio = (double)vowelHits / text.Length;

        // Penalise distance from a ~40% vowel ratio typical of natural language.
        var vowelPenalty = Math.Abs(vowelRatio - 0.40);
        return commonRatio - vowelPenalty;
    }
}
