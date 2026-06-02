namespace GcToolkit.Core.Ciphers;

/// <summary>
/// The alphabet a Caesar shift rotates within. Characters that fall outside the active alphabet
/// pass through unchanged, so punctuation, spaces and (in <see cref="Letters"/>/<see cref="Digits"/>
/// modes) the other character classes are preserved.
/// </summary>
public enum CaesarAlphabet
{
    /// <summary>The 26 Latin letters A–Z (case preserved). The classic Caesar cipher / ROT13.</summary>
    Letters,

    /// <summary>The 10 digits 0–9 (ROT5).</summary>
    Digits,

    /// <summary>The 94 printable ASCII characters <c>'!'</c>–<c>'~'</c> (33–126), i.e. ROT47.</summary>
    Ascii,
}

/// <summary>One brute-force candidate: the <see cref="Shift"/> applied and the resulting <see cref="Text"/>.</summary>
public readonly record struct CaesarShiftResult(int Shift, string Text);

/// <summary>
/// A pure, stateless Caesar (shift) cipher — the single source of truth for the transform. Rotates
/// characters within a chosen <see cref="CaesarAlphabet"/> by an arbitrary signed shift (normalized
/// modulo the alphabet size), preserves letter case, and passes every out-of-alphabet character
/// through unchanged. Beyond geocachingtoolbox.com parity (letters/ROT13, digits/ROT5, ASCII/ROT47,
/// and "show all rotations"), this codec preserves case and full Unicode, accepts any signed shift
/// for one-call decoding, and exposes the substitution alphabet for a "key" display.
/// </summary>
public sealed class CaesarCipher
{
    /// <summary>Number of symbols in the <see cref="CaesarAlphabet.Letters"/> alphabet (A–Z).</summary>
    public const int LetterAlphabetSize = 26;

    /// <summary>Number of symbols in the <see cref="CaesarAlphabet.Digits"/> alphabet (0–9).</summary>
    public const int DigitAlphabetSize = 10;

    /// <summary>Number of symbols in the <see cref="CaesarAlphabet.Ascii"/> alphabet (printable ASCII 33–126).</summary>
    public const int AsciiAlphabetSize = 94;

    private const char AsciiFirst = '!'; // 33
    private const char AsciiLast = '~';   // 126

    /// <summary>The number of symbols in <paramref name="alphabet"/>.</summary>
    public static int AlphabetSize(CaesarAlphabet alphabet) => alphabet switch
    {
        CaesarAlphabet.Letters => LetterAlphabetSize,
        CaesarAlphabet.Digits => DigitAlphabetSize,
        CaesarAlphabet.Ascii => AsciiAlphabetSize,
        _ => LetterAlphabetSize,
    };

    /// <summary>
    /// The conventional self-inverse rotation for <paramref name="alphabet"/>: 13 for letters (ROT13),
    /// 5 for digits (ROT5), 47 for ASCII (ROT47). At this shift the same key both encodes and decodes.
    /// </summary>
    public static int DefaultShift(CaesarAlphabet alphabet) => AlphabetSize(alphabet) / 2;

    /// <summary>
    /// Shifts every character of <paramref name="text"/> that belongs to <paramref name="alphabet"/>
    /// forward by <paramref name="shift"/> positions (normalized modulo the alphabet size; negative
    /// shifts decode). Letters keep their case; characters outside the alphabet are emitted unchanged.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for <see langword="null"/>/empty input; otherwise the shifted text.</returns>
    public string Transform(string? text, int shift, CaesarAlphabet alphabet = CaesarAlphabet.Letters)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var size = AlphabetSize(alphabet);
        var normalized = Normalize(shift, size);
        if (normalized == 0)
        {
            return text;
        }

        return string.Create(text.Length, (text, normalized, alphabet), static (span, state) =>
        {
            var (source, by, alpha) = state;
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = ShiftChar(source[i], by, alpha);
            }
        });
    }

    /// <summary>
    /// Every non-trivial rotation of <paramref name="text"/> for <paramref name="alphabet"/> — shifts
    /// <c>1 … size-1</c> in order — for brute-forcing an unknown Caesar shift (the "show all shifts"
    /// feature). Letters yield 25 candidates, digits 9, ASCII 93.
    /// </summary>
    public IReadOnlyList<CaesarShiftResult> AllShifts(string? text, CaesarAlphabet alphabet = CaesarAlphabet.Letters)
    {
        var size = AlphabetSize(alphabet);
        var results = new List<CaesarShiftResult>(size - 1);
        for (var shift = 1; shift < size; shift++)
        {
            results.Add(new CaesarShiftResult(shift, Transform(text, shift, alphabet)));
        }

        return results;
    }

    /// <summary>
    /// The substitution row for a "key" display: the plain alphabet of <paramref name="alphabet"/>
    /// rotated by <paramref name="shift"/>. Pairing this with <see cref="PlainAlphabet"/> shows exactly
    /// how each symbol maps. Letters are returned upper-case.
    /// </summary>
    public string CipherAlphabet(int shift, CaesarAlphabet alphabet = CaesarAlphabet.Letters)
        => Transform(PlainAlphabet(alphabet), shift, alphabet);

    /// <summary>The ordered plain alphabet for <paramref name="alphabet"/> (upper-case letters / digits / printable ASCII).</summary>
    public string PlainAlphabet(CaesarAlphabet alphabet)
    {
        var size = AlphabetSize(alphabet);
        var first = alphabet switch
        {
            CaesarAlphabet.Letters => 'A',
            CaesarAlphabet.Digits => '0',
            _ => AsciiFirst,
        };

        return string.Create(size, first, static (span, start) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = (char)(start + i);
            }
        });
    }

    private static char ShiftChar(char c, int by, CaesarAlphabet alphabet)
    {
        switch (alphabet)
        {
            case CaesarAlphabet.Letters:
                if (c is >= 'A' and <= 'Z')
                {
                    return (char)('A' + (c - 'A' + by) % LetterAlphabetSize);
                }

                if (c is >= 'a' and <= 'z')
                {
                    return (char)('a' + (c - 'a' + by) % LetterAlphabetSize);
                }

                return c;

            case CaesarAlphabet.Digits:
                if (c is >= '0' and <= '9')
                {
                    return (char)('0' + (c - '0' + by) % DigitAlphabetSize);
                }

                return c;

            case CaesarAlphabet.Ascii:
                if (c is >= AsciiFirst and <= AsciiLast)
                {
                    return (char)(AsciiFirst + (c - AsciiFirst + by) % AsciiAlphabetSize);
                }

                return c;

            default:
                return c;
        }
    }

    /// <summary>Reduces an arbitrary signed shift into <c>[0, size)</c>.</summary>
    private static int Normalize(int shift, int size) => ((shift % size) + size) % size;
}
