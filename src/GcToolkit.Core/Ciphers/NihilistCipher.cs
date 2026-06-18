using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// How a cell's two-digit coordinate is composed from its row and column (both 1- or 0-based per
/// <see cref="NihilistOptions.Base"/>). The classic Nihilist cipher is <see cref="RowColumn"/>;
/// some variants (and dCode) emit column-first.
/// </summary>
public enum NihilistOrientation
{
    /// <summary>Coordinate = <c>row*10 + column</c> (classic Nihilist).</summary>
    RowColumn,

    /// <summary>Coordinate = <c>column*10 + row</c>.</summary>
    ColumnRow,
}

/// <summary>
/// Knobs that pin down a Nihilist square + numbering variant. Defaults match the canonical
/// Wikipedia cipher: a 5×5 (I/J-merged) square, row-column orientation, and 1-based cells.
/// </summary>
/// <param name="Size">Square side: 5 (25 letters, I/J merged) or 6 (A–Z plus 0–9).</param>
/// <param name="Orientation">Row-column (classic) or column-row coordinate order.</param>
/// <param name="ZeroBased">
/// When <see langword="false"/> (the default, classic) cells count from 1; when <see langword="true"/>
/// they count from 0. Designed so <c>default(NihilistOptions)</c> is the canonical 1-based variant.
/// </param>
/// <param name="Alphabet">
/// Optional explicit alphabet to seed instead of the size's default. Must contain exactly
/// <c>Size*Size</c> distinct characters. <see langword="null"/> uses the default (A–Z I/J-merged for 5,
/// A–Z0–9 for 6).
/// </param>
public readonly record struct NihilistOptions(
    int Size = 5,
    NihilistOrientation Orientation = NihilistOrientation.RowColumn,
    bool ZeroBased = false,
    string? Alphabet = null)
{
    /// <summary>The first cell index implied by <see cref="ZeroBased"/> (0 or 1).</summary>
    public int Base => ZeroBased ? 0 : 1;
}

/// <summary>The result of an encode/decode, plus the per-symbol breakdown for teaching/debugging.</summary>
/// <param name="Text">The transformed text (cipher number stream, or recovered plaintext).</param>
/// <param name="Steps">One entry per processed plaintext symbol: plain value, key value, cipher value.</param>
public readonly record struct NihilistResult(string Text, IReadOnlyList<NihilistStep> Steps);

/// <summary>A single plain + key = cipher step in a Nihilist transform.</summary>
/// <param name="Symbol">The plaintext letter/digit (as placed in the square, e.g. <c>J</c>→<c>I</c>).</param>
/// <param name="PlainValue">The square coordinate of <see cref="Symbol"/>.</param>
/// <param name="KeyValue">The additive key's coordinate added (encrypt) / subtracted (decrypt).</param>
/// <param name="CipherValue">The emitted cipher number (<see cref="PlainValue"/> ± <see cref="KeyValue"/>).</param>
public readonly record struct NihilistStep(char Symbol, int PlainValue, int KeyValue, int CipherValue);

/// <summary>Raised when a keyword/text contains a character that the chosen square cannot represent.</summary>
public sealed class NihilistCipherException(string message) : Exception(message);

/// <summary>
/// A pure, stateless Nihilist cipher — the single source of truth for the transform. A Polybius square
/// is seeded by a <em>Polybius keyword</em> (duplicate letters removed, then the rest of the alphabet
/// appended); each letter maps to its two-digit row+column coordinate. An <em>additive keyword</em>'s
/// coordinate values are added to each plaintext value on encrypt and subtracted on decrypt, repeating
/// the key as needed. Output is a space-separated stream of two/three-digit numbers.
///
/// Beyond cachesleuth.com parity (keyword-seeded square, additive key, 5×5/6×6), this codec exposes a
/// grid-orientation toggle (row-column vs column-row), a configurable cell base (0 or 1), a custom
/// alphabet, the rendered square, the plain+key=cipher breakdown, and strict validation of empty
/// keywords, off-square characters, malformed number streams and out-of-range cipher values.
/// </summary>
public sealed class NihilistCipher
{
    /// <summary>The 25-letter alphabet for a 5×5 square (J is folded onto I).</summary>
    public const string Default5x5Alphabet = "ABCDEFGHIKLMNOPQRSTUVWXYZ";

