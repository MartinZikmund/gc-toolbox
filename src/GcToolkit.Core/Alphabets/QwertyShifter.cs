namespace GcToolkit.Core.Alphabets;

/// <summary>The physical keyboard layout whose key sequence drives the shift ordering.</summary>
public enum KeyboardLayout
{
    /// <summary>US QWERTY (default).</summary>
    Qwerty,

    /// <summary>Central-European QWERTZ (Y and Z swapped vs. QWERTY).</summary>
    Qwertz,

    /// <summary>French AZERTY (A↔Q, Z↔W swapped, M relocated).</summary>
    Azerty,
}

/// <summary>The character set the shift rotates within. Outside characters pass through unchanged.</summary>
public enum QwertyAlphabet
{
    /// <summary>Letters A–Z plus digits 0–9 — 36 keys (CacheSleuth "A-Z and 0-9").</summary>
    LettersDigits,

    /// <summary>Letters, digits and the main symbol keys — 47 keys (CacheSleuth "include special characters").</summary>
    Extended,
}

/// <summary>How the shift slides along the key ordering.</summary>
public enum ShiftDirection
{
    /// <summary>Shift toward the end of the ordering (CacheSleuth default).</summary>
    Right,

    /// <summary>Shift toward the start of the ordering.</summary>
    Left,
}

/// <summary>Which keys a character may move across.</summary>
public enum ShiftModel
{
    /// <summary>One circular ordering of every key — shift wraps from the last key to the first (CacheSleuth parity).</summary>
    Linear,

    /// <summary>Shift only within the character's own keyboard row, wrapping within that row.</summary>
    RowBounded,
}

/// <summary>One brute-force candidate: the <see cref="Shift"/> applied and the resulting <see cref="Text"/>.</summary>
public readonly record struct QwertyShiftResult(int Shift, string Text);

/// <summary>
/// A pure, stateless QWERTY keyboard shifter — the single source of truth for the transform. Treats
/// the keyboard's keys as one circular ordering (or, optionally, one ordering per row) and slides each
/// character left/right by N positions with wraparound. Mirrors cachesleuth.com (36-key "A-Z + 0-9" and
/// 47-key extended sets, left/right, circular wrap, pass-through of unknown characters) and goes beyond
/// it with selectable layouts (QWERTY/QWERTZ/AZERTY), a true row-bounded shift model, case preservation
/// with an optional fold-to-upper, one-call decoding, and a full brute-force list.
/// </summary>
public sealed class QwertyShifter
{
    /// <summary>Number of keys in the <see cref="QwertyAlphabet.LettersDigits"/> ordering.</summary>
    public const int LettersDigitsSize = 36;

    /// <summary>Number of keys in the <see cref="QwertyAlphabet.Extended"/> ordering.</summary>
    public const int ExtendedSize = 47;

    // The QWERTY key sequence read row by row (number row first), letters upper-case.
    // 36 keys: 10 digits + 26 letters in their physical key order.
    private static readonly string[] QwertyLettersDigitsRows =
    [
        "1234567890",
        "QWERTYUIOP",
        "ASDFGHJKL",
        "ZXCVBNM",
    ];

    // 47 keys: the same rows extended with the main US symbol keys in their physical positions
    // (12 + 13 + 12 + 10 = 47).
    private static readonly string[] QwertyExtendedRows =
    [
        "1234567890-=",
        "QWERTYUIOP[]\\",
        "ASDFGHJKL;'`",
        "ZXCVBNM,./",
    ];

    /// <summary>The number of keys in <paramref name="alphabet"/>.</summary>
    public static int AlphabetSize(QwertyAlphabet alphabet) => alphabet switch
    {
        QwertyAlphabet.LettersDigits => LettersDigitsSize,
        QwertyAlphabet.Extended => ExtendedSize,
        _ => LettersDigitsSize,
    };

    /// <summary>
    /// The ordered keys (upper-case) for <paramref name="layout"/> and <paramref name="alphabet"/> as a
    /// single string — the circular ordering a linear shift rotates within and the source of the on-screen
    /// keyboard data.
    /// </summary>
    public string KeyOrdering(KeyboardLayout layout, QwertyAlphabet alphabet)
        => string.Concat(KeyRows(layout, alphabet));

    /// <summary>The keyboard rows (upper-case) for <paramref name="layout"/> and <paramref name="alphabet"/>.</summary>
    public IReadOnlyList<string> KeyRows(KeyboardLayout layout, QwertyAlphabet alphabet)
    {
        var rows = alphabet == QwertyAlphabet.Extended ? QwertyExtendedRows : QwertyLettersDigitsRows;
        return layout switch
        {
            KeyboardLayout.Qwertz => [.. rows.Select(SwapToQwertz)],
            KeyboardLayout.Azerty => [.. rows.Select(SwapToAzerty)],
            _ => rows,
        };
    }

