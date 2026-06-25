using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>How successive trigrams are joined in the encoded output.</summary>
public enum KennyCodeSeparator
{
    /// <summary>No separator — trigrams run together (<c>mmmmmp</c>).</summary>
    None,

    /// <summary>A single space between trigrams (<c>mmm mmp</c>).</summary>
    Space,

    /// <summary>A comma between trigrams (<c>mmm,mmp</c>).</summary>
    Comma,
}

/// <summary>Options controlling how <see cref="KennyCodeCodec.Encode(string?, KennyCodeEncodeOptions?)"/> renders output.</summary>
public sealed record KennyCodeEncodeOptions
{
    /// <summary>The separator placed between trigrams. Defaults to <see cref="KennyCodeSeparator.None"/>.</summary>
    public KennyCodeSeparator Separator { get; init; } = KennyCodeSeparator.None;

    /// <summary>When <see langword="true"/>, emit upper-case trigrams (<c>MPF</c>) instead of lower-case (<c>mpf</c>).</summary>
    public bool UpperCase { get; init; }

    /// <summary>
    /// When <see langword="true"/>, word boundaries (runs of non-letters) survive as a wider gap so the
    /// output stays readable word-by-word; otherwise every non-letter is dropped and only trigrams remain.
    /// </summary>
    public bool PreserveStructure { get; init; }
}

/// <summary>The outcome of a lenient decode: the recovered <see cref="Text"/> plus validity diagnostics.</summary>
/// <param name="Text">The decoded letters; an incomplete or impossible group becomes <c>?</c>.</param>
/// <param name="InvalidGroupCount">How many three-letter groups could not be mapped to a letter.</param>
/// <param name="IsLengthMultipleOfThree">Whether the count of usable m/p/f symbols was a multiple of three.</param>
public readonly record struct KennyCodeDecodeResult(string Text, int InvalidGroupCount, bool IsLengthMultipleOfThree)
{
    /// <summary><see langword="true"/> when any group was invalid or the signal length was not a multiple of three.</summary>
    public bool HasErrors => InvalidGroupCount > 0 || !IsLengthMultipleOfThree;
}

/// <summary>
/// A pure, stateless codec for "Kenny code" — the South Park <c>mmm/mpp/mpf</c> cipher: a fixed,
/// Bacon-style base-3 substitution over the digits <c>m &lt; p &lt; f</c>. Each letter A–Z maps to a
/// unique three-symbol trigram (letter index 0–25 in base 3, digit 0→<c>m</c>, 1→<c>p</c>, 2→<c>f</c>,
/// most-significant first). Encoding is case-insensitive; decoding is lenient — it keeps only the m/p/f
/// symbols, groups them in threes, and reports any incomplete or impossible group. Beyond
/// geocachingtoolbox.com parity this codec adds separator/case/structure options and decode diagnostics.
/// </summary>
public sealed class KennyCodeCodec
{
    /// <summary>The single character emitted for a group that cannot be mapped back to a letter.</summary>
    public const char InvalidMarker = '?';

    private const string Digits = "mpf"; // index 0/1/2 — the ordered base-3 alphabet
    private const int AlphabetSize = 26;
    private const int TrigramLength = 3;

    private static readonly string[] _trigrams = BuildTrigrams();
    private static readonly (char Letter, string Trigram)[] _referenceTable = BuildReferenceTable();

    /// <summary>The full 26-row substitution table (A–Z → trigram) for an on-page reference chart.</summary>
    public IReadOnlyList<(char Letter, string Trigram)> ReferenceTable => _referenceTable;

