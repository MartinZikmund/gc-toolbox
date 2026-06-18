using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// Pure, head-independent codec for electronic segment displays (issue #58). Turns text into per-character
/// lit-segment patterns and back, across four display families (7-/9-/14-/16-segment). A glyph is modelled
/// as a set of lit segment labels / a bit mask, written in three interchangeable notations (label list,
/// binary mask, decimal). Goes beyond geocachingtoolbox.com — which only renders code → display — by closing
/// the loop to and from text, auto-detecting the notation, toggling bit order and common-anode/cathode
/// inversion, and ranking the nearest standard characters (segment Hamming distance) for an arbitrary mask.
/// </summary>
/// <remarks>
/// Segment labels are listed once per type in <see cref="SegmentLabels"/>; bit <c>i</c> of a mask is the
/// <c>i</c>-th label. Mappings on real hardware are <b>not universal</b>, so these are the most common
/// conventions and a custom mask can always be entered directly.
/// </remarks>
public sealed class SegmentDisplayCodec
{
    /// <summary>The token used for a character that cannot be rendered / a mask that matches nothing.</summary>
    public const char Unknown = '?';

    // Canonical segment label order per type. The index of a label is its bit position in the mask.
    private static readonly IReadOnlyDictionary<SegmentDisplayType, string[]> SegmentLabels =
        new Dictionary<SegmentDisplayType, string[]>
        {
            [SegmentDisplayType.SevenSegment] = ["a", "b", "c", "d", "e", "f", "g"],
            [SegmentDisplayType.NineSegment] = ["a", "b", "c", "d", "e", "f", "g", "h", "i"],
            [SegmentDisplayType.FourteenSegment] =
                ["a", "b", "c", "d", "e", "f", "g1", "g2", "h", "i", "j", "k", "l", "m"],
            [SegmentDisplayType.SixteenSegment] =
                ["a1", "a2", "b", "c", "d1", "d2", "e", "f", "g1", "g2", "h", "i", "j", "k", "l", "m"],
        };

    // ---- Glyph tables (character -> lit segment labels) ----
    //
    // 7-segment: a top, b top-right, c bottom-right, d bottom, e bottom-left, f top-left, g middle.
    // Digits are universal; letters use the common "calculator" set (b/d/etc. lower-case where the upper
    // form is indistinguishable from a digit). 14/16-segment carry the full alphabet via the centre
    // verticals (i/l) and diagonals (h/j/k/m); 9-segment adds the two upper diagonals (h ╲, i ╱).

    private static readonly Dictionary<char, string[]> SevenSegmentGlyphs = new()
    {
        ['0'] = ["a", "b", "c", "d", "e", "f"],
        ['1'] = ["b", "c"],
        ['2'] = ["a", "b", "g", "e", "d"],
        ['3'] = ["a", "b", "g", "c", "d"],
        ['4'] = ["f", "g", "b", "c"],
        ['5'] = ["a", "f", "g", "c", "d"],
        ['6'] = ["a", "f", "g", "e", "c", "d"],
        ['7'] = ["a", "b", "c"],
        ['8'] = ["a", "b", "c", "d", "e", "f", "g"],
        ['9'] = ["a", "b", "c", "d", "f", "g"],
        ['A'] = ["a", "b", "c", "e", "f", "g"],
        ['B'] = ["c", "d", "e", "f", "g"],   // lower-case 'b'
        ['C'] = ["a", "d", "e", "f"],
        ['D'] = ["b", "c", "d", "e", "g"],   // lower-case 'd'
        ['E'] = ["a", "d", "e", "f", "g"],
        ['F'] = ["a", "e", "f", "g"],
        ['G'] = ["a", "c", "d", "e", "f"],
        ['H'] = ["b", "c", "e", "f", "g"],
        ['I'] = ["e", "f"],
        ['J'] = ["b", "c", "d", "e"],
        ['L'] = ["d", "e", "f"],
        ['N'] = ["c", "e", "g"],             // lower-case 'n'
        ['O'] = ["a", "b", "c", "d", "e", "f"],
        ['P'] = ["a", "b", "e", "f", "g"],
        ['Q'] = ["a", "b", "c", "f", "g"],
        ['R'] = ["e", "g"],                  // lower-case 'r'
        ['S'] = ["a", "f", "g", "c", "d"],
        ['T'] = ["d", "e", "f", "g"],        // lower-case 't'
        ['U'] = ["b", "c", "d", "e", "f"],
        ['Y'] = ["b", "c", "d", "f", "g"],
        ['-'] = ["g"],
        ['_'] = ["d"],
    };

