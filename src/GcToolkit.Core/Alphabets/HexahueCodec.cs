using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>The outcome of encoding text to Hexahue: the rendered <see cref="Glyphs"/> plus the
/// characters that had no glyph, reported (never silently dropped) so the UI can warn the user.</summary>
public sealed record HexahueEncodeResult(
    IReadOnlyList<HexahueGlyph> Glyphs,
    IReadOnlyList<char> UnsupportedCharacters,
    int UnsupportedCount)
{
    /// <summary><see langword="true"/> when at least one input character could not be encoded.</summary>
    public bool HasUnsupported => UnsupportedCount > 0;
}

/// <summary>
/// Bidirectional Hexahue codec — the pure, head-independent source of truth for the colour-block
/// alphabet (issue #51). Every character is a 2×3 grid of six coloured squares
/// (<see cref="HexahueGlyph"/>): the 26 letters use the six hues (each once, in the canonical chart
/// order), the 10 digits use two squares each of white/grey/black, and <c>'.'</c>/<c>','</c> use a
/// black-and-white checker. Space is a dedicated all-white block. Input is case-insensitive and accented
/// Latin letters fold to their base (Č → C); characters with no glyph are reported, not dropped. Decoding
/// reverses the mapping, emitting <see cref="UnknownMarker"/> for an unrecognised glyph so it never throws.
/// </summary>
public sealed class HexahueCodec
{
    /// <summary>Stands in for an unrecognised glyph when decoding, so a bad input never throws.</summary>
    public const char UnknownMarker = '?';

    // The canonical chart, in reading order (TopLeft, TopRight, MiddleLeft, MiddleRight, BottomLeft,
    // BottomRight). This is the standard Hexahue mapping; the sixth hue is modelled as Magenta (the
    // View can draw it purple for the purple-convention variant).
    private static readonly (char Character, HexahueGlyph Glyph)[] Chart = BuildChart();

    private static readonly IReadOnlyDictionary<char, HexahueGlyph> CharToGlyph =
        Chart.ToDictionary(static e => e.Character, static e => e.Glyph);

    private static readonly IReadOnlyDictionary<HexahueGlyph, char> GlyphToChar =
        Chart.ToDictionary(static e => e.Glyph, static e => e.Character);

    /// <summary>
    /// Encodes <paramref name="text"/> to Hexahue glyphs. Letters are upper-cased and accented Latin
    /// letters fold to their base; any character without a glyph is collected in the result rather than
    /// silently dropped. Returns an empty result for null/empty input.
    /// </summary>
    public HexahueEncodeResult Encode(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new([], [], 0);
        }

        List<HexahueGlyph> glyphs = new(text.Length);
        List<char> unsupported = [];
        var unsupportedCount = 0;

        foreach (var character in text)
        {
            if (TryMap(character, out var glyph))
            {
                glyphs.Add(glyph);
                continue;
            }

            unsupportedCount++;
            if (!unsupported.Contains(character))
            {
                unsupported.Add(character);
            }
        }