    /// <summary>
    /// Encodes <paramref name="text"/> to Kenny code. Each A–Z letter becomes its trigram; how trigrams are
    /// joined and whether word structure survives is controlled by <paramref name="options"/> (defaults:
    /// lower-case, no separator, structure dropped).
    /// </summary>
    /// <returns><see cref="string.Empty"/> for null/empty input; otherwise the encoded text.</returns>
    public string Encode(string? text, KennyCodeEncodeOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        options ??= new KennyCodeEncodeOptions();
        var separator = SeparatorString(options.Separator);

        var builder = new StringBuilder(text.Length * (TrigramLength + 1));
        var pendingWordGap = false; // a run of non-letters seen since the last emitted trigram
        var first = true;

        foreach (var c in text)
        {
            var index = LetterIndex(c);
            if (index < 0)
            {
                pendingWordGap = true;
                continue;
            }

            if (!first)
            {
                // Between words (when preserving structure) a single space marks the boundary; otherwise
                // just the chosen separator joins consecutive trigrams.
                if (options.PreserveStructure && pendingWordGap)
                {
                    builder.Append(' ');
                }
                else
                {
                    builder.Append(separator);
                }
            }

            builder.Append(Trigram(index, options.UpperCase));
            pendingWordGap = false;
            first = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Leniently decodes Kenny code: keeps only m/p/f symbols (case-insensitive), discards all separators and
    /// noise, groups the survivors in threes and maps each group back to a letter. An incomplete trailing
    /// group or an impossible group becomes <see cref="InvalidMarker"/>; the result reports how many groups
    /// were invalid and whether the signal length was a multiple of three.
    /// </summary>
    public KennyCodeDecodeResult Decode(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new KennyCodeDecodeResult(string.Empty, 0, true);
        }

        // Keep only the meaningful symbols, normalized to lower case.
        var signal = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            var lower = char.ToLowerInvariant(c);
            if (Digits.Contains(lower))
            {
                signal.Append(lower);
            }
        }

        if (signal.Length == 0)
        {
            return new KennyCodeDecodeResult(string.Empty, 0, true);
        }

        var isMultipleOfThree = signal.Length % TrigramLength == 0;
        var decoded = new StringBuilder(signal.Length / TrigramLength + 1);
        var invalidGroups = 0;

        for (var i = 0; i < signal.Length; i += TrigramLength)
        {
            if (i + TrigramLength > signal.Length)
            {
                // Incomplete trailing group.
                decoded.Append(InvalidMarker);
                invalidGroups++;
                break;
            }

            var letter = DecodeGroup(signal[i], signal[i + 1], signal[i + 2]);
            if (letter == InvalidMarker)
            {
                invalidGroups++;
            }

            decoded.Append(letter);
        }

        return new KennyCodeDecodeResult(decoded.ToString(), invalidGroups, isMultipleOfThree);
    }

    /// <summary>The 0-based alphabet index of <paramref name="c"/> (case-insensitive), or -1 if not A–Z.</summary>
    private static int LetterIndex(char c)
    {
        var lower = char.ToLowerInvariant(c);
        return lower is >= 'a' and <= 'z' ? lower - 'a' : -1;
    }

    private static string Trigram(int index, bool upperCase)
        => upperCase ? _trigrams[index].ToUpperInvariant() : _trigrams[index];

    private static char DecodeGroup(char a, char b, char c)
    {
        var index = Digits.IndexOf(a) * 9 + Digits.IndexOf(b) * 3 + Digits.IndexOf(c);
        return index is >= 0 and < AlphabetSize ? (char)('a' + index) : InvalidMarker;
    }

    private static string SeparatorString(KennyCodeSeparator separator) => separator switch
    {
        KennyCodeSeparator.Space => " ",
        KennyCodeSeparator.Comma => ",",
        _ => string.Empty,
    };

    private static string[] BuildTrigrams()
    {
        var trigrams = new string[AlphabetSize];
        for (var i = 0; i < AlphabetSize; i++)
        {
            // Three base-3 digits, most significant first.
            trigrams[i] = $"{Digits[i / 9]}{Digits[i / 3 % 3]}{Digits[i % 3]}";
        }

        return trigrams;
    }

    private static (char Letter, string Trigram)[] BuildReferenceTable()
    {
        var table = new (char, string)[AlphabetSize];
        for (var i = 0; i < AlphabetSize; i++)
        {
            table[i] = ((char)('A' + i), _trigrams[i]);
        }

        return table;
    }
}