    // 14-segment full alphanumeric set. Letters use the centre column (i top-vertical, l bottom-vertical)
    // and diagonals (h top-left ╲, j top-right ╱, k bottom-left ╱, m bottom-right ╲) for crisp glyphs.
    private static readonly Dictionary<char, string[]> FourteenSegmentGlyphs = new()
    {
        ['0'] = ["a", "b", "c", "d", "e", "f", "j", "k"],
        ['1'] = ["b", "c", "j"],
        ['2'] = ["a", "b", "g1", "g2", "e", "d"],
        ['3'] = ["a", "b", "g2", "c", "d"],
        ['4'] = ["f", "g1", "g2", "b", "c"],
        ['5'] = ["a", "f", "g1", "m", "d"],
        ['6'] = ["a", "f", "g1", "g2", "e", "c", "d"],
        ['7'] = ["a", "b", "c"],
        ['8'] = ["a", "b", "c", "d", "e", "f", "g1", "g2"],
        ['9'] = ["a", "b", "c", "d", "f", "g1", "g2"],
        ['A'] = ["a", "b", "c", "e", "f", "g1", "g2"],
        ['B'] = ["a", "b", "c", "d", "g2", "i", "l"],
        ['C'] = ["a", "d", "e", "f"],
        ['D'] = ["a", "b", "c", "d", "i", "l"],
        ['E'] = ["a", "d", "e", "f", "g1", "g2"],
        ['F'] = ["a", "e", "f", "g1", "g2"],
        ['G'] = ["a", "c", "d", "e", "f", "g2"],
        ['H'] = ["b", "c", "e", "f", "g1", "g2"],
        ['I'] = ["a", "d", "i", "l"],
        ['J'] = ["b", "c", "d", "e"],
        ['K'] = ["e", "f", "g1", "j", "m"],
        ['L'] = ["d", "e", "f"],
        ['M'] = ["b", "c", "e", "f", "h", "j"],
        ['N'] = ["b", "c", "e", "f", "h", "m"],
        ['O'] = ["a", "b", "c", "d", "e", "f"],
        ['P'] = ["a", "b", "e", "f", "g1", "g2"],
        ['Q'] = ["a", "b", "c", "d", "e", "f", "m"],
        ['R'] = ["a", "b", "e", "f", "g1", "g2", "m"],
        ['S'] = ["a", "f", "g1", "g2", "c", "d"],
        ['T'] = ["a", "i", "l"],
        ['U'] = ["b", "c", "d", "e", "f"],
        ['V'] = ["e", "f", "k", "j"],
        ['W'] = ["b", "c", "e", "f", "k", "m"],
        ['X'] = ["h", "j", "k", "m"],
        ['Y'] = ["h", "j", "l"],
        ['Z'] = ["a", "d", "j", "k"],
        ['-'] = ["g1", "g2"],
        ['_'] = ["d"],
        ['+'] = ["g1", "g2", "i", "l"],
    };

    // 9-segment = 7-segment + two upper diagonals (h top-left ╲, i top-right ╱). Falls back to the
    // 7-segment table for everything that doesn't need the diagonals.
    private static readonly Dictionary<char, string[]> NineSegmentExtraGlyphs = new()
    {
        ['M'] = ["e", "f", "h", "i", "b", "c"],
        ['N'] = ["e", "f", "h", "b", "c"],
        ['V'] = ["e", "f", "i"],
        ['W'] = ["e", "f", "b", "c", "i"],
        ['X'] = ["h", "i", "g"],
        ['K'] = ["e", "f", "g", "h"],
    };

