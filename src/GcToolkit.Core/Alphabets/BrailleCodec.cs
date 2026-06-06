using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// Bidirectional Grade-1 (uncontracted) English Braille translator. Text is rendered as Unicode
/// braille-pattern characters (<c>U+2800</c>–<c>U+283F</c>) — copy-pasteable, unlike the image-based
/// converter on geocachingtoolbox.com — and the same characters decode straight back to text.
/// </summary>
/// <remarks>
/// Conventions (literary English Braille, matching geocachingtoolbox where it overlaps):
/// <list type="bullet">
///   <item><description>Letters <c>a–z</c> use the standard dot cells; input is case-folded and an
///   upper-case letter is preceded by the <b>capital sign</b> (dot 6, <c>U+2820</c>).</description></item>
///   <item><description>A run of digits is introduced by the <b>number sign</b> (dots 3-4-5-6,
///   <c>U+283C</c>); within the run <c>a–j</c> stand for <c>1–9,0</c>. The run ends at any non-digit
///   (a space restarts it), so <c>"1 2"</c> emits two number signs.</description></item>
///   <item><description>Space is the blank cell (<c>U+2800</c>). Punctuation uses the literary
///   lower-cell signs; the question mark shares its cell with the opening quote and a single sign
///   serves both parentheses (the site's stated conventions). Those collisions decode to one canonical
///   character, so a round-trip of letters, digits, spaces and the common punctuation is lossless.</description></item>
///   <item><description>Any character with no braille mapping passes through unchanged on encode, and
///   any non-braille character passes through unchanged on decode.</description></item>
/// </list>
/// </remarks>
public sealed class BrailleCodec
{
    /// <summary>The blank braille cell (no raised dots) — used for spaces and the dot-display of a space.</summary>
    public const char Blank = '⠀';

    /// <summary>Capital indicator: dot 6 (<c>U+2820</c>), prefixes a single upper-case letter.</summary>
    public const char CapitalSign = '⠠';

    /// <summary>Number indicator: dots 3-4-5-6 (<c>U+283C</c>), introduces a run of digits.</summary>
    public const char NumberSign = '⠼';

    /// <summary>Dot-display marker for the blank cell (a space), which has no raised dots.</summary>
    public const string BlankDots = "⠀";

    // Dot-number lists are the source of truth; the cell is derived by OR-ing each dot's bit into U+2800.
    private static readonly (char Character, int[] Dots)[] LetterDots =
    [
        ('a', [1]), ('b', [1, 2]), ('c', [1, 4]), ('d', [1, 4, 5]), ('e', [1, 5]),
        ('f', [1, 2, 4]), ('g', [1, 2, 4, 5]), ('h', [1, 2, 5]), ('i', [2, 4]), ('j', [2, 4, 5]),
        ('k', [1, 3]), ('l', [1, 2, 3]), ('m', [1, 3, 4]), ('n', [1, 3, 4, 5]), ('o', [1, 3, 5]),
        ('p', [1, 2, 3, 4]), ('q', [1, 2, 3, 4, 5]), ('r', [1, 2, 3, 5]), ('s', [2, 3, 4]), ('t', [2, 3, 4, 5]),
        ('u', [1, 3, 6]), ('v', [1, 2, 3, 6]), ('w', [2, 4, 5, 6]), ('x', [1, 3, 4, 6]),
        ('y', [1, 3, 4, 5, 6]), ('z', [1, 3, 5, 6]),
    ];

    // Literary lower-cell punctuation. The first entry for a cell owns it on decode (so '?' wins its
    // shared cell, '(' owns the single bracket sign, and the closing quote owns 3-5-6).
    private static readonly (char Character, int[] Dots)[] PunctuationDots =
    [
        ('.', [2, 5, 6]), (',', [2]), (';', [2, 3]), (':', [2, 5]),
        ('?', [2, 3, 6]), ('!', [2, 3, 5]), ('\'', [3]), ('-', [3, 6]),
        ('(', [2, 3, 5, 6]), (')', [2, 3, 5, 6]),
        ('"', [3, 5, 6]),
    ];

    private static readonly IReadOnlyDictionary<char, char> LetterToCell;
    private static readonly IReadOnlyDictionary<char, char> CellToLetter;
    private static readonly IReadOnlyDictionary<char, char> PunctuationToCell;
    private static readonly IReadOnlyDictionary<char, char> CellToPunctuation;

    // Digits 1-9,0 share the letter cells of a-j; index 0 holds '0' (cell of 'j').
    private static readonly char[] DigitCells = new char[10];
    private static readonly IReadOnlyDictionary<char, char> CellToDigit;

