using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>How the 26-letter alphabet is squeezed into the 25-cell Playfair square.</summary>
public enum PlayfairFit
{
    /// <summary>Fold one letter onto another (classic <c>J → I</c>), leaving 25 distinct letters.</summary>
    Merge,

    /// <summary>Drop a chosen letter entirely (e.g. <c>Q</c>), leaving 25 distinct letters.</summary>
    Skip,
}

/// <summary>The order key/alphabet letters fill the 5×5 square.</summary>
public enum PlayfairFillOrder
{
    /// <summary>Left-to-right, top-to-bottom — the conventional layout.</summary>
    RowMajor,

    /// <summary>Top-to-bottom, left-to-right (each column filled before the next).</summary>
    ColumnMajor,
}

/// <summary>
/// All configuration for a Playfair transform: the key source, the 25-letter fit rule, the
/// double-letter/padding filler, and the fill order. Sensible defaults match the classic cipher
/// (keyword square, <c>J → I</c> merge, <c>X</c> filler, row-major, split repeated pairs).
/// </summary>
public sealed record PlayfairOptions
{
    /// <summary>The keyword that seeds the square (ignored when <see cref="ManualSquare"/> is set).</summary>
    public string Keyword { get; init; } = string.Empty;

    /// <summary>An explicit 25-letter square (manual/random). Overrides <see cref="Keyword"/> when non-empty.</summary>
    public string? ManualSquare { get; init; }

    /// <summary>How to reduce 26 letters to 25.</summary>
    public PlayfairFit Fit { get; init; } = PlayfairFit.Merge;

    /// <summary>The letter folded away when <see cref="Fit"/> is <see cref="PlayfairFit.Merge"/> (default <c>J</c>).</summary>
    public char MergeFrom { get; init; } = 'J';

    /// <summary>The letter that <see cref="MergeFrom"/> becomes (default <c>I</c>).</summary>
    public char MergeTo { get; init; } = 'I';

    /// <summary>The letter dropped when <see cref="Fit"/> is <see cref="PlayfairFit.Skip"/> (default <c>Q</c>).</summary>
    public char SkipLetter { get; init; } = 'Q';

    /// <summary>Filler inserted between repeated letters and used to pad an odd final digraph (default <c>X</c>).</summary>
    public char Filler { get; init; } = 'X';

    /// <summary>Order the square is filled in.</summary>
    public PlayfairFillOrder FillOrder { get; init; } = PlayfairFillOrder.RowMajor;

    /// <summary>
    /// When <see langword="true"/> (default) a repeated-letter pair is split by the <see cref="Filler"/>.
    /// When <see langword="false"/> the pair is encoded directly by the down-and-right rule.
    /// </summary>
    public bool SplitDoubles { get; init; } = true;
}

/// <summary>
/// The outcome of a transform: the resulting <see cref="Text"/>, the <see cref="NormalizedInput"/> that
/// produced it (uppercased, stripped, filler-inserted), how many characters were dropped during
/// normalization, and an optional <see cref="ErrorKey"/> for validation failures.
/// </summary>
public readonly record struct PlayfairResult(string Text, string NormalizedInput, int StrippedCount, string? ErrorKey)
{
    /// <summary><see langword="true"/> when a validation error occurred (and <see cref="Text"/> is empty).</summary>
    public bool HasError => ErrorKey is not null;
}

/// <summary>
/// A pure, stateless Playfair (Wheatstone, 1854) digraph cipher — the single source of truth for the
/// transform. Builds the 5×5 key square from a keyword (or an explicit manual square), normalizes text to
/// the 25-letter alphabet (configurable merge/skip), splits repeated pairs and pads odd input with a
/// selectable filler, and applies the row/column/rectangle rules (inverse for decrypt). Beyond
/// geocachingtoolbox.com parity it exposes the built square for display, supports any merge pair / skip
/// letter / filler / fill order, an optional no-split mode, and reports exactly what normalization stripped.
/// </summary>
public sealed class PlayfairCipher
{
    private const int Size = 5;
    private const int CellCount = Size * Size;

    /// <summary>
    /// Builds the 5×5 key square (five rows of five letters). With a <see cref="PlayfairOptions.ManualSquare"/>
    /// the cells are used verbatim; otherwise the keyword's distinct letters lead, followed by the rest of
    /// the fitted alphabet, filled row- or column-major.
    /// </summary>
    /// <exception cref="ArgumentException">The manual square is not exactly 25 distinct fitted letters.</exception>
    public IReadOnlyList<string> BuildSquare(PlayfairOptions options)
    {
        var cells = BuildCells(options);
        var rows = new string[Size];
        for (var r = 0; r < Size; r++)
        {
            rows[r] = new string(cells, r * Size, Size);
        }

        return rows;
    }