    private static readonly IReadOnlyDictionary<SegmentDisplayType, Dictionary<char, int>> CharToMaskByType;
    private static readonly IReadOnlyDictionary<SegmentDisplayType, Dictionary<int, char>> MaskToCharByType;

    static SegmentDisplayCodec()
    {
        var charToMask = new Dictionary<SegmentDisplayType, Dictionary<char, int>>();
        var maskToChar = new Dictionary<SegmentDisplayType, Dictionary<int, char>>();

        foreach (var type in (SegmentDisplayType[])Enum.GetValues(typeof(SegmentDisplayType)))
        {
            var labels = SegmentLabels[type];
            var glyphs = GlyphTableFor(type);

            var c2m = new Dictionary<char, int>();
            var m2c = new Dictionary<int, char>();
            foreach (var (character, litLabels) in glyphs)
            {
                var mask = MaskFromLabels(litLabels, labels);
                c2m[character] = mask;
                m2c.TryAdd(mask, character); // first listed char owns a shared mask on decode
            }

            charToMask[type] = c2m;
            maskToChar[type] = m2c;
        }

        CharToMaskByType = charToMask;
        MaskToCharByType = maskToChar;
    }

    /// <summary>The ordered segment labels of a display <paramref name="type"/> (bit <c>i</c> = label <c>i</c>).</summary>
    public static IReadOnlyList<string> LabelsFor(SegmentDisplayType type) => SegmentLabels[type];

    /// <summary>The number of segments a display <paramref name="type"/> has.</summary>
    public static int SegmentCount(SegmentDisplayType type) => SegmentLabels[type].Length;

    /// <summary>The full mask with every segment lit (the digit 8 / a fully-lit cell).</summary>
    public static int FullMask(SegmentDisplayType type) => (1 << SegmentCount(type)) - 1;

    /// <summary>
    /// Encodes <paramref name="text"/> to one <see cref="SegmentGlyph"/> per character. Characters with no
    /// glyph in the table yield a <see cref="Unknown"/> glyph with an empty mask, never silently dropped.
    /// When <paramref name="invert"/> is set (common-cathode "lit = 0"), every mask is bit-inverted.
    /// </summary>
    public IReadOnlyList<SegmentGlyph> Encode(string? text, SegmentDisplayType type, bool invert = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var table = CharToMaskByType[type];
        var glyphs = new List<SegmentGlyph>(text.Length);

        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                glyphs.Add(BuildGlyph(' ', type, 0, invert, isExact: true, distance: 0));
                continue;
            }

