using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// The outcome of attempting a Hill-cipher transform or matrix inversion: either a value or a
/// machine-readable failure reason the UI maps to a friendly localized message.
/// </summary>
public enum HillStatus
{
    /// <summary>The operation succeeded.</summary>
    Ok,

    /// <summary>The key matrix is not invertible mod 26 (gcd(det, 26) ≠ 1), so decryption is impossible.</summary>
    NotInvertible,

    /// <summary>The supplied key (numbers or keyword) did not fill an N×N matrix.</summary>
    InvalidKey,

    /// <summary>The input contained no encodable letters.</summary>
    NoLetters,
}

/// <summary>Result of a transform: the produced <see cref="Text"/> and a <see cref="Status"/>.</summary>
public readonly record struct HillResult(HillStatus Status, string Text)
{
    /// <summary>Whether the transform produced output.</summary>
    public bool Success => Status == HillStatus.Ok;

    internal static HillResult Fail(HillStatus status) => new(status, string.Empty);
}

/// <summary>Result of inverting a key matrix mod 26: the <see cref="Inverse"/> matrix and a <see cref="Status"/>.</summary>
public readonly record struct HillInverseResult(HillStatus Status, int[,] Inverse)
{
    /// <summary>Whether an inverse exists mod 26.</summary>
    public bool Success => Status == HillStatus.Ok;
}

/// <summary>
/// A pure, stateless Hill cipher — the single source of truth for the polygraphic matrix transform.
/// Maps letters A=0…Z=25 and encrypts blocks of N letters by left-multiplying with an N×N key matrix
/// mod 26; decryption multiplies by the matrix inverse mod 26 (adjugate × modular inverse of the
/// determinant). Supports any N ≥ 2 (2×2 and 3×3 are the common cases), keyword-derived keys filled
/// row-major, configurable pad character, and pass-through of non-letters. Invertibility is validated
/// up front: when gcd(det, 26) ≠ 1 the cipher reports <see cref="HillStatus.NotInvertible"/> rather
/// than returning a silently wrong answer. Beyond cachesleuth.com (2×2 only), this codec does N×N,
/// keyword keys, the live inverse display, and a known-plaintext key-recovery solver.
/// </summary>
public sealed class HillCipher
{
    /// <summary>The modulus for the standard 26-letter Latin alphabet.</summary>
    public const int Modulus = 26;

    /// <summary>
    /// Encrypts <paramref name="text"/> with the N×N <paramref name="key"/> matrix mod 26. Only letters
    /// are encoded (case ignored, output upper-case); every other character passes through unchanged.
    /// The final letter block is padded with <paramref name="pad"/>. Returns <see cref="HillStatus.InvalidKey"/>
    /// for a non-square/empty key and <see cref="HillStatus.NoLetters"/> when there is nothing to encode.
    /// </summary>
    public HillResult Encrypt(string? text, int[,] key, char pad = 'X')
        => Transform(text, key, pad, decrypt: false);

    /// <summary>
    /// Decrypts <paramref name="text"/> using the inverse of <paramref name="key"/> mod 26. Returns
    /// <see cref="HillStatus.NotInvertible"/> when the key has no inverse mod 26 (no silent wrong answer).
    /// </summary>
    public HillResult Decrypt(string? text, int[,] key, char pad = 'X')
    {
        var inverse = InvertMatrix(key);
        if (!inverse.Success)
        {
            return HillResult.Fail(inverse.Status);
        }

        return Transform(text, inverse.Inverse, pad, decrypt: true);
    }

    private HillResult Transform(string? text, int[,] matrix, char pad, bool decrypt)
    {
        var n = MatrixSize(matrix);
        if (n == 0)
        {
            return HillResult.Fail(HillStatus.InvalidKey);
        }

        // For encryption the key must be invertible too, so a round-trip is always possible and the
        // user is warned the moment they enter an unusable key.
        if (!decrypt && !IsInvertible(matrix))
        {
            return HillResult.Fail(HillStatus.NotInvertible);
        }

        if (string.IsNullOrEmpty(text))
        {
            return HillResult.Fail(HillStatus.NoLetters);
        }

        // Separate the letter stream (encoded) from everything else (passed through, position-preserved).
        var letters = new List<int>(text.Length);
        var slots = new int[text.Length]; // index into `letters`, or -1 for a pass-through char
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (TryToValue(c, out var v))
            {
                slots[i] = letters.Count;
                letters.Add(v);
            }
            else
            {
                slots[i] = -1;
            }
        }

