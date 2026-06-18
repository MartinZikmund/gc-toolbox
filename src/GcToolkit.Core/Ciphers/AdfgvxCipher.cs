using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// The two WWI cipher variants. <see cref="Adfgx"/> uses a 5×5 Polybius square (25 letters, J folded
/// to I) with header letters A D F G X; <see cref="Adfgvx"/> uses a 6×6 square (26 letters + 10 digits)
/// with header letters A D F G V X.
/// </summary>
public enum AdfgvxVariant
{
    /// <summary>5×5 square, letters only (J→I), headers A D F G X.</summary>
    Adfgx,

    /// <summary>6×6 square, letters and digits, headers A D F G V X.</summary>
    Adfgvx,
}

/// <summary>
/// One step of the worked pipeline preview: a single plaintext character and the coordinate pair
/// (two header letters) it substitutes to.
/// </summary>
public readonly record struct AdfgvxSubstitution(char PlainChar, string Pair);

/// <summary>The outcome of a transform: the resulting <see cref="Text"/> and any cleaning <see cref="Notes"/>.</summary>
public readonly record struct AdfgvxResult(string Text, IReadOnlyList<AdfgvxNote> Notes)
{
    /// <summary>Whether any non-fatal cleaning happened (J→I folding or characters dropped).</summary>
    public bool HasNotes => Notes.Count > 0;
}

/// <summary>A non-fatal observation about how the input was cleaned before transforming.</summary>
public enum AdfgvxNote
{
    /// <summary>One or more J letters were folded to I (ADFGX only).</summary>
    FoldedJToI,

    /// <summary>One or more characters were outside the square's alphabet and were ignored.</summary>
    DroppedUnsupportedChars,
}

/// <summary>
/// Thrown when the square or keyword cannot produce a valid transform (duplicate cells, wrong length,
/// empty keyword, malformed ciphertext). Carries a stable <see cref="Reason"/> the UI maps to a message.
/// </summary>
public sealed class AdfgvxException(AdfgvxError reason) : Exception(reason.ToString())
{
    public AdfgvxError Reason { get; } = reason;
}

/// <summary>The validation failures the cipher surfaces; the UI maps each to a localized message.</summary>
public enum AdfgvxError
{
    /// <summary>The square does not contain exactly the required number of distinct cells.</summary>
    IncompleteSquare,

    /// <summary>The square repeats a letter or digit.</summary>
    DuplicateCell,

    /// <summary>The square contains a character that is not allowed for the variant.</summary>
    InvalidSquareChar,

    /// <summary>The transposition keyword is empty (after removing non-letters).</summary>
    EmptyKeyword,

    /// <summary>The ciphertext does not consist solely of header letters, or is empty/odd for decoding.</summary>
    MalformedCiphertext,
}

/// <summary>
/// A pure, stateless ADFGX / ADFGVX codec — the single source of truth for the transform. It performs
/// the two classic stages: a mixed-alphabet Polybius-square substitution (each character → its
/// row/column header pair) followed by a keyword-driven columnar transposition (columns sorted stably
/// by keyword letter). Decoding reverses both stages. The square is built from a keyword-seeded fill or
/// an explicit fill string; input is cleaned forgivingly (J→I for ADFGX, unsupported characters
/// dropped) with the changes surfaced as <see cref="AdfgvxNote"/>s. Beyond geocachingtoolbox.com parity
/// this exposes the worked pipeline (square, per-character substitution) for a live preview.
/// </summary>
public sealed class AdfgvxCipher
{
    /// <summary>The five header letters of an ADFGX square (also the first five of ADFGVX).</summary>
    public const string Adfgx5Headers = "ADFGX";

    /// <summary>The six header letters of an ADFGVX square.</summary>
    public const string Adfgvx6Headers = "ADFGVX";

    /// <summary>The 25 letters of the ordered ADFGX alphabet (A–Z without J).</summary>
    public const string Adfgx5Alphabet = "ABCDEFGHIKLMNOPQRSTUVWXYZ";

    /// <summary>The 36 symbols of the ordered ADFGVX alphabet (A–Z then 0–9).</summary>
    public const string Adfgvx6Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>The side length of the square for <paramref name="variant"/> (5 or 6).</summary>
    public static int Size(AdfgvxVariant variant) => variant == AdfgvxVariant.Adfgx ? 5 : 6;

    /// <summary>The header letters for <paramref name="variant"/> (<c>ADFGX</c> or <c>ADFGVX</c>).</summary>
    public static string Headers(AdfgvxVariant variant)
        => variant == AdfgvxVariant.Adfgx ? Adfgx5Headers : Adfgvx6Headers;

    /// <summary>The ordered alphabet for <paramref name="variant"/> (25 or 36 symbols).</summary>
    public static string Alphabet(AdfgvxVariant variant)
        => variant == AdfgvxVariant.Adfgx ? Adfgx5Alphabet : Adfgvx6Alphabet;

    /// <summary>
    /// The natural ordered square fill for <paramref name="variant"/> (A–Z without J, or A–Z + 0–9),
    /// the default preset shown before the user customizes it.
    /// </summary>
    public string OrderedFill(AdfgvxVariant variant) => Alphabet(variant);

