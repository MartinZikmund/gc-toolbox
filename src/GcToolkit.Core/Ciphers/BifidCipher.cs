using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>How the 26-letter alphabet is reduced to the 25 cells of the Polybius square.</summary>
public enum BifidFitKind
{
    /// <summary>Fold one letter into another (the classic <c>J → I</c>); both encode as the target.</summary>
    Merge,

    /// <summary>Drop one letter entirely; it is not on the square and is ignored on input.</summary>
    Skip,
}

/// <summary>
/// The rule that trims the alphabet to 25 letters. The default is <see cref="MergeJIntoI"/>; callers
/// can merge into any target letter or skip a chosen letter instead (geocachingtoolbox.com parity).
/// </summary>
public readonly record struct BifidLetterFit
{
    /// <summary>The classic reduction: fold <c>J</c> into <c>I</c>.</summary>
    public static readonly BifidLetterFit MergeJIntoI = new(BifidFitKind.Merge, 'J', 'I');

    public BifidLetterFit(BifidFitKind kind, char from, char into = 'I')
    {
        Kind = kind;
        From = char.ToUpperInvariant(from);
        Into = char.ToUpperInvariant(into);
    }

    /// <summary>Merge or skip.</summary>
    public BifidFitKind Kind { get; }

    /// <summary>The letter removed from the square (merged away or skipped).</summary>
    public char From { get; }

    /// <summary>For <see cref="BifidFitKind.Merge"/>, the surviving letter <see cref="From"/> folds into.</summary>
    public char Into { get; }

    /// <summary>Skip <paramref name="letter"/> entirely (no merge target).</summary>
    public static BifidLetterFit Skip(char letter) => new(BifidFitKind.Skip, letter);
}

/// <summary>
/// An immutable 5×5 Polybius key square — the 25 letters that participate in a Bifid cipher, plus the
/// fold map that routes off-square letters (e.g. <c>J → I</c>) onto a cell. Build one from a keyword
/// (<see cref="FromKeyword"/>) or an explicit 25-letter string (<see cref="FromText"/>).
/// </summary>
public sealed class BifidSquare
{
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly char[] _cells;          // row-major, length 25
    private readonly int[] _position;        // 'A'..'Z' -> packed (row*5+col), or -1 if off-square
    private readonly char? _foldTarget;      // the merge target (J's 'I'), if any

    private BifidSquare(char[] cells, BifidLetterFit fit)
    {
        _cells = cells;
        Letters = new string(cells);

        _position = new int[26];
        Array.Fill(_position, -1);
        for (var i = 0; i < cells.Length; i++)
        {
            _position[cells[i] - 'A'] = i;
        }

        _foldTarget = fit.Kind == BifidFitKind.Merge && fit.From != fit.Into ? fit.Into : null;
        if (_foldTarget is char target)
        {
            // The merged-away letter resolves to its target's cell.
            _position[fit.From - 'A'] = _position[target - 'A'];
        }
    }

    /// <summary>The 25 square letters in reading order (row-major).</summary>
    public string Letters { get; }

    /// <summary>
    /// Builds the standard keyword square: the keyword's letters first (folded to upper case, de-duped,
    /// off-square letters routed by <paramref name="fit"/>), then the remaining alphabet in order.
    /// </summary>
    public static BifidSquare FromKeyword(string? keyword, BifidLetterFit fit)
    {
        var omitted = OmittedLetter(fit);
        StringBuilder builder = new(25);
        HashSet<char> seen = [];

        void Add(char letter)
        {
            var c = Resolve(letter, fit);
            if (c != '\0' && c != omitted && seen.Add(c))
            {
                builder.Append(c);
            }
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            foreach (var raw in keyword)
            {
                if (char.IsLetter(raw))
                {
                    Add(raw);
                }
            }
        }

        foreach (var letter in FullAlphabet)
        {
            Add(letter);
        }

        return new BifidSquare(builder.ToString().ToCharArray(), fit);
    }

    /// <summary>
    /// Builds a square from an explicit 25-letter string (manual or random square). Throws when the
    /// text is not exactly 25 distinct A–Z letters. Off-square input letters are mapped per <paramref name="fit"/>.
    /// </summary>
    public static BifidSquare FromText(string? text, BifidLetterFit? fit = null)
    {
        var effective = fit ?? BifidLetterFit.MergeJIntoI;
        if (text is null)
        {
            throw new ArgumentException("A square needs exactly 25 letters.", nameof(text));
        }

        var cells = text.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray();
        if (cells.Length != 25)
        {
            throw new ArgumentException($"A square needs exactly 25 letters, got {cells.Length}.", nameof(text));
        }

        if (cells.Distinct().Count() != 25)
        {
            throw new ArgumentException("A square cannot repeat a letter.", nameof(text));
        }

        return new BifidSquare(cells, effective);
    }

