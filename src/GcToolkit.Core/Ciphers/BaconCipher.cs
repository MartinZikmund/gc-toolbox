using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// Which 26-letter → 5-bit mapping the Bacon (Baconian biliteral) cipher uses.
/// </summary>
public enum BaconVersion
{
    /// <summary>
    /// The classic 24-letter table where I/J and U/V each share a code. Encoding maps J→I's code and
    /// V→U's code; decoding yields I and U for those shared codes (the merge is lossy by design).
    /// </summary>
    Standard,

    /// <summary>
    /// The modern "distinct values" variant: every letter A–Z gets its own 5-bit code equal to its
    /// 0-based alphabet index (A=0=AAAAA … Z=25=BBAAB), so encoding/decoding is fully reversible.
    /// </summary>
    Distinct,
}

/// <summary>
/// Options for a Bacon transform: which <see cref="Version"/>, the two symbols that stand for bit 0
/// and bit 1 (default <c>A</c>/<c>B</c>), and whether to swap their roles.
/// </summary>
/// <param name="Version">The mapping table to use.</param>
/// <param name="FirstSymbol">The symbol for bit 0 (default <c>A</c>).</param>
/// <param name="SecondSymbol">The symbol for bit 1 (default <c>B</c>).</param>
/// <param name="SwapSymbols">When <see langword="true"/>, the two symbol roles are inverted before encode/decode.</param>
public readonly record struct BaconOptions(
    BaconVersion Version,
    char FirstSymbol = 'A',
    char SecondSymbol = 'B',
    bool SwapSymbols = false);

/// <summary>One row of the on-page reference chart: a <see cref="Letter"/> and the <see cref="Code"/> it maps to.</summary>
public readonly record struct BaconTableRow(char Letter, string Code)
{
    /// <summary>Screen-reader label for the tile, e.g. <c>"A AAAAA"</c>.</summary>
    public string AutomationName => $"{Letter} {Code}";
}

/// <summary>
/// A pure, stateless Bacon (Baconian biliteral) cipher — the single source of truth for the transform.
/// Each letter becomes a 5-symbol group of two symbols (classically A/B). Supports two mappings:
/// the classic 24-letter <see cref="BaconVersion.Standard"/> table (I=J, U=V) and the reversible
/// <see cref="BaconVersion.Distinct"/> table (each letter's binary index). The two symbols are
/// configurable and their roles can be swapped. Decoding is lenient: it keeps only the two active
/// symbols, ignores all other characters, auto-chunks into groups of five, and emits <c>?</c> for any
/// group that is not a whole multiple of five or is absent from the table.
/// </summary>
public sealed class BaconCipher
{
    /// <summary>Number of symbols in a single Bacon group.</summary>
    public const int GroupLength = 5;

    /// <summary>Marker emitted for an unknown / malformed group on decode.</summary>
    public const char UnknownMarker = '?';

    private const char DefaultFirst = 'A';
    private const char DefaultSecond = 'B';

    // Classic 24-letter table, indexed 'A'..'Z'. I/J share ABAAA; U/V share BAABB.
    private static readonly string[] StandardCodes =
    [
        "AAAAA", // A
        "AAAAB", // B
        "AAABA", // C
        "AAABB", // D
        "AABAA", // E
        "AABAB", // F
        "AABBA", // G
        "AABBB", // H
        "ABAAA", // I
        "ABAAA", // J (shares I)
        "ABAAB", // K
        "ABABA", // L
        "ABABB", // M
        "ABBAA", // N
        "ABBAB", // O
        "ABBBA", // P
        "ABBBB", // Q
        "BAAAA", // R
        "BAAAB", // S
        "BAABA", // T
        "BAABB", // U
        "BAABB", // V (shares U)
        "BABAA", // W
        "BABAB", // X
        "BABBA", // Y
        "BABBB", // Z
    ];

    /// <summary>
    /// Encodes <paramref name="text"/> with the default A/B symbols and the given <paramref name="version"/>;
    /// pass <paramref name="swapSymbols"/> to invert the two symbol roles.
    /// </summary>
    public string Encode(string? text, BaconVersion version, bool swapSymbols = false)
        => Encode(text, new BaconOptions(version, SwapSymbols: swapSymbols));

