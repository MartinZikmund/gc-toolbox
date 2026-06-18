namespace GcToolkit.Core.Ciphers;

/// <summary>
/// How the 26-letter Latin alphabet is reduced to the 25 cells a 5×5 square holds: either two
/// letters are merged (classically J→I) or a single chosen letter is omitted entirely. This choice
/// is shared by all four squares so the plain and keyword grids stay aligned.
/// </summary>
public readonly struct FourSquareAlphabet
{
    private FourSquareAlphabet(string letters, char? merged, char mergedInto, char? skipped)
    {
        Letters = letters;
        Merged = merged;
        MergedInto = mergedInto;
        Skipped = skipped;
    }

    /// <summary>The 25 letters that fill a square, in alphabetical order.</summary>
    public string Letters { get; }

    /// <summary>The letter folded into <see cref="MergedInto"/> on input (e.g. <c>J</c>), or <see langword="null"/> in skip mode.</summary>
    public char? Merged { get; }

    /// <summary>The target of <see cref="Merged"/> (e.g. <c>I</c>).</summary>
    public char MergedInto { get; }

    /// <summary>The letter omitted from the alphabet in skip mode, or <see langword="null"/> in merge mode.</summary>
    public char? Skipped { get; }

    /// <summary>The classic "J is replaced by I" alphabet (A–Z without J).</summary>
    public static FourSquareAlphabet MergeJIntoI { get; } = Merge('J', 'I');

    /// <summary>Folds <paramref name="from"/> into <paramref name="into"/>, removing <paramref name="from"/> from the square.</summary>
    public static FourSquareAlphabet Merge(char from, char into)
    {
        from = char.ToUpperInvariant(from);
        into = char.ToUpperInvariant(into);
        var letters = BuildAlphabet(from);
        return new FourSquareAlphabet(letters, from, into, skipped: null);
    }

    /// <summary>Omits <paramref name="letter"/> from the square (no substitution on input).</summary>
    public static FourSquareAlphabet Skip(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        var letters = BuildAlphabet(letter);
        return new FourSquareAlphabet(letters, merged: null, mergedInto: '\0', skipped: letter);
    }

    /// <summary>Maps an upper-case letter to the form used inside the squares, or <see langword="null"/> if it is not representable.</summary>
    internal char? Normalize(char upper)
    {
        if (Merged is char from && upper == from)
        {
            return MergedInto;
        }

        if (Skipped is char skip && upper == skip)
        {
            return null;
        }

        return upper;
    }

    private static string BuildAlphabet(char exclude)
    {
        Span<char> buffer = stackalloc char[25];
        var count = 0;
        for (var c = 'A'; c <= 'Z'; c++)
        {
            if (c != exclude)
            {
                buffer[count++] = c;
            }
        }

        return new string(buffer);
    }
}

/// <summary>The outcome of a four-square transform: the resulting <see cref="Text"/> and whether a filler was appended.</summary>
public readonly record struct FourSquareResult(string Text, bool PaddingApplied);

/// <summary>
/// A pure, stateless four-square (Delastelle) digraph cipher — the single source of truth for the
/// transform. Two plain-alphabet squares (top-left, bottom-right) and two keyword squares
/// (top-right, bottom-left) drive a polygraphic substitution over pairs of letters. The 26-letter
/// alphabet is reduced to 25 via a shared <see cref="FourSquareAlphabet"/> (merge or skip). Non-letter
/// characters are stripped, input is upper-cased, and odd-length plaintext is padded with a filler.
/// </summary>
public sealed class FourSquareCipher
{
    private const int Size = 5;

    private readonly FourSquareAlphabet _alphabet;
    private readonly char _filler;
    private readonly char[] _plain;       // grids 1 (top-left) & 4 (bottom-right)
    private readonly char[] _topRight;    // grid 2
    private readonly char[] _bottomLeft;  // grid 3

    /// <summary>
    /// Builds the four squares from two keywords. The plain squares hold the alphabet in order; the
    /// keyword squares are each the deduplicated keyword followed by the rest of the alphabet.
    /// </summary>
    public FourSquareCipher(string topRightKey, string bottomLeftKey, FourSquareAlphabet alphabet, char filler = 'X')
    {
        _alphabet = alphabet;
        _filler = NormalizeFiller(filler, alphabet);
        _plain = alphabet.Letters.ToCharArray();
        _topRight = BuildSquare(topRightKey, alphabet);
        _bottomLeft = BuildSquare(bottomLeftKey, alphabet);
    }

    private FourSquareCipher(char[] plain, char[] topRight, char[] bottomLeft, FourSquareAlphabet alphabet, char filler)
    {
        _alphabet = alphabet;
        _filler = NormalizeFiller(filler, alphabet);
        _plain = plain;
        _topRight = topRight;
        _bottomLeft = bottomLeft;
    }

    /// <summary>The top-left / bottom-right plain square (25 letters, row-major).</summary>
    public IReadOnlyList<char> PlainSquare => _plain;

    /// <summary>The top-right keyword square (25 letters, row-major).</summary>
    public IReadOnlyList<char> TopRightSquare => _topRight;