    /// <summary>
    /// Shifts every character of <paramref name="text"/> that belongs to the ordering by
    /// <paramref name="shift"/> positions in <paramref name="direction"/>, wrapping circularly. Letters keep
    /// their case unless <paramref name="foldToUpper"/> is set; characters outside the ordering pass through
    /// unchanged. <see cref="ShiftModel.RowBounded"/> confines the shift to each character's own keyboard row.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the shifted text.</returns>
    public string Encode(
        string? text,
        int shift,
        ShiftDirection direction = ShiftDirection.Right,
        QwertyAlphabet alphabet = QwertyAlphabet.LettersDigits,
        KeyboardLayout layout = KeyboardLayout.Qwerty,
        ShiftModel model = ShiftModel.Linear,
        bool foldToUpper = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var signed = direction == ShiftDirection.Left ? -shift : shift;
        var rows = KeyRows(layout, alphabet);

        // Map each upper-case key to its (rowIndex, columnIndex) once for O(1) lookup.
        var index = BuildIndex(rows);

        return string.Create(text.Length, (text, signed, rows, index, model, foldToUpper), static (span, state) =>
        {
            var (source, by, keyRows, lookup, shiftModel, fold) = state;
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = ShiftChar(source[i], by, keyRows, lookup, shiftModel, fold);
            }
        });
    }

    /// <summary>Decodes by shifting the opposite way — <see cref="Encode"/> with the negated shift.</summary>
    public string Decode(
        string? text,
        int shift,
        ShiftDirection direction = ShiftDirection.Right,
        QwertyAlphabet alphabet = QwertyAlphabet.LettersDigits,
        KeyboardLayout layout = KeyboardLayout.Qwerty,
        ShiftModel model = ShiftModel.Linear,
        bool foldToUpper = false)
        => Encode(text, -shift, direction, alphabet, layout, model, foldToUpper);

    /// <summary>
    /// Every non-trivial shift of <paramref name="text"/> (1 … size-1, in order) for the chosen
    /// ordering — for cracking an unknown keyboard shift. Always uses the linear model, since that is the
    /// circular ordering a brute force walks.
    /// </summary>
    public IReadOnlyList<QwertyShiftResult> BruteForce(
        string? text,
        ShiftDirection direction = ShiftDirection.Right,
        QwertyAlphabet alphabet = QwertyAlphabet.LettersDigits,
        KeyboardLayout layout = KeyboardLayout.Qwerty,
        bool foldToUpper = false)
    {
        var size = AlphabetSize(alphabet);
        var results = new List<QwertyShiftResult>(size - 1);
        for (var shift = 1; shift < size; shift++)
        {
            results.Add(new QwertyShiftResult(shift, Encode(text, shift, direction, alphabet, layout, ShiftModel.Linear, foldToUpper)));
        }

        return results;
    }

    private static char ShiftChar(
        char c,
        int by,
        IReadOnlyList<string> rows,
        IReadOnlyDictionary<char, (int Row, int Col)> lookup,
        ShiftModel model,
        bool fold)
    {
        var upper = char.ToUpperInvariant(c);
        if (!lookup.TryGetValue(upper, out var pos))
        {
            return c; // Outside the ordering — pass through unchanged.
        }

        char shifted;
        if (model == ShiftModel.RowBounded)
        {
            var row = rows[pos.Row];
            shifted = row[Mod(pos.Col + by, row.Length)];
        }
        else
        {
            // Flatten to a single circular ordering.
            var flatIndex = 0;
            for (var r = 0; r < pos.Row; r++)
            {
                flatIndex += rows[r].Length;
            }

            flatIndex += pos.Col;

            var size = 0;
            foreach (var row in rows)
            {
                size += row.Length;
            }

            var target = Mod(flatIndex + by, size);
            shifted = KeyAt(rows, target);
        }

        if (fold)
        {
            return shifted;
        }

        // Restore the original case for letters; non-letters are returned as-is.
        return char.IsLetter(c) && char.IsLower(c) ? char.ToLowerInvariant(shifted) : shifted;
    }

    private static char KeyAt(IReadOnlyList<string> rows, int flatIndex)
    {
        foreach (var row in rows)
        {
            if (flatIndex < row.Length)
            {
                return row[flatIndex];
            }

            flatIndex -= row.Length;
        }

        return rows[0][0]; // Unreachable for a normalized index.
    }

    private static Dictionary<char, (int Row, int Col)> BuildIndex(IReadOnlyList<string> rows)
    {
        var map = new Dictionary<char, (int Row, int Col)>();
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var col = 0; col < row.Length; col++)
            {
                map[row[col]] = (r, col);
            }
        }

        return map;
    }

    /// <summary>QWERTZ swaps the physical positions of Y and Z relative to QWERTY.</summary>
    private static string SwapToQwertz(string row)
        => Swap(row, 'Y', 'Z');

    /// <summary>AZERTY swaps A↔Q and Z↔W relative to QWERTY (the puzzle-relevant top/home row keys).</summary>
    private static string SwapToAzerty(string row)
        => Swap(Swap(row, 'A', 'Q'), 'Z', 'W');

    private static string Swap(string row, char a, char b)
    {
        if (!row.Contains(a) && !row.Contains(b))
        {
            return row;
        }

        return string.Create(row.Length, (row, a, b), static (span, state) =>
        {
            var (source, x, y) = state;
            for (var i = 0; i < span.Length; i++)
            {
                var ch = source[i];
                span[i] = ch == x ? y : ch == y ? x : ch;
            }
        });
    }

    /// <summary>Reduces an arbitrary signed index into <c>[0, size)</c>.</summary>
    private static int Mod(int value, int size) => ((value % size) + size) % size;
}