        return new(glyphs, unsupported, unsupportedCount);
    }

    /// <summary>Decodes a Hexahue glyph sequence back to text. An unrecognised glyph becomes
    /// <see cref="UnknownMarker"/>. Returns <see cref="string.Empty"/> for an empty sequence.</summary>
    public string Decode(IReadOnlyList<HexahueGlyph> glyphs)
    {
        if (glyphs.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(glyphs.Count);
        foreach (var glyph in glyphs)
        {
            builder.Append(GlyphToChar.TryGetValue(glyph, out var character) ? character : UnknownMarker);
        }

        return builder.ToString();
    }

    /// <summary>The glyph for a single supported character, or <see langword="null"/> if it has none.
    /// Case-insensitive; the character must already be a base letter/digit/punctuation/space.</summary>
    public static HexahueGlyph? GlyphFor(char character)
        => CharToGlyph.TryGetValue(char.ToUpperInvariant(character), out var glyph) ? glyph : null;

    /// <summary>The full reference chart as an ordered, clickable list (26 letters, 10 digits,
    /// <c>'.'</c>, <c>','</c> and space) — the Hexahue "alphabet" the UI renders and lets you tap.</summary>
    public static IReadOnlyList<HexahuePaletteEntry> GetAlphabet()
        => [.. Chart.Select(e => new HexahuePaletteEntry(e.Character, e.Glyph))];

    private static bool TryMap(char character, out HexahueGlyph glyph)
    {
        var upper = char.ToUpperInvariant(character);
        if (CharToGlyph.TryGetValue(upper, out glyph!))
        {
            return true;
        }

        // No direct glyph: fold an accented Latin letter (e.g. Č → C) to its base letter and retry.
        if (FoldToBaseLetter(character) is char baseLetter
            && CharToGlyph.TryGetValue(char.ToUpperInvariant(baseLetter), out glyph!))
        {
            return true;
        }

        glyph = null!;
        return false;
    }

    private static char? FoldToBaseLetter(char character)
    {
        foreach (var candidate in character.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(candidate) != UnicodeCategory.NonSpacingMark)
            {
                // Only fold to a base letter; otherwise (e.g. a symbol) report it as unsupported.
                return char.IsLetter(candidate) ? candidate : null;
            }
        }

        return null;
    }

    private static (char, HexahueGlyph)[] BuildChart()
    {
        const HexahueColor m = HexahueColor.Magenta;
        const HexahueColor r = HexahueColor.Red;
        const HexahueColor g = HexahueColor.Green;
        const HexahueColor y = HexahueColor.Yellow;
        const HexahueColor b = HexahueColor.Blue;
        const HexahueColor c = HexahueColor.Cyan;
        const HexahueColor w = HexahueColor.White;
        const HexahueColor e = HexahueColor.Grey;
        const HexahueColor k = HexahueColor.Black;

        // Letters: the six hues, each used once, advancing through the canonical permutation chain.
        return
        [
            ('A', Glyph(m, r, g, y, b, c)),
            ('B', Glyph(r, m, g, y, b, c)),
            ('C', Glyph(r, g, m, y, b, c)),
            ('D', Glyph(r, g, y, m, b, c)),
            ('E', Glyph(r, g, y, b, m, c)),
            ('F', Glyph(r, g, y, b, c, m)),
            ('G', Glyph(g, r, y, b, c, m)),
            ('H', Glyph(g, y, r, b, c, m)),
            ('I', Glyph(g, y, b, r, c, m)),
            ('J', Glyph(g, y, b, c, r, m)),
            ('K', Glyph(g, y, b, c, m, r)),
            ('L', Glyph(y, g, b, c, m, r)),
            ('M', Glyph(y, b, g, c, m, r)),
            ('N', Glyph(y, b, c, g, m, r)),
            ('O', Glyph(y, b, c, m, g, r)),
            ('P', Glyph(y, b, c, m, r, g)),
            ('Q', Glyph(b, y, c, m, r, g)),
            ('R', Glyph(b, c, y, m, r, g)),
            ('S', Glyph(b, c, m, y, r, g)),
            ('T', Glyph(b, c, m, r, y, g)),
            ('U', Glyph(b, c, m, r, g, y)),
            ('V', Glyph(c, b, m, r, g, y)),
            ('W', Glyph(c, m, b, r, g, y)),
            ('X', Glyph(c, m, r, b, g, y)),
            ('Y', Glyph(c, m, r, g, b, y)),
            ('Z', Glyph(c, m, r, g, y, b)),

            // Punctuation: black-and-white checkers (no grey).
            ('.', Glyph(k, w, w, k, k, w)),
            (',', Glyph(w, k, k, w, w, k)),

            // Space: a dedicated all-white block.
            (' ', Glyph(w, w, w, w, w, w)),

            // Digits: two squares each of black/grey/white, advancing through the chart's grey chain.
            ('0', Glyph(k, e, w, k, e, w)),
            ('1', Glyph(e, k, w, k, e, w)),
            ('2', Glyph(e, w, k, k, e, w)),
            ('3', Glyph(e, w, k, e, k, w)),
            ('4', Glyph(e, w, k, e, w, k)),
            ('5', Glyph(w, e, k, e, w, k)),
            ('6', Glyph(w, k, e, e, w, k)),
            ('7', Glyph(w, k, e, w, e, k)),
            ('8', Glyph(w, k, e, w, k, e)),
            ('9', Glyph(k, w, e, w, k, e)),
        ];

        static HexahueGlyph Glyph(
            HexahueColor tl, HexahueColor tr, HexahueColor ml,
            HexahueColor mr, HexahueColor bl, HexahueColor br)
            => new(tl, tr, ml, mr, bl, br);
    }
}