    /// <summary>Encrypts <paramref name="text"/> with the given <paramref name="options"/>.</summary>
    public PlayfairResult Encrypt(string? text, PlayfairOptions options) => Transform(text, options, encrypt: true);

    /// <summary>Decrypts <paramref name="text"/> with the given <paramref name="options"/>.</summary>
    public PlayfairResult Decrypt(string? text, PlayfairOptions options) => Transform(text, options, encrypt: false);

    private PlayfairResult Transform(string? text, PlayfairOptions options, bool encrypt)
    {
        var square = BuildCells(options);
        var (positions, alphabet) = IndexSquare(square);

        var (cleaned, stripped) = Clean(text, options, alphabet);
        if (cleaned.Length == 0)
        {
            return new PlayfairResult(string.Empty, string.Empty, stripped, null);
        }

        if (encrypt)
        {
            return EncryptCore(cleaned, stripped, options, square, positions, alphabet);
        }

        // Ciphertext is already in fixed digraphs; an odd length means it cannot be decrypted.
        if (cleaned.Length % 2 != 0)
        {
            return new PlayfairResult(string.Empty, cleaned, stripped, "PlayfairErrorOddCiphertext");
        }

        var plain = MapDigraphs(cleaned, square, positions, step: -1);
        return new PlayfairResult(plain, cleaned, stripped, null);
    }

    private static PlayfairResult EncryptCore(
        string cleaned,
        int stripped,
        PlayfairOptions options,
        char[] square,
        int[] positions,
        HashSet<char> alphabet)
    {
        var prepared = PrepareDigraphs(cleaned, options, alphabet);
        var cipher = MapDigraphs(prepared, square, positions, step: +1);
        return new PlayfairResult(cipher, prepared, stripped, null);
    }

    /// <summary>
    /// Splits <paramref name="cleaned"/> into digraphs, inserting the filler between repeated letters
    /// (when <see cref="PlayfairOptions.SplitDoubles"/>) and padding an odd tail. Returns the flat,
    /// even-length, filler-inserted plaintext — the exact string a correct decrypt recovers.
    /// </summary>
    private static string PrepareDigraphs(string cleaned, PlayfairOptions options, HashSet<char> alphabet)
    {
        var builder = new StringBuilder(cleaned.Length + cleaned.Length / 2 + 1);
        var i = 0;
        while (i < cleaned.Length)
        {
            var a = cleaned[i];
            if (i + 1 == cleaned.Length)
            {
                // Odd tail: pad with the filler (or an alternate if the tail letter IS the filler).
                builder.Append(a).Append(FillerFor(a, options, alphabet));
                break;
            }

            var b = cleaned[i + 1];
            if (options.SplitDoubles && a == b)
            {
                builder.Append(a).Append(FillerFor(a, options, alphabet));
                i++; // re-examine b at the start of the next pair
            }
            else
            {
                builder.Append(a).Append(b);
                i += 2;
            }
        }

        return builder.ToString();
    }

    /// <summary>The filler to separate/pad <paramref name="letter"/>: the configured filler, or the first
    /// other alphabet letter when the filler equals the letter (so the pair stops being a double).</summary>
    private static char FillerFor(char letter, PlayfairOptions options, HashSet<char> alphabet)
    {
        var filler = Fold(char.ToUpperInvariant(options.Filler), options);
        if (filler != letter && alphabet.Contains(filler))
        {
            return filler;
        }

        // Filler clashes with the letter (or is not in the square): pick any other in-square letter.
        foreach (var c in alphabet)
        {
            if (c != letter)
            {
                return c;
            }
        }

        return letter; // unreachable for a 25-letter square
    }

    /// <summary>Maps each digraph by the row (right/left), column (down/up) or rectangle rule. <paramref name="step"/>
    /// is +1 to encrypt, −1 to decrypt.</summary>
    private static string MapDigraphs(string text, char[] square, int[] positions, int step)
    {
        var output = new char[text.Length];
        for (var i = 0; i < text.Length; i += 2)
        {
            var pa = positions[text[i] - 'A'];
            var pb = positions[text[i + 1] - 'A'];
            var (ra, ca) = (pa / Size, pa % Size);
            var (rb, cb) = (pb / Size, pb % Size);

            if (ra == rb)
            {
                output[i] = square[ra * Size + Wrap(ca + step)];
                output[i + 1] = square[rb * Size + Wrap(cb + step)];
            }
            else if (ca == cb)
            {
                output[i] = square[Wrap(ra + step) * Size + ca];
                output[i + 1] = square[Wrap(rb + step) * Size + cb];
            }
            else
            {
                output[i] = square[ra * Size + cb];
                output[i + 1] = square[rb * Size + ca];
            }
        }

        return new string(output);
    }

