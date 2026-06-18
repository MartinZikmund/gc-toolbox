namespace GcToolkit.Core.Alphabets;

/// <summary>
/// The seven segments of a classic calculator display, named by the conventional letters. Each maps
/// to one boolean on a <see cref="SevenSegmentGlyph"/>:
/// <code>
///   aaaa
///  f    b
///  f    b
///   gggg
///  e    c
///  e    c
///   dddd
/// </code>
/// </summary>
public readonly record struct SevenSegments(bool A, bool B, bool C, bool D, bool E, bool F, bool G)
{
    /// <summary>The same segment pattern rotated 180° — the swap that makes a flipped display readable.
    /// Top/bottom (a↔d) and the two side pairs (b↔e, c↔f) trade places; the middle (g) is unchanged.</summary>
    public SevenSegments Rotated180() => new(D, E, F, A, B, C, G);
}

/// <summary>
/// A single character rendered on a seven-segment display, with the segment booleans for both the
/// upright and the 180°-rotated views so the UI can show a calculator reading right-side-up and
/// upside-down. Built for digits (and the few BEGHILOS letters), so a solver literally sees the trick.
/// </summary>
/// <param name="Character">The character this glyph shows (a digit, or a BEGHILOS letter).</param>
/// <param name="Segments">Which of the seven segments are lit for the upright reading.</param>
public sealed record SevenSegmentGlyph(char Character, SevenSegments Segments)
{
    /// <summary>The segment pattern for the upside-down (180°-rotated) reading.</summary>
    public SevenSegments Flipped => Segments.Rotated180();

    // Canonical segment patterns for the digits 0–9 and the readable BEGHILOS letters. Letters reuse
    // the digit pattern they resemble (B≈8, E≈3-with-extra is shown as its own E pattern, etc.).
    private static readonly IReadOnlyDictionary<char, SevenSegments> Patterns = BuildPatterns();

    /// <summary>Builds the glyph for <paramref name="character"/>, or <see langword="null"/> if the
    /// character has no seven-segment pattern. Digits and BEGHILOS letters are recognised (case-insensitive).</summary>
    public static SevenSegmentGlyph? For(char character)
    {
        var key = char.ToUpperInvariant(character);
        return Patterns.TryGetValue(key, out var segments) ? new SevenSegmentGlyph(character, segments) : null;
    }

    /// <summary>Maps every character of <paramref name="text"/> to a glyph, skipping any character with
    /// no seven-segment pattern. Returns an empty list for null/empty input.</summary>
    public static IReadOnlyList<SevenSegmentGlyph> ForText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var glyphs = new List<SevenSegmentGlyph>(text.Length);
        foreach (var ch in text)
        {
            if (For(ch) is { } glyph)
            {
                glyphs.Add(glyph);
            }
        }

        return glyphs;
    }

    private static Dictionary<char, SevenSegments> BuildPatterns()
    {
        // (a, b, c, d, e, f, g) for each digit — the standard seven-segment font.
        var digits = new Dictionary<char, SevenSegments>
        {
            ['0'] = new(true, true, true, true, true, true, false),
            ['1'] = new(false, true, true, false, false, false, false),
            ['2'] = new(true, true, false, true, true, false, true),
            ['3'] = new(true, true, true, true, false, false, true),
            ['4'] = new(false, true, true, false, false, true, true),
            ['5'] = new(true, false, true, true, false, true, true),
            ['6'] = new(true, false, true, true, true, true, true),
            ['7'] = new(true, true, true, false, false, false, false),
            ['8'] = new(true, true, true, true, true, true, true),
            ['9'] = new(true, true, true, true, false, true, true),
        };

        // BEGHILOS letters reuse the resembling digit's segment pattern.
        var letterToDigit = new Dictionary<char, char>
        {
            ['B'] = '8', ['E'] = '3', ['G'] = '6', ['H'] = '4', ['I'] = '1',
            ['L'] = '7', ['O'] = '0', ['S'] = '5', ['Z'] = '2',
        };

        foreach (var (letter, digit) in letterToDigit)
        {
            digits[letter] = digits[digit];
        }

        return digits;
    }
}
