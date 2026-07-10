namespace GcToolkit.Core.Ciphers;

/// <summary>One brute-force candidate: the <see cref="StartOffset"/> tried and the resulting <see cref="Text"/>.</summary>
public readonly record struct TrithemiusOffsetResult(int StartOffset, string Text);

/// <summary>
/// A pure, stateless Trithemius cipher — a keyless progressive polyalphabetic substitution and the
/// ancestor of Vigenère. The i-th letter (counting letters only, 0-based) is shifted by
/// <c>startOffset + i * step</c> positions modulo 26: with the defaults (<c>startOffset 0</c>,
/// <c>step 1</c>) <c>AAAA</c> encrypts to <c>ABCD</c>, matching the cachesleuth.com tabula recta.
/// Letter case is preserved and every non-letter passes through unchanged without advancing the
/// progressive shift. Beyond cachesleuth parity this codec exposes a configurable
/// <c>startOffset</c>/<c>step</c> and an <see cref="AllOffsets"/> brute-force view for an unknown start.
/// </summary>
public sealed class TrithemiusCipher
{
    /// <summary>Number of letters in the Latin alphabet (A–Z).</summary>
    public const int AlphabetSize = 26;

    /// <summary>
    /// Encrypts (or, when <paramref name="decrypt"/> is <see langword="true"/>, decrypts)
    /// <paramref name="text"/> with the progressive Trithemius shift: the i-th letter moves by
    /// <c><paramref name="startOffset"/> + i * <paramref name="step"/></c> positions (mod 26), where
    /// <c>i</c> counts letters only. Decrypting applies the same shifts backward. Case is preserved and
    /// non-letters are emitted unchanged without consuming a shift step.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the result.</returns>
    public string Transform(string? text, bool decrypt, int startOffset = 0, int step = 1)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return string.Create(text.Length, (text, decrypt, startOffset, step), static (span, state) =>
        {
            var (source, dec, start, by) = state;
            var letterIndex = 0;
            for (var i = 0; i < span.Length; i++)
            {
                var c = source[i];
                var shift = start + letterIndex * by;
                if (c is >= 'A' and <= 'Z')
                {
                    span[i] = ShiftLetter(c, 'A', dec ? -shift : shift);
                    letterIndex++;
                }
                else if (c is >= 'a' and <= 'z')
                {
                    span[i] = ShiftLetter(c, 'a', dec ? -shift : shift);
                    letterIndex++;
                }
                else
                {
                    span[i] = c;
                }
            }
        });
    }

    /// <summary>
    /// Decodes <paramref name="text"/> at every starting row (<c>startOffset 0 … 25</c>, keeping the
    /// default <c>step 1</c>) — 26 candidates for brute-forcing an unknown Trithemius start. The true
    /// plaintext appears at the offset that was used to encrypt.
    /// </summary>
    public IReadOnlyList<TrithemiusOffsetResult> AllOffsets(string? text, int step = 1)
    {
        var results = new List<TrithemiusOffsetResult>(AlphabetSize);
        for (var offset = 0; offset < AlphabetSize; offset++)
        {
            results.Add(new TrithemiusOffsetResult(offset, Transform(text, decrypt: true, offset, step)));
        }

        return results;
    }

    /// <summary>
    /// The 26×26 tabula recta: row <c>r</c> is the plain alphabet rotated left by <c>r</c>
    /// (<c>ABC…Z</c>, <c>BCD…ZA</c>, …, <c>ZAB…Y</c>). The View renders this as the reference table.
    /// </summary>
    public IReadOnlyList<string> TabulaRecta()
    {
        var rows = new List<string>(AlphabetSize);
        for (var r = 0; r < AlphabetSize; r++)
        {
            rows.Add(string.Create(AlphabetSize, r, static (span, rotation) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = (char)('A' + (rotation + i) % AlphabetSize);
                }
            }));
        }

        return rows;
    }

    private static char ShiftLetter(char c, char origin, int by)
    {
        var normalized = ((by % AlphabetSize) + AlphabetSize) % AlphabetSize;
        return (char)(origin + (c - origin + normalized) % AlphabetSize);
    }
}