    private static int Wrap(int index) => ((index % Size) + Size) % Size;

    /// <summary>Uppercases, applies the fit (merge/skip) rule and strips anything not in the square's
    /// alphabet. Returns the cleaned text and the count of characters removed.</summary>
    private static (string Cleaned, int Stripped) Clean(string? text, PlayfairOptions options, HashSet<char> alphabet)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (string.Empty, 0);
        }

        var builder = new StringBuilder(text.Length);
        var stripped = 0;
        foreach (var raw in text)
        {
            if (!char.IsLetter(raw))
            {
                if (!char.IsWhiteSpace(raw))
                {
                    stripped++;
                }

                continue;
            }

            var folded = Fold(char.ToUpperInvariant(raw), options);
            if (folded >= 'A' && folded <= 'Z' && alphabet.Contains(folded))
            {
                builder.Append(folded);
            }
            else
            {
                stripped++;
            }
        }

        return (builder.ToString(), stripped);
    }

    /// <summary>Applies the active fit rule to a single upper-case letter (merge → target, skip → drop).</summary>
    private static char Fold(char upper, PlayfairOptions options)
    {
        if (options.ManualSquare is { Length: > 0 })
        {
            return upper; // a manual square defines its own 25 letters; no extra folding.
        }

        if (options.Fit == PlayfairFit.Merge && upper == char.ToUpperInvariant(options.MergeFrom))
        {
            return char.ToUpperInvariant(options.MergeTo);
        }

        return upper; // skip-fit simply leaves the letter; Clean drops it if it is not in the alphabet.
    }

    /// <summary>Maps each square cell to its flat index, and returns the set of letters it contains.</summary>
    private static (int[] Positions, HashSet<char> Alphabet) IndexSquare(char[] square)
    {
        var positions = new int[26];
        Array.Fill(positions, -1);
        var alphabet = new HashSet<char>(CellCount);
        for (var i = 0; i < square.Length; i++)
        {
            positions[square[i] - 'A'] = i;
            alphabet.Add(square[i]);
        }

        return (positions, alphabet);
    }

    /// <summary>Produces the flat 25-cell square as a <c>char[]</c>, honouring manual square, fit, keyword and fill order.</summary>
    private static char[] BuildCells(PlayfairOptions options)
    {
        if (options.ManualSquare is { Length: > 0 } manual)
        {
            return ManualCells(manual);
        }

        var skip = options.Fit == PlayfairFit.Skip
            ? char.ToUpperInvariant(options.SkipLetter)
            : char.ToUpperInvariant(options.MergeFrom);

        // Distinct, in-alphabet letters from the keyword first, then the remaining fitted alphabet.
        var seen = new bool[26];
        var ordered = new List<char>(CellCount);

        void Add(char upper)
        {
            if (upper < 'A' || upper > 'Z' || upper == skip)
            {
                return;
            }

            var idx = upper - 'A';
            if (!seen[idx])
            {
                seen[idx] = true;
                ordered.Add(upper);
            }
        }

        foreach (var raw in options.Keyword ?? string.Empty)
        {
            if (char.IsLetter(raw))
            {
                Add(Fold(char.ToUpperInvariant(raw), options));
            }
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            Add(c);
        }

        var rowMajor = ordered.ToArray();
        return options.FillOrder == PlayfairFillOrder.ColumnMajor ? Transpose(rowMajor) : rowMajor;
    }

    private static char[] ManualCells(string manual)
    {
        var cells = manual.ToUpperInvariant().ToCharArray();
        if (cells.Length != CellCount || !cells.All(c => c is >= 'A' and <= 'Z') || cells.Distinct().Count() != CellCount)
        {
            throw new ArgumentException("A manual square must contain exactly 25 distinct letters (A–Z).", nameof(manual));
        }

        return cells;
    }

    /// <summary>Re-reads a row-major layout as column-major (each column filled before the next).</summary>
    private static char[] Transpose(char[] rowMajor)
    {
        var result = new char[CellCount];
        for (var i = 0; i < CellCount; i++)
        {
            var r = i / Size;
            var c = i % Size;
            result[c * Size + r] = rowMajor[i];
        }

        return result;
    }
}