    static BrailleCodec()
    {
        var letterToCell = new Dictionary<char, char>();
        var cellToLetter = new Dictionary<char, char>();
        foreach (var (character, dots) in LetterDots)
        {
            var cell = CellFromDots(dots);
            letterToCell[character] = cell;
            cellToLetter[cell] = character;
        }

        var punctuationToCell = new Dictionary<char, char>();
        var cellToPunctuation = new Dictionary<char, char>();
        foreach (var (character, dots) in PunctuationDots)
        {
            var cell = CellFromDots(dots);
            punctuationToCell[character] = cell;
            cellToPunctuation.TryAdd(cell, character); // first listed wins the shared cell on decode
        }

        // 'a'..'i' -> 1..9, 'j' -> 0.
        var cellToDigit = new Dictionary<char, char>();
        for (var d = 1; d <= 9; d++)
        {
            var letter = (char)('a' + d - 1);
            DigitCells[d] = letterToCell[letter];
            cellToDigit[letterToCell[letter]] = (char)('0' + d);
        }

        DigitCells[0] = letterToCell['j'];
        cellToDigit[letterToCell['j']] = '0';

        LetterToCell = letterToCell;
        CellToLetter = cellToLetter;
        PunctuationToCell = punctuationToCell;
        CellToPunctuation = cellToPunctuation;
        CellToDigit = cellToDigit;
    }

    /// <summary>Encodes <paramref name="text"/> to Unicode braille. Returns <see cref="string.Empty"/>
    /// for null/empty input.</summary>
    public string Encode(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length * 2);
        var inNumberMode = false;

        foreach (var ch in text)
        {
            if (ch is >= '0' and <= '9')
            {
                if (!inNumberMode)
                {
                    builder.Append(NumberSign);
                    inNumberMode = true;
                }

                builder.Append(DigitCells[ch - '0']);
                continue;
            }

            // Any non-digit ends the current number run.
            inNumberMode = false;

            if (ch == ' ')
            {
                builder.Append(Blank);
                continue;
            }

            if (char.IsUpper(ch) && LetterToCell.ContainsKey(char.ToLowerInvariant(ch)))
            {
                builder.Append(CapitalSign);
                builder.Append(LetterToCell[char.ToLowerInvariant(ch)]);
                continue;
            }

            if (LetterToCell.TryGetValue(char.ToLowerInvariant(ch), out var letterCell))
            {
                builder.Append(letterCell);
                continue;
            }

            if (PunctuationToCell.TryGetValue(ch, out var punctuationCell))
            {
                builder.Append(punctuationCell);
                continue;
            }

            // Unmapped: pass through so nothing is silently lost.
            builder.Append(ch);
        }

        return builder.ToString();
    }

    /// <summary>Decodes a Unicode braille string back to text, reversing the capital and number
    /// indicators. Non-braille characters pass through unchanged.</summary>
    public string Decode(string? braille)
    {
        if (string.IsNullOrEmpty(braille))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(braille.Length);
        var inNumberMode = false;
        var capitalNext = false;

        foreach (var cell in braille)
        {
            if (cell == NumberSign)
            {
                inNumberMode = true;
                capitalNext = false;
                continue;
            }

            if (cell == CapitalSign)
            {
                capitalNext = true;
                inNumberMode = false;
                continue;
            }

            if (cell == Blank)
            {
                builder.Append(' ');
                inNumberMode = false;
                capitalNext = false;
                continue;
            }

            if (inNumberMode && CellToDigit.TryGetValue(cell, out var digit))
            {
                builder.Append(digit);
                continue;
            }

            // A non-digit cell (or any cell once number mode is off) drops out of number mode.
            inNumberMode = false;

            if (CellToLetter.TryGetValue(cell, out var letter))
            {
                builder.Append(capitalNext ? char.ToUpperInvariant(letter) : letter);
                capitalNext = false;
                continue;
            }

            capitalNext = false;

            if (CellToPunctuation.TryGetValue(cell, out var punctuation))
            {
                builder.Append(punctuation);
                continue;
            }

            // Not a recognised braille cell: pass through (covers stray text in a decode stream).
            builder.Append(cell);
        }

        return builder.ToString();
    }

    /// <summary>
    /// A human-readable dot-number rendering of <paramref name="text"/>'s braille form, one
    /// space-separated group per cell (e.g. <c>m → "1-3-4"</c>, <c>1 → "3-4-5-6 1"</c>). The blank
    /// cell renders as <see cref="BlankDots"/>. This is a teaching aid beyond the reference tool.
    /// </summary>
    public string DescribeDots(string? text)
    {
        var braille = Encode(text);
        if (braille.Length == 0)
        {
            return string.Empty;
        }

        var groups = new List<string>(braille.Length);
        foreach (var cell in braille)
        {
            groups.Add(DotsOfCell(cell));
        }

        return string.Join(' ', groups);
    }

    /// <summary>The dash-joined dot numbers raised in a single braille <paramref name="cell"/>
    /// (e.g. <c>U+2807 → "1-2-3"</c>); the blank cell yields <see cref="BlankDots"/>, a non-braille
    /// character yields itself.</summary>
    public static string DotsOfCell(char cell)
    {
        if (cell is < '⠀' or > '⣿')
        {
            return cell.ToString();
        }

        if (cell == Blank)
        {
            return BlankDots;
        }

        var bits = cell - '⠀';
        var dots = new List<int>(8);
        for (var dot = 1; dot <= 8; dot++)
        {
            if ((bits & (1 << (dot - 1))) != 0)
            {
                dots.Add(dot);
            }
        }

        return string.Join('-', dots);
    }

    private static char CellFromDots(int[] dots)
    {
        var bits = 0;
        foreach (var dot in dots)
        {
            bits |= 1 << (dot - 1);
        }

        return (char)('⠀' + bits);
    }
}