    /// <summary>The 36-symbol alphabet for a 6×6 square (all letters plus digits, no merging).</summary>
    public const string Default6x6Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// Builds the Polybius square for <paramref name="polybiusKeyword"/> and <paramref name="options"/>:
    /// the de-duplicated keyword letters first (in order), then the remaining alphabet letters. Returns
    /// the flattened reading order (row by row); index <c>i</c> sits at row <c>i / Size</c>, column
    /// <c>i % Size</c>.
    /// </summary>
    /// <exception cref="NihilistCipherException">A keyword character is not in the square's alphabet.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> is internally inconsistent.</exception>
    public string BuildSquare(string? polybiusKeyword, NihilistOptions options = default)
    {
        var alphabet = ResolveAlphabet(options);
        var seen = new HashSet<char>();
        var builder = new StringBuilder(alphabet.Length);

        foreach (var raw in polybiusKeyword ?? string.Empty)
        {
            if (char.IsWhiteSpace(raw))
            {
                continue;
            }

            var c = Fold(raw, alphabet);
            if (!alphabet.Contains(c))
            {
                throw new NihilistCipherException($"Keyword character '{raw}' is not in the square alphabet.");
            }

            if (seen.Add(c))
            {
                builder.Append(c);
            }
        }

        foreach (var c in alphabet)
        {
            if (seen.Add(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>The rows of the square as strings, top to bottom — for rendering the grid.</summary>
    public IReadOnlyList<string> SquareRows(string? polybiusKeyword, NihilistOptions options = default)
    {
        var size = ResolveSize(options);
        var square = BuildSquare(polybiusKeyword, options);
        var rows = new List<string>(size);
        for (var r = 0; r < size; r++)
        {
            rows.Add(square.Substring(r * size, size));
        }

        return rows;
    }

    /// <summary>The 1- or 0-based axis labels for the rendered square (e.g. <c>1 2 3 4 5</c>).</summary>
    public IReadOnlyList<int> AxisLabels(NihilistOptions options = default)
    {
        var size = ResolveSize(options);
        var start = options.Base;
        var labels = new List<int>(size);
        for (var i = 0; i < size; i++)
        {
            labels.Add(start + i);
        }

        return labels;
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with the keyword-seeded square and the repeating
    /// <paramref name="additiveKeyword"/>. Non-alphabet characters in the plaintext (spaces,
    /// punctuation) are skipped — only square symbols are encoded. Output is space-separated numbers.
    /// </summary>
    /// <exception cref="NihilistCipherException">
    /// The additive keyword is empty/has no square characters, or a plaintext character is not in the square.
    /// </exception>
    public NihilistResult Encrypt(string? plaintext, string? polybiusKeyword, string? additiveKeyword, NihilistOptions options = default)
    {
        var square = BuildSquare(polybiusKeyword, options);
        var keyValues = KeyValuesForSquare(additiveKeyword, square, options);

        var steps = new List<NihilistStep>();
        var numbers = new List<string>();
        var keyIndex = 0;

        foreach (var raw in plaintext ?? string.Empty)
        {
            if (char.IsWhiteSpace(raw))
            {
                continue;
            }

            var c = Fold(raw, square);
            if (!TryCoordinate(square, c, options, out var plainValue))
            {
                throw new NihilistCipherException($"Plaintext character '{raw}' is not in the square.");
            }

            var keyValue = keyValues[keyIndex % keyValues.Count];
            keyIndex++;
            var cipherValue = plainValue + keyValue;

            steps.Add(new NihilistStep(c, plainValue, keyValue, cipherValue));
            numbers.Add(cipherValue.ToString());
        }

        return new NihilistResult(string.Join(' ', numbers), steps);
    }

    /// <summary>
    /// Decrypts a space-separated <paramref name="numberStream"/> back to plaintext, subtracting the
    /// repeating <paramref name="additiveKeyword"/>. Any token separator (spaces, commas, newlines) is
    /// accepted.
    /// </summary>
    /// <exception cref="NihilistCipherException">
    /// The additive keyword is empty, a token is not a number, or a recovered value falls outside the square.
    /// </exception>
    public NihilistResult Decrypt(string? numberStream, string? polybiusKeyword, string? additiveKeyword, NihilistOptions options = default)
    {
        var square = BuildSquare(polybiusKeyword, options);
        var keyValues = KeyValuesForSquare(additiveKeyword, square, options);

        var tokens = (numberStream ?? string.Empty)
            .Split([' ', '\t', '\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries);

        var steps = new List<NihilistStep>();
        var text = new StringBuilder(tokens.Length);
        var keyIndex = 0;

        foreach (var token in tokens)
        {
            if (!int.TryParse(token, out var cipherValue))
            {
                throw new NihilistCipherException($"'{token}' is not a valid cipher number.");
            }

            var keyValue = keyValues[keyIndex % keyValues.Count];
            keyIndex++;
            var plainValue = cipherValue - keyValue;

            if (!TryDecodeCoordinate(square, plainValue, options, out var symbol))
            {
                throw new NihilistCipherException($"Cipher value {cipherValue} decodes to an out-of-range coordinate {plainValue}.");
            }

            steps.Add(new NihilistStep(symbol, plainValue, keyValue, cipherValue));
            text.Append(symbol);
        }

        return new NihilistResult(text.ToString(), steps);
    }

    /// <summary>The repeating additive key's coordinate values, in order.</summary>
    /// <exception cref="NihilistCipherException">The keyword is empty or has no square characters.</exception>
    public IReadOnlyList<int> KeyValues(string? additiveKeyword, string? polybiusKeyword, NihilistOptions options = default)
        => KeyValuesForSquare(additiveKeyword, BuildSquare(polybiusKeyword, options), options);

    private List<int> KeyValuesForSquare(string? additiveKeyword, string square, NihilistOptions options)
    {
        if (string.IsNullOrWhiteSpace(additiveKeyword))
        {
            throw new NihilistCipherException("The additive keyword must not be empty.");
        }

        var values = new List<int>();
        foreach (var raw in additiveKeyword)
        {
            if (char.IsWhiteSpace(raw))
            {
                continue;
            }

            var c = Fold(raw, square);
            if (!TryCoordinate(square, c, options, out var value))
            {
                throw new NihilistCipherException($"Additive key character '{raw}' is not in the square.");
            }

            values.Add(value);
        }

        if (values.Count == 0)
        {
            throw new NihilistCipherException("The additive keyword has no characters that fit the square.");
        }

        return values;
    }

    /// <summary>The two-digit coordinate of <paramref name="symbol"/> in <paramref name="square"/>, honoring orientation/base.</summary>
    private static bool TryCoordinate(string square, char symbol, NihilistOptions options, out int coordinate)
    {
        var index = square.IndexOf(symbol);
        if (index < 0)
        {
            coordinate = 0;
            return false;
        }

        var size = (int)Math.Sqrt(square.Length);
        var start = options.Base;
        var row = index / size + start;
        var col = index % size + start;

        coordinate = options.Orientation == NihilistOrientation.ColumnRow
            ? col * 10 + row
            : row * 10 + col;
        return true;
    }

    /// <summary>The square symbol at <paramref name="coordinate"/>, or failure if the coordinate is off-grid.</summary>
    private static bool TryDecodeCoordinate(string square, int coordinate, NihilistOptions options, [MaybeNullWhen(false)] out char symbol)
    {
        symbol = default;
        if (coordinate < 0)
        {
            return false;
        }

        var size = (int)Math.Sqrt(square.Length);
        var start = options.Base;
        var first = coordinate / 10 - start;
        var second = coordinate % 10 - start;

        var (row, col) = options.Orientation == NihilistOrientation.ColumnRow
            ? (second, first)
            : (first, second);

        if (row < 0 || row >= size || col < 0 || col >= size)
        {
            return false;
        }

        symbol = square[row * size + col];
        return true;
    }

    private static string ResolveAlphabet(NihilistOptions options)
    {
        var size = ResolveSize(options);
        if (options.Alphabet is { } custom)
        {
            var normalized = NormalizeAlphabet(custom);
            if (normalized.Length != size * size)
            {
                throw new ArgumentException($"A custom alphabet for a {size}×{size} square must have exactly {size * size} distinct characters.", nameof(options));
            }

            return normalized;
        }

        return size == 6 ? Default6x6Alphabet : Default5x5Alphabet;
    }

    private static string NormalizeAlphabet(string custom)
    {
        var seen = new HashSet<char>();
        var builder = new StringBuilder(custom.Length);
        foreach (var raw in custom)
        {
            if (char.IsWhiteSpace(raw))
            {
                continue;
            }

            var c = char.ToUpperInvariant(raw);
            if (!seen.Add(c))
            {
                throw new ArgumentException($"A custom alphabet must not repeat characters ('{c}').", nameof(custom));
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>5 or 6; defaults a zero/unset size to 5.</summary>
    private static int ResolveSize(NihilistOptions options) => options.Size switch
    {
        6 => 6,
        0 or 5 => 5,
        _ => throw new ArgumentException("Square size must be 5 or 6.", nameof(options)),
    };

    /// <summary>Upper-cases and, for the I/J-merged 25-letter square, folds J onto I.</summary>
    private static char Fold(char c, string alphabet)
    {
        var upper = char.ToUpperInvariant(c);
        if (upper == 'J' && !alphabet.Contains('J') && alphabet.Contains('I'))
        {
            return 'I';
        }

        return upper;
    }
}