    /// <summary>The (row, col) of <paramref name="letter"/>, applying the fold map; <c>(-1, -1)</c> if off-square.</summary>
    public (int Row, int Col) Find(char letter)
    {
        var upper = char.ToUpperInvariant(letter);
        if (upper is < 'A' or > 'Z')
        {
            return (-1, -1);
        }

        var packed = _position[upper - 'A'];
        return packed < 0 ? (-1, -1) : (packed / 5, packed % 5);
    }

    /// <summary>The letter at the given grid cell.</summary>
    public char LetterAt(int row, int col) => _cells[(row * 5) + col];

    /// <summary>The letter at a flat row-major index (0–24).</summary>
    public char LetterAt(int index) => _cells[index];

    private static char OmittedLetter(BifidLetterFit fit) => fit.Kind switch
    {
        BifidFitKind.Skip => fit.From,
        BifidFitKind.Merge when fit.From != fit.Into => fit.From,
        _ => '\0',
    };

    /// <summary>Folds an input letter onto a square letter; returns <c>'\0'</c> when it has no cell.</summary>
    private static char Resolve(char letter, BifidLetterFit fit)
    {
        var c = char.ToUpperInvariant(letter);
        if (c is < 'A' or > 'Z')
        {
            return '\0';
        }

        if (fit.Kind == BifidFitKind.Merge && c == fit.From)
        {
            return fit.Into;
        }

        if (fit.Kind == BifidFitKind.Skip && c == fit.From)
        {
            return '\0';
        }

        return c;
    }
}

/// <summary>The outcome of a Bifid transform: the resulting <see cref="Text"/> and whether any input
/// characters were ignored (not on the square — spaces, digits, punctuation, or a skipped letter).</summary>
public readonly record struct BifidResult(string Text, bool HasIgnored);

/// <summary>
/// A pure, stateless Bifid (Delastelle) cipher — the single source of truth for the transform. Maps
/// each letter to its <see cref="BifidSquare"/> coordinates, concatenates all rows then all columns
/// (optionally within fixed-size periodic blocks), re-pairs the sequence, and maps each pair back
/// through the square. Beyond geocachingtoolbox.com's single-block variant it supports an adjustable
/// period and reports ignored characters for clear validation states.
/// </summary>
public sealed class BifidCipher
{
    /// <summary>
    /// Encrypts <paramref name="text"/> with <paramref name="square"/>. Non-square characters are
    /// stripped; case is folded. <paramref name="period"/> ≤ 0 fractionates the whole message at once;
    /// a positive period groups it into blocks of that length first.
    /// </summary>
    public BifidResult Encrypt(string? text, BifidSquare square, int period = 0)
        => Transform(text, square, period, decrypt: false);

    /// <summary>Decrypts <paramref name="text"/> — the inverse of <see cref="Encrypt"/> with the same square and period.</summary>
    public BifidResult Decrypt(string? text, BifidSquare square, int period = 0)
        => Transform(text, square, period, decrypt: true);

    private static BifidResult Transform(string? text, BifidSquare square, int period, bool decrypt)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new BifidResult(string.Empty, false);
        }

        List<char> letters = new(text.Length);
        var hasIgnored = false;
        foreach (var c in text)
        {
            var (row, _) = square.Find(c);
            if (row >= 0)
            {
                letters.Add(char.ToUpperInvariant(c));
            }
            else
            {
                // Anything without a cell (space, digit, punctuation, or a skipped letter) is dropped.
                hasIgnored = true;
            }
        }

        if (letters.Count == 0)
        {
            return new BifidResult(string.Empty, hasIgnored);
        }

        StringBuilder output = new(letters.Count);
        var blockSize = period > 0 ? period : letters.Count;
        for (var start = 0; start < letters.Count; start += blockSize)
        {
            var length = Math.Min(blockSize, letters.Count - start);
            TransformBlock(letters, start, length, square, decrypt, output);
        }

        return new BifidResult(output.ToString(), hasIgnored);
    }

    private static void TransformBlock(List<char> letters, int start, int length, BifidSquare square, bool decrypt, StringBuilder output)
    {
        Span<int> coords = stackalloc int[length * 2];

        if (!decrypt)
        {
            // Encrypt: rows first, then columns; re-pair consecutive numbers.
            for (var i = 0; i < length; i++)
            {
                var (row, col) = square.Find(letters[start + i]);
                coords[i] = row;
                coords[length + i] = col;
            }

            for (var i = 0; i < length; i++)
            {
                output.Append(square.LetterAt(coords[(i * 2)], coords[(i * 2) + 1]));
            }
        }
        else
        {
            // Decrypt: read each letter's (row,col) into one stream, then split into the row-half and col-half.
            for (var i = 0; i < length; i++)
            {
                var (row, col) = square.Find(letters[start + i]);
                coords[i * 2] = row;
                coords[(i * 2) + 1] = col;
            }

            for (var i = 0; i < length; i++)
            {
                output.Append(square.LetterAt(coords[i], coords[length + i]));
            }
        }
    }
}