        if (letters.Count == 0)
        {
            return HillResult.Fail(HillStatus.NoLetters);
        }

        var padValue = char.ToUpperInvariant(pad) - 'A';
        if (padValue is < 0 or >= Modulus)
        {
            padValue = 'X' - 'A';
        }

        var padded = letters.Count;
        while (padded % n != 0)
        {
            letters.Add(padValue);
            padded++;
        }

        var transformed = new int[letters.Count];
        var block = new int[n];
        for (var start = 0; start < letters.Count; start += n)
        {
            for (var r = 0; r < n; r++)
            {
                var sum = 0;
                for (var c = 0; c < n; c++)
                {
                    sum += matrix[r, c] * letters[start + c];
                }

                block[r] = Mod(sum);
            }

            for (var r = 0; r < n; r++)
            {
                transformed[start + r] = block[r];
            }
        }

        var sb = new StringBuilder(text.Length + n);
        var emittedLetters = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (slots[i] >= 0)
            {
                sb.Append((char)('A' + transformed[slots[i]]));
                emittedLetters++;
            }
            else
            {
                sb.Append(text[i]);
            }
        }

        // Padding letters were appended past the original input, so they have no position slot —
        // emit them at the end.
        for (var i = emittedLetters; i < transformed.Length; i++)
        {
            sb.Append((char)('A' + transformed[i]));
        }

        return new HillResult(HillStatus.Ok, sb.ToString());
    }

    /// <summary>
    /// The inverse of <paramref name="key"/> mod 26 (adjugate × modular inverse of the determinant),
    /// or a failure status when the matrix is non-square (<see cref="HillStatus.InvalidKey"/>) or not
    /// invertible mod 26 (<see cref="HillStatus.NotInvertible"/>).
    /// </summary>
    public HillInverseResult InvertMatrix(int[,] key)
    {
        var n = MatrixSize(key);
        if (n == 0)
        {
            return new HillInverseResult(HillStatus.InvalidKey, new int[0, 0]);
        }

        var det = Mod(Determinant(key));
        var detInverse = ModInverse(det, Modulus);
        if (detInverse < 0)
        {
            return new HillInverseResult(HillStatus.NotInvertible, new int[0, 0]);
        }

        var adjugate = Adjugate(key);
        var inverse = new int[n, n];
        for (var r = 0; r < n; r++)
        {
            for (var c = 0; c < n; c++)
            {
                inverse[r, c] = Mod(adjugate[r, c] * detInverse);
            }
        }

        return new HillInverseResult(HillStatus.Ok, inverse);
    }

    /// <summary>The determinant of <paramref name="matrix"/> reduced into <c>[0, 26)</c>.</summary>
    public int DeterminantMod(int[,] matrix) => Mod(Determinant(matrix));

    /// <summary><see langword="true"/> when <paramref name="matrix"/> is square and invertible mod 26.</summary>
    public bool IsInvertible(int[,] matrix)
    {
        var n = MatrixSize(matrix);
        return n != 0 && ModInverse(Mod(Determinant(matrix)), Modulus) >= 0;
    }

    /// <summary>
    /// Builds an N×N key matrix from <paramref name="keyword"/>, mapping its letters A=0…Z=25 row-major.
    /// Non-letters are ignored. Returns <see langword="null"/> when there are fewer than N² letters
    /// (the common geocaching key presentation: a phrase whose letters fill the grid).
    /// </summary>
    public int[,]? KeyFromKeyword(string? keyword, int size)
    {
        if (size < 2 || string.IsNullOrEmpty(keyword))
        {
            return null;
        }

        var values = new List<int>(keyword.Length);
        foreach (var c in keyword)
        {
            if (TryToValue(c, out var v))
            {
                values.Add(v);
            }
        }

        if (values.Count < size * size)
        {
            return null;
        }

        var matrix = new int[size, size];
        var k = 0;
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                matrix[r, c] = values[k++];
            }
        }

        return matrix;
    }

    /// <summary>
    /// Known-plaintext key recovery (beyond parity): given enough <paramref name="plaintext"/> /
    /// <paramref name="ciphertext"/> letters, recovers the N×N key matrix used to produce the
    /// ciphertext. Needs N independent plaintext blocks; returns <see langword="null"/> when the
    /// plaintext block matrix is not invertible mod 26 or there is insufficient material.
    /// </summary>
    public int[,]? RecoverKey(string? plaintext, string? ciphertext, int size)
    {
        if (size < 2)
        {
            return null;
        }

        var p = LettersToValues(plaintext);
        var c = LettersToValues(ciphertext);
        var need = size * size;
        if (p.Count < need || c.Count < need)
        {
            return null;
        }

        // Stack the first N plaintext blocks as columns of P and the matching ciphertext as C.
        // Then K = C · P⁻¹ (mod 26).
        var plain = new int[size, size];
        var cipher = new int[size, size];
        for (var col = 0; col < size; col++)
        {
            for (var row = 0; row < size; row++)
            {
                plain[row, col] = p[col * size + row];
                cipher[row, col] = c[col * size + row];
            }
        }

        var plainInverse = InvertMatrix(plain);
        if (!plainInverse.Success)
        {
            return null;
        }

        return Multiply(cipher, plainInverse.Inverse);
    }

    /// <summary>Multiplies two square matrices of equal size mod 26.</summary>
    public int[,] Multiply(int[,] a, int[,] b)
    {
        var n = a.GetLength(0);
        var result = new int[n, n];
        for (var r = 0; r < n; r++)
        {
            for (var c = 0; c < n; c++)
            {
                var sum = 0;
                for (var k = 0; k < n; k++)
                {
                    sum += a[r, k] * b[k, c];
                }

                result[r, c] = Mod(sum);
            }
        }

        return result;
    }

    private List<int> LettersToValues(string? text)
    {
        var values = new List<int>(text?.Length ?? 0);
        if (text is null)
        {
            return values;
        }

        foreach (var c in text)
        {
            if (TryToValue(c, out var v))
            {
                values.Add(v);
            }
        }

        return values;
    }

    private static bool TryToValue(char c, out int value)
    {
        if (c is >= 'A' and <= 'Z')
        {
            value = c - 'A';
            return true;
        }

        if (c is >= 'a' and <= 'z')
        {
            value = c - 'a';
            return true;
        }

        value = 0;
        return false;
    }

    private static int MatrixSize(int[,] matrix)
    {
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        return rows >= 2 && rows == cols ? rows : 0;
    }

    private static long Determinant(int[,] matrix)
    {
        var n = matrix.GetLength(0);
        if (n == 1)
        {
            return matrix[0, 0];
        }

        if (n == 2)
        {
            return (long)matrix[0, 0] * matrix[1, 1] - (long)matrix[0, 1] * matrix[1, 0];
        }

        // Laplace expansion along the first row — fine for the small N this cipher uses.
        long det = 0;
        for (var c = 0; c < n; c++)
        {
            var sign = c % 2 == 0 ? 1 : -1;
            det += sign * matrix[0, c] * Determinant(Minor(matrix, 0, c));
        }

        return det;
    }

    private static int[,] Adjugate(int[,] matrix)
    {
        var n = matrix.GetLength(0);
        var adjugate = new int[n, n];
        for (var r = 0; r < n; r++)
        {
            for (var c = 0; c < n; c++)
            {
                var sign = (r + c) % 2 == 0 ? 1 : -1;
                var cofactor = sign * Determinant(Minor(matrix, r, c));
                // Transpose of the cofactor matrix is the adjugate.
                adjugate[c, r] = Mod(cofactor);
            }
        }

        return adjugate;
    }

    private static int[,] Minor(int[,] matrix, int skipRow, int skipCol)
    {
        var n = matrix.GetLength(0);
        var minor = new int[n - 1, n - 1];
        var mr = 0;
        for (var r = 0; r < n; r++)
        {
            if (r == skipRow)
            {
                continue;
            }

            var mc = 0;
            for (var c = 0; c < n; c++)
            {
                if (c == skipCol)
                {
                    continue;
                }

                minor[mr, mc] = matrix[r, c];
                mc++;
            }

            mr++;
        }

        return minor;
    }

    /// <summary>The modular inverse of <paramref name="a"/> mod <paramref name="m"/>, or -1 when none exists.</summary>
    private static int ModInverse(int a, int m)
    {
        a = ((a % m) + m) % m;
        for (var x = 1; x < m; x++)
        {
            if (a * x % m == 1)
            {
                return x;
            }
        }

        return -1;
    }

    private static int Mod(long value) => (int)(((value % Modulus) + Modulus) % Modulus);
}
