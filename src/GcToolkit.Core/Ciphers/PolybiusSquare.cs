using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>The grid variant: a 5×5 square (25 letters, one pair merged) or a 6×6 square (26 letters + digits 0–9).</summary>
public enum PolybiusGridSize
{
    /// <summary>5×5 = 25 cells. Two letters share a cell (default I/J), configurable via the merge pair.</summary>
    FiveByFive = 5,

    /// <summary>6×6 = 36 cells: the 26 letters A–Z followed by the digits 0–9. No merge needed.</summary>
    SixBySix = 6,
}

/// <summary>How the row/column coordinate labels are rendered.</summary>
public enum PolybiusLabelScheme
{
    /// <summary>Digit labels: <c>1..5</c> for the 5×5 square, <c>1..6</c> for the 6×6.</summary>
    Digits,

    /// <summary>The historical ADFGX (5×5) / ADFGVX (6×6) letter labels of the WWI ciphers.</summary>
    Adfgx,

    /// <summary>A caller-supplied label string (one character per row/column).</summary>
    Custom,
}

/// <summary>Where keyword letters are placed when their letter repeats elsewhere in the keyword.</summary>
public enum PolybiusKeywordPlacement
{
    /// <summary>Keep the first occurrence of each keyword letter (the usual keyed-square convention).</summary>
    FirstInstance,

    /// <summary>Keep the last occurrence of each keyword letter (cachesleuth's "use last instance" option).</summary>
    LastInstance,
}

/// <summary>One cell of the rendered square: its <see cref="Letter"/> and the row/column <see cref="Coordinate"/> label pair.</summary>
/// <param name="Row">Zero-based row index.</param>
/// <param name="Column">Zero-based column index.</param>
/// <param name="Letter">The letter(s) shown in the cell (two letters when a merge pair shares it).</param>
/// <param name="Coordinate">The rendered coordinate label pair (e.g. <c>"23"</c> or <c>"AD"</c>).</param>
public readonly record struct PolybiusCell(int Row, int Column, string Letter, string Coordinate);

/// <summary>Everything that defines a Polybius square and how its output reads.</summary>
/// <param name="Size">5×5 or 6×6.</param>
/// <param name="Keyword">Optional keyword/phrase that seeds a keyed alphabet (letters first, then the rest).</param>
/// <param name="LabelScheme">Digits, ADFGX/ADFGVX, or a custom label string.</param>
/// <param name="CustomLabels">Required when <paramref name="LabelScheme"/> is <see cref="PolybiusLabelScheme.Custom"/>; one char per row/column.</param>
/// <param name="MergeFrom">5×5 only: the letter folded onto <paramref name="MergeInto"/> (default <c>'J'</c> → <c>'I'</c>).</param>
/// <param name="MergeInto">5×5 only: the cell that absorbs <paramref name="MergeFrom"/> (default <c>'I'</c>).</param>
/// <param name="ReverseKeyword">Reverse the keyword before seeding the square.</param>
/// <param name="KeywordPlacement">First vs last instance of repeated keyword letters.</param>
/// <param name="Separator">String inserted between coordinate pairs on encrypt (default a single space).</param>
/// <param name="ColumnThenRow">When <see langword="true"/>, labels read column-first instead of the default row-first.</param>
public sealed record PolybiusOptions(
    PolybiusGridSize Size = PolybiusGridSize.FiveByFive,
    string? Keyword = null,
    PolybiusLabelScheme LabelScheme = PolybiusLabelScheme.Digits,
    string? CustomLabels = null,
    char MergeFrom = 'J',
    char MergeInto = 'I',
    bool ReverseKeyword = false,
    PolybiusKeywordPlacement KeywordPlacement = PolybiusKeywordPlacement.FirstInstance,
    string Separator = " ",
    bool ColumnThenRow = false);

/// <summary>The outcome of a decrypt: the recovered <see cref="Text"/> plus any coordinate pairs that were invalid.</summary>
/// <param name="Text">The decoded plaintext (only the valid pairs contribute letters).</param>
/// <param name="InvalidPairs">The raw tokens that could not be mapped to a cell (out of range, wrong length, leftover digit).</param>
/// <param name="HasLeftover">A trailing coordinate digit was left over (an odd count) — the input is malformed.</param>
public readonly record struct PolybiusDecodeResult(string Text, IReadOnlyList<string> InvalidPairs, bool HasLeftover)
{
    /// <summary><see langword="true"/> when every coordinate parsed cleanly.</summary>
    public bool IsClean => InvalidPairs.Count == 0 && !HasLeftover;
}