    /// <summary>
    /// Encodes the letters of <paramref name="text"/> into space-separated 5-symbol groups. Case is
    /// ignored; every non-letter is dropped (each tool group corresponds to exactly one letter).
    /// </summary>
    public string Encode(string? text, BaconOptions options)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var (zero, one) = ActiveSymbols(options);
        var codes = SelectCodes(options.Version);

        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            var index = LetterIndex(ch);
            if (index < 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            AppendGroup(builder, codes[index], zero, one);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Decodes <paramref name="text"/> with the default A/B symbols and the given <paramref name="version"/>;
    /// pass <paramref name="swapSymbols"/> to invert the two symbol roles.
    /// </summary>
    public string Decode(string? text, BaconVersion version, bool swapSymbols = false)
        => Decode(text, new BaconOptions(version, SwapSymbols: swapSymbols));

    /// <summary>
    /// Leniently decodes <paramref name="text"/>: keeps only the two active symbols, ignores all other
    /// characters, auto-chunks the result into groups of five, and maps each group back to a letter.
    /// A trailing partial group (length not a multiple of five) and any unrecognized group both emit
    /// <see cref="UnknownMarker"/>.
    /// </summary>
    public string Decode(string? text, BaconOptions options)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var (zero, one) = ActiveSymbols(options);
        var zeroLower = char.ToLowerInvariant(zero);
        var oneLower = char.ToLowerInvariant(one);

        // Keep only the two active signal symbols (case-insensitively); everything else is noise.
        var bits = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var lower = char.ToLowerInvariant(ch);
            if (lower == zeroLower)
            {
                bits.Append('A');
            }
            else if (lower == oneLower)
            {
                bits.Append('B');
            }
        }

        if (bits.Length == 0)
        {
            return string.Empty;
        }

        var codes = SelectCodes(options.Version);
        var result = new StringBuilder(bits.Length / GroupLength + 1);
        for (var i = 0; i < bits.Length; i += GroupLength)
        {
            if (i + GroupLength > bits.Length)
            {
                // Trailing partial group can't be a full letter.
                result.Append(UnknownMarker);
                break;
            }

            var group = bits.ToString(i, GroupLength);
            result.Append(LetterForCode(group, codes));
        }

        return result.ToString();
    }

    /// <summary>The full A→Z reference chart for <paramref name="version"/> in the default A/B symbols.</summary>
    public IReadOnlyList<BaconTableRow> GetReferenceTable(BaconVersion version)
        => GetReferenceTable(new BaconOptions(version));

    /// <summary>The full A→Z reference chart for <paramref name="options"/>, rendered in its active symbols.</summary>
    public IReadOnlyList<BaconTableRow> GetReferenceTable(BaconOptions options)
    {
        var (zero, one) = ActiveSymbols(options);
        var codes = SelectCodes(options.Version);
        var rows = new List<BaconTableRow>(codes.Length);
        var builder = new StringBuilder(GroupLength);
        for (var i = 0; i < codes.Length; i++)
        {
            builder.Clear();
            AppendGroup(builder, codes[i], zero, one);
            rows.Add(new BaconTableRow((char)('A' + i), builder.ToString()));
        }

        return rows;
    }

    private static string[] SelectCodes(BaconVersion version) => version switch
    {
        BaconVersion.Distinct => DistinctCodes,
        _ => StandardCodes,
    };

    private static readonly string[] DistinctCodes = BuildDistinctCodes();

    private static string[] BuildDistinctCodes()
    {
        var codes = new string[26];
        for (var i = 0; i < codes.Length; i++)
        {
            var chars = new char[GroupLength];
            for (var bit = 0; bit < GroupLength; bit++)
            {
                // MSB-first: bit (GroupLength-1) is the high bit.
                var isSet = (i >> (GroupLength - 1 - bit) & 1) == 1;
                chars[bit] = isSet ? 'B' : 'A';
            }

            codes[i] = new string(chars);
        }

        return codes;
    }

    /// <summary>The 0-based index of a Latin letter, or −1 for anything else.</summary>
    private static int LetterIndex(char c)
    {
        if (c is >= 'A' and <= 'Z')
        {
            return c - 'A';
        }

        if (c is >= 'a' and <= 'z')
        {
            return c - 'a';
        }

        return -1;
    }

    /// <summary>Resolves the (bit-0, bit-1) symbols, honoring a swap.</summary>
    private static (char Zero, char One) ActiveSymbols(BaconOptions options)
    {
        var zero = options.FirstSymbol;
        var one = options.SecondSymbol;
        return options.SwapSymbols ? (one, zero) : (zero, one);
    }

    private static void AppendGroup(StringBuilder builder, string code, char zero, char one)
    {
        foreach (var symbol in code)
        {
            builder.Append(symbol == 'A' ? zero : one);
        }
    }

    /// <summary>Maps a canonical A/B group back to its letter, or <see cref="UnknownMarker"/> if unassigned.</summary>
    private static char LetterForCode(string group, string[] codes)
    {
        // First match wins, so Standard's shared codes resolve to I and U (the lower index).
        for (var i = 0; i < codes.Length; i++)
        {
            if (codes[i] == group)
            {
                return (char)('A' + i);
            }
        }

        return UnknownMarker;
    }
}