    /// <summary>The bottom-left keyword square (25 letters, row-major).</summary>
    public IReadOnlyList<char> BottomLeftSquare => _bottomLeft;

    /// <summary>The active 25-letter alphabet configuration.</summary>
    public FourSquareAlphabet Alphabet => _alphabet;

    /// <summary>
    /// Builds a 25-letter square from <paramref name="keyword"/>: its distinct, alphabet-normalized
    /// letters in order, then the remaining alphabet. An empty keyword yields the plain alphabet.
    /// </summary>
    public static char[] BuildSquare(string? keyword, FourSquareAlphabet alphabet)
    {
        var result = new char[25];
        Span<bool> used = stackalloc bool[26]; // index by letter - 'A'
        var count = 0;

        if (!string.IsNullOrEmpty(keyword))
        {
            foreach (var raw in keyword)
            {
                if (!char.IsLetter(raw))
                {
                    continue;
                }

                var normalized = alphabet.Normalize(char.ToUpperInvariant(raw));
                if (normalized is not char c)
                {
                    continue;
                }

                var index = c - 'A';
                if (!used[index])
                {
                    used[index] = true;
                    result[count++] = c;
                }
            }
        }

        // Fill the rest of the square with the alphabet letters not yet placed.
        foreach (var c in alphabet.Letters)
        {
            var index = c - 'A';
            if (!used[index])
            {
                used[index] = true;
                result[count++] = c;
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a cipher from explicit squares (manual / random mode). Each square must be a complete
    /// 25-letter permutation of the active alphabet.
    /// </summary>
    public static FourSquareCipher FromSquares(char[] plain, char[] topRight, char[] bottomLeft, FourSquareAlphabet alphabet, char filler = 'X')
    {
        Validate(plain, alphabet, nameof(plain));
        Validate(topRight, alphabet, nameof(topRight));
        Validate(bottomLeft, alphabet, nameof(bottomLeft));
        return new FourSquareCipher((char[])plain.Clone(), (char[])topRight.Clone(), (char[])bottomLeft.Clone(), alphabet, filler);
    }

    /// <summary>Encrypts <paramref name="text"/>: plain digraph in the two plain squares → cipher digraph in the keyword squares.</summary>
    public FourSquareResult Encrypt(string? text) => Transform(text, encrypt: true);

    /// <summary>Decrypts <paramref name="text"/>: cipher digraph in the keyword squares → plain digraph in the plain squares.</summary>
    public FourSquareResult Decrypt(string? text) => Transform(text, encrypt: false);

    private FourSquareResult Transform(string? text, bool encrypt)
    {
        var letters = Sanitize(text);
        if (letters.Length == 0)
        {
            return new FourSquareResult(string.Empty, PaddingApplied: false);
        }

        var padded = letters.Length % 2 != 0;
        if (padded)
        {
            letters += _filler;
        }

        var output = new char[letters.Length];
        var (firstSource, secondSource, firstTarget, secondTarget) = encrypt
            ? (_plain, _plain, _topRight, _bottomLeft)
            : (_topRight, _bottomLeft, _plain, _plain);

        for (var i = 0; i < letters.Length; i += 2)
        {
            var (r1, c1) = IndexOf(firstSource, letters[i]);
            var (r2, c2) = IndexOf(secondSource, letters[i + 1]);

            // Cipher pair reads at the cross row/column intersections in the target squares.
            output[i] = firstTarget[r1 * Size + c2];
            output[i + 1] = secondTarget[r2 * Size + c1];
        }

        return new FourSquareResult(new string(output), padded);
    }

    private string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var buffer = new char[text.Length];
        var count = 0;
        foreach (var raw in text)
        {
            if (!char.IsLetter(raw))
            {
                continue;
            }

            if (_alphabet.Normalize(char.ToUpperInvariant(raw)) is char c)
            {
                buffer[count++] = c;
            }
        }

        return new string(buffer, 0, count);
    }

    private static (int Row, int Col) IndexOf(char[] square, char letter)
    {
        var index = Array.IndexOf(square, letter);
        return (index / Size, index % Size);
    }

    private static char NormalizeFiller(char filler, FourSquareAlphabet alphabet)
    {
        var upper = char.ToUpperInvariant(filler);
        // The filler must live in the active alphabet; fall back to X (or A if X is excluded).
        if (alphabet.Letters.Contains(upper))
        {
            return upper;
        }

        return alphabet.Letters.Contains('X') ? 'X' : alphabet.Letters[0];
    }

    private static void Validate(char[] square, FourSquareAlphabet alphabet, string name)
    {
        if (square.Length != 25)
        {
            throw new ArgumentException($"Square must contain exactly 25 letters but had {square.Length}.", name);
        }

        Span<bool> seen = stackalloc bool[26];
        foreach (var raw in square)
        {
            var c = char.ToUpperInvariant(raw);
            if (alphabet.Normalize(c) is not char normalized || normalized != c || !alphabet.Letters.Contains(c))
            {
                throw new ArgumentException($"Square contains '{raw}', which is not in the active alphabet.", name);
            }

            var index = c - 'A';
            if (seen[index])
            {
                throw new ArgumentException($"Square contains duplicate letter '{c}'.", name);
            }

            seen[index] = true;
        }
    }
}