/// <summary>
/// A pure, deterministic Polybius-square codec — the single source of truth for the cipher behind
/// Bifid/Nihilist/ADFGX. Builds a 5×5 or 6×6 square (optionally keyed, with a configurable merge pair),
/// labels its rows/columns with digits, the historical ADFGX letters, or custom labels, and converts
/// text to coordinate pairs and back. Beyond cachesleuth.com it folds in the ADFGX/ADFGVX label rows,
/// selectable read order and separators, and a forgiving decode that flags invalid/out-of-range pairs
/// (and odd digit counts) instead of silently dropping them.
/// </summary>
public sealed class PolybiusSquare
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";

    /// <summary>The ADFGX row/column labels for the 5×5 square (chosen for their distinct Morse code).</summary>
    public const string AdfgxLabels = "ADFGX";

    /// <summary>The ADFGVX row/column labels for the 6×6 square.</summary>
    public const string AdfgvxLabels = "ADFGVX";

    private readonly char[,] _grid;
    private readonly Dictionary<char, (int Row, int Col)> _positions = new();
    private readonly string _rowLabels;
    private readonly string _colLabels;

    /// <summary>The options this square was built from.</summary>
    public PolybiusOptions Options { get; }

    /// <summary>The side length of the square (5 or 6).</summary>
    public int Side { get; }

    /// <summary>The labels read down the rows (top to bottom).</summary>
    public string RowLabels => _rowLabels;

    /// <summary>The labels read across the columns (left to right).</summary>
    public string ColumnLabels => _colLabels;

    /// <summary>
    /// Builds the square from <paramref name="options"/>. The fill order is keyword letters (deduped),
    /// then the rest of the alphabet (plus digits for 6×6); the merge pair shares a cell on the 5×5.
    /// </summary>
    /// <exception cref="ArgumentException">The label scheme/grid size mismatch, an invalid merge pair, or an empty keyword after cleaning.</exception>
    public PolybiusSquare(PolybiusOptions options)
    {
        Options = options;
        Side = (int)options.Size;

        var fill = BuildFillSequence(options);
        _grid = new char[Side, Side];
        for (var i = 0; i < fill.Length; i++)
        {
            var (row, col) = (i / Side, i % Side);
            _grid[row, col] = fill[i];
            _positions[fill[i]] = (row, col);
        }

        if (options.Size == PolybiusGridSize.FiveByFive)
        {
            // The merged letter resolves to the same cell as the letter it folds onto.
            var (from, into) = NormalizeMergePair(options);
            if (from != into && _positions.TryGetValue(into, out var pos))
            {
                _positions[from] = pos;
            }
        }

        (_rowLabels, _colLabels) = ResolveLabels(options, Side);
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> to coordinate pairs. Letters map through the square (the
    /// merge pair folds onto its partner; 6×6 also encodes digits); characters not in the square are
    /// skipped. Pairs are joined with <see cref="PolybiusOptions.Separator"/> and read row-then-column
    /// (or column-then-row when <see cref="PolybiusOptions.ColumnThenRow"/> is set).
    /// </summary>
    public string Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        var pairs = new List<string>(plaintext.Length);
        foreach (var raw in plaintext)
        {
            var c = NormalizeChar(raw);
            if (_positions.TryGetValue(c, out var pos))
            {
                pairs.Add(Pair(pos.Row, pos.Col));
            }
        }

        return string.Join(Options.Separator, pairs);
    }

    /// <summary>
    /// Decrypts coordinate pairs back to plaintext, forgivingly. Any separator/whitespace is tolerated,
    /// label characters are matched case-insensitively, and tokens that are not a valid in-range pair are
    /// collected in <see cref="PolybiusDecodeResult.InvalidPairs"/> rather than dropped. With digit labels a
    /// continuous digit stream is also accepted, and a leftover trailing digit sets
    /// <see cref="PolybiusDecodeResult.HasLeftover"/>.
    /// </summary>
    public PolybiusDecodeResult Decrypt(string? cipher)
    {
        if (string.IsNullOrWhiteSpace(cipher))
        {
            return new PolybiusDecodeResult(string.Empty, [], false);
        }

        var labelChars = (_rowLabels + _colLabels).ToUpperInvariant();
        var coordinates = ExtractCoordinateChars(cipher, labelChars, out var noise);

        var builder = new StringBuilder(coordinates.Count / 2);
        var invalid = new List<string>();

        for (var i = 0; i + 1 < coordinates.Count; i += 2)
        {
            var first = coordinates[i];
            var second = coordinates[i + 1];
            var pair = $"{first}{second}";

            var a = IndexOfLabel(first, Options.ColumnThenRow ? _colLabels : _rowLabels);
            var b = IndexOfLabel(second, Options.ColumnThenRow ? _rowLabels : _colLabels);
            var (row, col) = Options.ColumnThenRow ? (b, a) : (a, b);

            if (a < 0 || b < 0)
            {
                invalid.Add(pair);
                continue;
            }

            builder.Append(_grid[row, col]);
        }

        // Any token that wasn't a coordinate char at all is reported, and an odd count is a leftover.
        invalid.AddRange(noise);
        var hasLeftover = coordinates.Count % 2 == 1;

        return new PolybiusDecodeResult(builder.ToString(), invalid, hasLeftover);
    }

    /// <summary>The square as rows of <see cref="PolybiusCell"/> for rendering a tappable grid; merged 5×5
    /// cells show both letters (e.g. <c>"I/J"</c>).</summary>
    public IReadOnlyList<IReadOnlyList<PolybiusCell>> GetCells()
    {
        var rows = new List<IReadOnlyList<PolybiusCell>>(Side);
        for (var r = 0; r < Side; r++)
        {
            var cells = new List<PolybiusCell>(Side);
            for (var c = 0; c < Side; c++)
            {
                cells.Add(new PolybiusCell(r, c, CellLetters(r, c), Pair(r, c)));
            }

            rows.Add(cells);
        }

        return rows;
    }

    /// <summary>The coordinate label pair for the cell holding <paramref name="letter"/>, or
    /// <see cref="string.Empty"/> if it isn't in the square.</summary>
    public string CoordinateFor(char letter)
        => _positions.TryGetValue(NormalizeChar(letter), out var pos) ? Pair(pos.Row, pos.Col) : string.Empty;

    // ---- Square construction ----

    private static char[] BuildFillSequence(PolybiusOptions options)
    {
        var (from, into) = options.Size == PolybiusGridSize.FiveByFive
            ? NormalizeMergePair(options)
            : ('\0', '\0');

        var seen = new HashSet<char>();
        var sequence = new List<char>((int)options.Size * (int)options.Size);

        void TryAdd(char c)
        {
            // On the 5×5 the merged letter never occupies its own cell — it shares the partner's.
            if (from != '\0' && c == from)
            {
                return;
            }

            if (seen.Add(c))
            {
                sequence.Add(c);
            }
        }

        var keyword = CleanKeyword(options);
        foreach (var c in keyword)
        {
            TryAdd(c);
        }

        foreach (var c in Alphabet)
        {
            TryAdd(c);
        }

        if (options.Size == PolybiusGridSize.SixBySix)
        {
            foreach (var c in Digits)
            {
                TryAdd(c);
            }
        }

        var capacity = (int)options.Size * (int)options.Size;
        if (sequence.Count != capacity)
        {
            throw new ArgumentException(
                $"Fill sequence produced {sequence.Count} cells but the grid needs {capacity}.", nameof(options));
        }

        return [.. sequence];
    }

    /// <summary>Upper-cases the keyword, keeps only A–Z (and digits for 6×6), applies reverse / last-instance.</summary>
    private static string CleanKeyword(PolybiusOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Keyword))
        {
            return string.Empty;
        }

        var allowDigits = options.Size == PolybiusGridSize.SixBySix;
        var filtered = new StringBuilder();
        foreach (var raw in options.Keyword)
        {
            // Uppercase + strip diacritics here; the 5×5 merge fold is applied later in BuildFillSequence.
            var c = char.ToUpperInvariant(StripDiacritic(raw));
            if ((c is >= 'A' and <= 'Z') || (allowDigits && c is >= '0' and <= '9'))
            {
                filtered.Append(c);
            }
        }

        var letters = filtered.ToString();
        if (options.ReverseKeyword)
        {
            letters = new string([.. letters.Reverse()]);
        }

        if (options.KeywordPlacement == PolybiusKeywordPlacement.LastInstance)
        {
            // Reverse, dedup keeping first (= last in original), reverse back to restore order.
            var deduped = DedupeKeepFirst(new string([.. letters.Reverse()]));
            letters = new string([.. deduped.Reverse()]);
        }

        return letters;
    }

    private static string DedupeKeepFirst(string text)
    {
        var seen = new HashSet<char>();
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (seen.Add(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>The merge pair upper-cased and validated; only meaningful for the 5×5 square.</summary>
    private static (char From, char Into) NormalizeMergePair(PolybiusOptions options)
    {
        var from = char.ToUpperInvariant(options.MergeFrom);
        var into = char.ToUpperInvariant(options.MergeInto);
        if (from is < 'A' or > 'Z' || into is < 'A' or > 'Z')
        {
            throw new ArgumentException("The merge pair must be two letters A–Z.", nameof(options));
        }

        return (from, into);
    }

    private static (string Row, string Col) ResolveLabels(PolybiusOptions options, int side)
    {
        var labels = options.LabelScheme switch
        {
            PolybiusLabelScheme.Digits => new string([.. Enumerable.Range(1, side).Select(n => (char)('0' + n))]),
            PolybiusLabelScheme.Adfgx => side == 5 ? AdfgxLabels : AdfgvxLabels,
            PolybiusLabelScheme.Custom => options.CustomLabels ?? string.Empty,
            _ => throw new ArgumentException("Unknown label scheme.", nameof(options)),
        };

        if (labels.Length != side)
        {
            throw new ArgumentException(
                $"Label scheme produced {labels.Length} labels but the grid needs {side}.", nameof(options));
        }

        // Distinct labels are required so a coordinate pair is unambiguous.
        if (labels.Distinct().Count() != labels.Length)
        {
            throw new ArgumentException("Coordinate labels must be distinct.", nameof(options));
        }

        return (labels, labels);
    }

    // ---- Helpers ----

    /// <summary>Upper-cases, applies the 5×5 merge fold, and strips diacritics so 'É' encodes as 'E'.</summary>
    private char NormalizeChar(char c)
    {
        var upper = char.ToUpperInvariant(StripDiacritic(c));
        if (Options.Size == PolybiusGridSize.FiveByFive)
        {
            var (from, into) = NormalizeMergePair(Options);
            if (upper == from)
            {
                return into;
            }
        }

        return upper;
    }

    private static char StripDiacritic(char c)
    {
        var normalized = c.ToString().Normalize(System.Text.NormalizationForm.FormD);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                return ch;
            }
        }

        return c;
    }

    private string CellLetters(int row, int col)
    {
        var letter = _grid[row, col];
        if (Options.Size == PolybiusGridSize.FiveByFive)
        {
            var (from, into) = NormalizeMergePair(Options);
            if (from != into && letter == into)
            {
                return $"{into}/{from}";
            }
        }

        return letter.ToString();
    }

    private string Pair(int row, int col)
    {
        var (a, b) = Options.ColumnThenRow
            ? (_colLabels[col], _rowLabels[row])
            : (_rowLabels[row], _colLabels[col]);
        return $"{a}{b}";
    }

    private static int IndexOfLabel(char label, string labels)
        => labels.IndexOf(char.ToUpperInvariant(label));

    /// <summary>
    /// Pulls the coordinate label characters out of a forgiving cipher string. Recognised label chars
    /// (in any case) are collected in order; runs of other letters/symbols that look like noise are
    /// reported so the caller can flag them.
    /// </summary>
    private static List<char> ExtractCoordinateChars(string cipher, string labelChars, out List<string> noise)
    {
        var labelSet = labelChars.ToHashSet();
        var coordinates = new List<char>(cipher.Length);
        var collected = new List<string>();
        var noiseRun = new StringBuilder();

        void FlushNoise()
        {
            if (noiseRun.Length > 0)
            {
                collected.Add(noiseRun.ToString());
                noiseRun.Clear();
            }
        }

        foreach (var raw in cipher)
        {
            var c = char.ToUpperInvariant(raw);
            if (labelSet.Contains(c))
            {
                coordinates.Add(c);
            }
            else if (char.IsWhiteSpace(raw) || raw is ',' or ';' or '-' or '.' or '|' or '/' or '\\')
            {
                FlushNoise(); // common separators are not noise
            }
            else
            {
                noiseRun.Append(raw);
            }
        }

        FlushNoise();
        noise = collected;
        return coordinates;
    }
}