    /// <summary>
    /// Builds a keyword-seeded fill: the distinct letters/digits of <paramref name="key"/> (in order,
    /// J→I-folded for ADFGX, unsupported characters ignored) followed by the remaining alphabet symbols.
    /// A classic way to share a memorable square. An empty/blank key yields <see cref="OrderedFill"/>.
    /// </summary>
    public string KeywordSeededFill(string? key, AdfgvxVariant variant)
    {
        var alphabet = Alphabet(variant);
        var seen = new HashSet<char>();
        var builder = new StringBuilder(alphabet.Length);

        if (!string.IsNullOrEmpty(key))
        {
            foreach (var raw in key)
            {
                var c = NormalizeChar(raw, variant);
                if (c is char ch && seen.Add(ch))
                {
                    builder.Append(ch);
                }
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

    /// <summary>
    /// Validates a square <paramref name="fill"/> for <paramref name="variant"/> and returns the
    /// flattened, upper-cased square (row-major). Throws <see cref="AdfgvxException"/> on the wrong
    /// length, an unsupported character, or a duplicate cell.
    /// </summary>
    public string BuildSquare(string fill, AdfgvxVariant variant)
    {
        var size = Size(variant);
        var expected = size * size;
        var alphabet = Alphabet(variant);

        var normalized = new char[expected];
        var seen = new HashSet<char>();
        var count = 0;

        foreach (var raw in fill ?? string.Empty)
        {
            if (char.IsWhiteSpace(raw))
            {
                continue;
            }

            var upper = char.ToUpperInvariant(raw);
            if (!alphabet.Contains(upper))
            {
                throw new AdfgvxException(AdfgvxError.InvalidSquareChar);
            }

            if (!seen.Add(upper))
            {
                throw new AdfgvxException(AdfgvxError.DuplicateCell);
            }

            if (count >= expected)
            {
                throw new AdfgvxException(AdfgvxError.IncompleteSquare);
            }

            normalized[count++] = upper;
        }

        if (count != expected)
        {
            throw new AdfgvxException(AdfgvxError.IncompleteSquare);
        }

        return new string(normalized);
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/>: substitutes each character to its header pair, then applies
    /// the keyword columnar transposition. Input is cleaned forgivingly (J→I for ADFGX, unsupported
    /// characters dropped) and the changes are reported in <see cref="AdfgvxResult.Notes"/>.
    /// </summary>
    public AdfgvxResult Encrypt(string? plaintext, string fill, string keyword, AdfgvxVariant variant)
    {
        var square = BuildSquare(fill, variant);
        var key = NormalizeKeyword(keyword);

        var (substituted, notes) = Substitute(plaintext, square, variant);
        var transposed = Transpose(substituted, key);
        return new AdfgvxResult(transposed, notes);
    }

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/>: reverses the keyword transposition, then maps each header
    /// pair back through the square. Throws <see cref="AdfgvxException"/> when the ciphertext is empty,
    /// of odd length, or contains a non-header letter.
    /// </summary>
    public AdfgvxResult Decrypt(string? ciphertext, string fill, string keyword, AdfgvxVariant variant)
    {
        var square = BuildSquare(fill, variant);
        var key = NormalizeKeyword(keyword);

        var headers = Headers(variant);
        var cleaned = CleanCiphertext(ciphertext, headers);
        if (cleaned.Length == 0 || cleaned.Length % 2 != 0)
        {
            throw new AdfgvxException(AdfgvxError.MalformedCiphertext);
        }

        var untransposed = Untranspose(cleaned, key);
        var plain = Desubstitute(untransposed, square, variant);
        return new AdfgvxResult(plain, []);
    }

    /// <summary>
    /// The per-character substitution steps for a live pipeline preview: each kept plaintext character
    /// paired with its two header letters. Mirrors the cleaning of <see cref="Encrypt"/>.
    /// </summary>
    public IReadOnlyList<AdfgvxSubstitution> SubstitutionSteps(string? plaintext, string fill, AdfgvxVariant variant)
    {
        var square = BuildSquare(fill, variant);
        var headers = Headers(variant);
        var size = Size(variant);
        var steps = new List<AdfgvxSubstitution>();

        if (string.IsNullOrEmpty(plaintext))
        {
            return steps;
        }

        foreach (var raw in plaintext)
        {
            if (NormalizeChar(raw, variant) is not char c)
            {
                continue;
            }

            var index = square.IndexOf(c);
            var pair = $"{headers[index / size]}{headers[index % size]}";
            steps.Add(new AdfgvxSubstitution(char.ToUpperInvariant(raw), pair));
        }

        return steps;
    }

    // ---- Stage 1: substitution ----

    private (string Substituted, IReadOnlyList<AdfgvxNote> Notes) Substitute(string? text, string square, AdfgvxVariant variant)
    {
        var headers = Headers(variant);
        var size = Size(variant);
        var builder = new StringBuilder();
        var foldedJ = false;
        var dropped = false;

        if (!string.IsNullOrEmpty(text))
        {
            foreach (var raw in text)
            {
                if (variant == AdfgvxVariant.Adfgx && (raw is 'J' or 'j'))
                {
                    foldedJ = true;
                }

                if (NormalizeChar(raw, variant) is not char c)
                {
                    if (!char.IsWhiteSpace(raw))
                    {
                        dropped = true;
                    }

                    continue;
                }

                var index = square.IndexOf(c);
                builder.Append(headers[index / size]);
                builder.Append(headers[index % size]);
            }
        }

        var notes = new List<AdfgvxNote>(2);
        if (foldedJ)
        {
            notes.Add(AdfgvxNote.FoldedJToI);
        }

        if (dropped)
        {
            notes.Add(AdfgvxNote.DroppedUnsupportedChars);
        }

        return (builder.ToString(), notes);
    }

    private static string Desubstitute(string pairs, string square, AdfgvxVariant variant)
    {
        var headers = Headers(variant);
        var size = Size(variant);
        var builder = new StringBuilder(pairs.Length / 2);

        for (var i = 0; i + 1 < pairs.Length; i += 2)
        {
            var row = headers.IndexOf(pairs[i]);
            var col = headers.IndexOf(pairs[i + 1]);
            if (row < 0 || col < 0)
            {
                throw new AdfgvxException(AdfgvxError.MalformedCiphertext);
            }

            builder.Append(square[row * size + col]);
        }

        return builder.ToString();
    }

    // ---- Stage 2: keyword columnar transposition ----

    /// <summary>
    /// Writes <paramref name="text"/> row by row under <paramref name="keyword"/>, then reads the columns
    /// out in the order given by sorting the keyword letters alphabetically (stable for repeats).
    /// </summary>
    private static string Transpose(string text, string keyword)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var cols = keyword.Length;
        var order = ColumnOrder(keyword);
        var builder = new StringBuilder(text.Length);

        foreach (var col in order)
        {
            for (var i = col; i < text.Length; i += cols)
            {
                builder.Append(text[i]);
            }
        }

        return builder.ToString();
    }

    /// <summary>Reverses <see cref="Transpose"/>: rebuilds the grid column by column, then reads it row by row.</summary>
    private static string Untranspose(string text, string keyword)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var cols = keyword.Length;
        var rows = (text.Length + cols - 1) / cols;
        var remainder = text.Length % cols; // number of columns that carry an extra (top) cell
        var order = ColumnOrder(keyword);

        // How tall each column is: the first `remainder` columns (by original index) have `rows` cells,
        // the rest have `rows - 1`. remainder == 0 means the grid is full and every column has `rows`.
        var grid = new char[rows, cols];
        var filled = new bool[rows, cols];

        var pos = 0;
        foreach (var col in order)
        {
            var height = remainder == 0 || col < remainder ? rows : rows - 1;
            for (var r = 0; r < height; r++)
            {
                grid[r, col] = text[pos++];
                filled[r, col] = true;
            }
        }

        var builder = new StringBuilder(text.Length);
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                if (filled[r, c])
                {
                    builder.Append(grid[r, c]);
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>The column indices in read-out order: a stable sort of the keyword letters.</summary>
    private static int[] ColumnOrder(string keyword)
    {
        var indices = new int[keyword.Length];
        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        // Stable order: by letter, ties broken by original column index (Array.Sort isn't stable,
        // so fold the index into the comparison).
        Array.Sort(indices, (a, b) =>
        {
            var byLetter = keyword[a].CompareTo(keyword[b]);
            return byLetter != 0 ? byLetter : a.CompareTo(b);
        });

        return indices;
    }

    // ---- Cleaning helpers ----

    private string NormalizeKeyword(string? keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            throw new AdfgvxException(AdfgvxError.EmptyKeyword);
        }

        var builder = new StringBuilder(keyword.Length);
        foreach (var raw in keyword)
        {
            if (char.IsLetterOrDigit(raw))
            {
                builder.Append(char.ToUpperInvariant(raw));
            }
        }

        if (builder.Length == 0)
        {
            throw new AdfgvxException(AdfgvxError.EmptyKeyword);
        }

        return builder.ToString();
    }

    /// <summary>Keeps only header letters (case-insensitive), upper-cased — the only valid ciphertext symbols.</summary>
    private static string CleanCiphertext(string? ciphertext, string headers)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(ciphertext.Length);
        foreach (var raw in ciphertext)
        {
            var upper = char.ToUpperInvariant(raw);
            if (headers.Contains(upper))
            {
                builder.Append(upper);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Maps a raw input character into the square's alphabet: upper-cases it, folds J→I for ADFGX, and
    /// returns <see langword="null"/> for anything outside the alphabet (to be dropped).
    /// </summary>
    private static char? NormalizeChar(char raw, AdfgvxVariant variant)
    {
        var c = char.ToUpperInvariant(raw);
        if (variant == AdfgvxVariant.Adfgx && c == 'J')
        {
            c = 'I';
        }

        return Alphabet(variant).Contains(c) ? c : null;
    }
}