            var key = char.ToUpperInvariant(ch);
            if (table.TryGetValue(key, out var mask))
            {
                glyphs.Add(BuildGlyph(ch, type, mask, invert, isExact: true, distance: 0));
            }
            else
            {
                glyphs.Add(BuildGlyph(Unknown, type, 0, invert, isExact: false, distance: SegmentCount(type)));
            }
        }

        return glyphs;
    }

    /// <summary>
    /// Decodes a sequence of segment <paramref name="codes"/> (space-separated, one per character) back to
    /// text. Each code is parsed in <paramref name="notation"/> (or auto-detected when
    /// <paramref name="notation"/> is <see langword="null"/>), optionally read in reverse bit order
    /// (<paramref name="reverseBits"/>) and de-inverted for a common-cathode display (<paramref name="invert"/>).
    /// An unmatched mask is decoded by closest match (nearest standard character by Hamming distance) and the
    /// glyph carries <see cref="SegmentGlyph.IsExact"/> = <see langword="false"/>; an unparseable / out-of-range
    /// code yields an <see cref="Unknown"/> glyph rather than being dropped.
    /// </summary>
    public SegmentDecodeResult Decode(
        string? codes,
        SegmentDisplayType type,
        SegmentNotation? notation = null,
        bool reverseBits = false,
        bool invert = false)
    {
        if (string.IsNullOrWhiteSpace(codes))
        {
            return new SegmentDecodeResult(string.Empty, []);
        }

        var tokens = SplitCodes(codes);
        var glyphs = new List<SegmentGlyph>(tokens.Count);
        var builder = new StringBuilder(tokens.Count);

        foreach (var token in tokens)
        {
            if (!TryParseMask(token, type, notation, reverseBits, out var mask))
            {
                glyphs.Add(BuildGlyph(Unknown, type, 0, invert: false, isExact: false, distance: SegmentCount(type)));
                builder.Append(Unknown);
                continue;
            }

            // Undo a common-cathode inversion before looking the mask up in the (anode) table.
            var lookupMask = invert ? FullMask(type) & ~mask : mask;
            var glyph = DecodeMask(lookupMask, type);

            // Preserve the user's actual (possibly inverted) mask in the glyph for display/round-trip.
            glyphs.Add(glyph with
            {
                Mask = mask,
                Binary = ToBinary(mask, type),
                Decimal = mask,
                Labels = LabelsFromMask(mask, type),
                LitSegments = SegmentStates(mask, type),
            });
            builder.Append(glyph.Character);
        }

        return new SegmentDecodeResult(builder.ToString(), glyphs);
    }

    /// <summary>
    /// Resolves a single <paramref name="mask"/> to its character: the exact table entry when present,
    /// otherwise the closest standard character by segment Hamming distance (with <see cref="SegmentGlyph.IsExact"/>
    /// = <see langword="false"/> and the <see cref="SegmentGlyph.Distance"/>). An all-off mask decodes to a space.
    /// </summary>
    public SegmentGlyph DecodeMask(int mask, SegmentDisplayType type)
    {
        mask &= FullMask(type);

        if (mask == 0)
        {
            return BuildGlyph(' ', type, 0, invert: false, isExact: true, distance: 0);
        }

        if (MaskToCharByType[type].TryGetValue(mask, out var exact))
        {
            return BuildGlyph(exact, type, mask, invert: false, isExact: true, distance: 0);
        }

        var (character, distance) = ClosestMatches(mask, type, 1)[0];
        return BuildGlyph(character, type, mask, invert: false, isExact: false, distance);
    }

    /// <summary>
    /// Ranks the <paramref name="count"/> standard characters whose glyphs are nearest to
    /// <paramref name="mask"/> by segment Hamming distance (ties broken by character order) — the auto-solver
    /// for ambiguous puzzle glyphs. Returns fewer than <paramref name="count"/> only if the table is smaller.
    /// </summary>
    public IReadOnlyList<(char Character, int Distance)> ClosestMatches(int mask, SegmentDisplayType type, int count)
    {
        mask &= FullMask(type);
        return [.. CharToMaskByType[type]
            .Select(kvp => (kvp.Key, Distance: PopCount(kvp.Value ^ mask)))
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Key)
            .Take(count)];
    }

    /// <summary>Builds the clickable reference chart for a display <paramref name="type"/>: every standard
    /// glyph (digits then letters then symbols), ordered, for the UI to render and "type" by tapping.</summary>
    public IReadOnlyList<SegmentGlyph> GetChart(SegmentDisplayType type)
    {
        return [.. CharToMaskByType[type]
            .OrderBy(kvp => CharOrder(kvp.Key))
            .ThenBy(kvp => kvp.Key)
            .Select(kvp => BuildGlyph(kvp.Key, type, kvp.Value, invert: false, isExact: true, distance: 0))];
    }

    /// <summary>Formats a <paramref name="mask"/> in the requested <paramref name="notation"/>: a space-joined
    /// label list, a fixed-width binary string, or the decimal value.</summary>
    public static string Format(int mask, SegmentDisplayType type, SegmentNotation notation) => notation switch
    {
        SegmentNotation.Labels => string.Join(' ', LabelsFromMask(mask, type)),
        SegmentNotation.Binary => ToBinary(mask, type),
        _ => (mask & FullMask(type)).ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>The lit segment labels of a <paramref name="mask"/>, in canonical order.</summary>
    public static IReadOnlyList<string> LabelsFromMask(int mask, SegmentDisplayType type)
    {
        var labels = SegmentLabels[type];
        var result = new List<string>(labels.Length);
        for (var i = 0; i < labels.Length; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                result.Add(labels[i]);
            }
        }

        return result;
    }

    /// <summary>The per-segment lit/unlit state of a <paramref name="mask"/>, in label order, for rendering.</summary>
    public static IReadOnlyList<SegmentState> SegmentStates(int mask, SegmentDisplayType type)
    {
        var labels = SegmentLabels[type];
        var result = new List<SegmentState>(labels.Length);
        for (var i = 0; i < labels.Length; i++)
        {
            result.Add(new SegmentState(labels[i], (mask & (1 << i)) != 0));
        }

        return result;
    }

    /// <summary>Parses a single code <paramref name="token"/> to a mask. Honours an explicit
    /// <paramref name="notation"/> or auto-detects, applies the reverse-bit-order toggle, and rejects
    /// out-of-range values. Returns <see langword="false"/> for anything unparseable.</summary>
    public bool TryParseMask(
        string? token,
        SegmentDisplayType type,
        SegmentNotation? notation,
        bool reverseBits,
        out int mask)
    {
        mask = 0;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var trimmed = token.Trim();
        var resolved = notation ?? DetectNotation(trimmed, type);

        switch (resolved)
        {
            case SegmentNotation.Labels:
                if (!TryParseLabels(trimmed, type, out mask))
                {
                    return false;
                }

                break;

            case SegmentNotation.Binary:
                if (!TryParseBinary(trimmed, type, out mask))
                {
                    return false;
                }

                break;

            default:
                if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out mask)
                    || mask < 0 || mask > FullMask(type))
                {
                    return false;
                }

                break;
        }

        if (reverseBits)
        {
            mask = ReverseBits(mask, type);
        }

        return true;
    }

    /// <summary>Best-effort guess of which <see cref="SegmentNotation"/> a code <paramref name="token"/> is
    /// written in: any letter ⇒ labels, an all-0/1 string the width of the display ⇒ binary, else decimal.</summary>
    public static SegmentNotation DetectNotation(string token, SegmentDisplayType type)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
        {
            return SegmentNotation.Decimal;
        }

        // Any alphabetic character (a label like "g1") means the label-list notation.
        foreach (var ch in trimmed)
        {
            if (char.IsLetter(ch))
            {
                return SegmentNotation.Labels;
            }
        }

        // A bare run of 0/1 the exact width of the display reads as a binary mask; otherwise decimal.
        var width = SegmentCount(type);
        if (trimmed.Length == width)
        {
            var allBits = true;
            foreach (var ch in trimmed)
            {
                if (ch is not ('0' or '1'))
                {
                    allBits = false;
                    break;
                }
            }

            if (allBits)
            {
                return SegmentNotation.Binary;
            }
        }

        return SegmentNotation.Decimal;
    }

    private static bool TryParseLabels(string token, SegmentDisplayType type, out int mask)
    {
        mask = 0;
        var labels = SegmentLabels[type];
        // Labels may be separated by spaces, commas, plus or slashes (forgiving of how a puzzle wrote them).
        var parts = token.Split([' ', ',', '+', '/', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var part in parts)
        {
            var index = Array.FindIndex(labels, l => string.Equals(l, part, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return false; // an unknown label is an error, not a silently-ignored token.
            }

            mask |= 1 << index;
        }

        return true;
    }

    private static bool TryParseBinary(string token, SegmentDisplayType type, out int mask)
    {
        mask = 0;
        var width = SegmentCount(type);
        if (token.Length > width)
        {
            return false;
        }

        // Read left-to-right as MSB..LSB so it mirrors ToBinary (label 0 is the rightmost bit).
        foreach (var ch in token)
        {
            if (ch is not ('0' or '1'))
            {
                return false;
            }

            mask = (mask << 1) | (ch - '0');
        }

        return true;
    }

    private static string ToBinary(int mask, SegmentDisplayType type)
    {
        var width = SegmentCount(type);
        mask &= FullMask(type);
        return Convert.ToString(mask, 2).PadLeft(width, '0');
    }

    private static int ReverseBits(int mask, SegmentDisplayType type)
    {
        var width = SegmentCount(type);
        var result = 0;
        for (var i = 0; i < width; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                result |= 1 << (width - 1 - i);
            }
        }

        return result;
    }

    private static SegmentGlyph BuildGlyph(
        char character, SegmentDisplayType type, int mask, bool invert, bool isExact, int distance)
    {
        var effective = invert ? FullMask(type) & ~mask : mask;
        return new SegmentGlyph(
            character,
            type,
            effective,
            LabelsFromMask(effective, type),
            ToBinary(effective, type),
            effective,
            SegmentStates(effective, type),
            isExact,
            distance);
    }

    private static int MaskFromLabels(string[] litLabels, string[] orderedLabels)
    {
        var mask = 0;
        foreach (var label in litLabels)
        {
            var index = Array.IndexOf(orderedLabels, label);
            if (index >= 0)
            {
                mask |= 1 << index;
            }
        }

        return mask;
    }

    private static Dictionary<char, string[]> GlyphTableFor(SegmentDisplayType type) => type switch
    {
        SegmentDisplayType.SevenSegment => SevenSegmentGlyphs,
        SegmentDisplayType.NineSegment => MergeNineSegment(),
        SegmentDisplayType.SixteenSegment => SixteenSegmentGlyphs(),
        _ => FourteenSegmentGlyphs,
    };

    // 9-segment starts from the 7-segment table and overrides the few characters that gain from the diagonals.
    private static Dictionary<char, string[]> MergeNineSegment()
    {
        var merged = new Dictionary<char, string[]>(SevenSegmentGlyphs);
        foreach (var (character, labels) in NineSegmentExtraGlyphs)
        {
            merged[character] = labels;
        }

        return merged;
    }

    // 16-segment derives from the 14-segment table: the split top (a→a1+a2) and bottom (d→d1+d2) bars.
    private static Dictionary<char, string[]> SixteenSegmentGlyphs()
    {
        var result = new Dictionary<char, string[]>(FourteenSegmentGlyphs.Count);
        foreach (var (character, labels) in FourteenSegmentGlyphs)
        {
            var expanded = new List<string>(labels.Length + 2);
            foreach (var label in labels)
            {
                switch (label)
                {
                    case "a":
                        expanded.Add("a1");
                        expanded.Add("a2");
                        break;
                    case "d":
                        expanded.Add("d1");
                        expanded.Add("d2");
                        break;
                    default:
                        expanded.Add(label);
                        break;
                }
            }

            result[character] = [.. expanded];
        }

        return result;
    }

    /// <summary>The separator between codes (one code = one character). Commas/semicolons/slashes and any
    /// run of 2+ spaces all split codes; a single space stays <i>inside</i> a code so the label-list
    /// notation can space-separate its labels (e.g. <c>"a b c, b c"</c> is two codes).</summary>
    public static List<string> SplitCodes(string codes)
    {
        var result = new List<string>();
        foreach (var part in codes.Split([',', ';', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            // A run of two or more spaces also separates codes (e.g. pasted "63  6  6").
            foreach (var sub in part.Split(["  "], StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = sub.Trim();
                if (trimmed.Length != 0)
                {
                    result.Add(trimmed);
                }
            }
        }

        return result;
    }

    private static int CharOrder(char c) => c switch
    {
        >= '0' and <= '9' => 0,
        >= 'A' and <= 'Z' => 1,
        _ => 2,
    };

    private static int PopCount(int value)
    {
        var count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }
}

/// <summary>The outcome of decoding segment codes: the recovered <see cref="Text"/> and the per-code
/// <see cref="Glyphs"/> (each flags whether it was an exact or closest match) for rendering.</summary>
public sealed record SegmentDecodeResult(string Text, IReadOnlyList<SegmentGlyph> Glyphs);
