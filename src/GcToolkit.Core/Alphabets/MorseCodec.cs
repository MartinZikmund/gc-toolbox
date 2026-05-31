using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// Bidirectional Morse code translator covering the full ITU-R M.1677-1 set (letters, digits,
/// punctuation) plus the accented Latin letters that have their own ITU code, so coverage exceeds the
/// geocachingtoolbox.com converter (which omits accents and most punctuation).
/// </summary>
/// <remarks>
/// Conventions (compatible with geocachingtoolbox and the major translators):
/// <list type="bullet">
///   <item><description>Element gap = a single space; word gap = <c>" / "</c> on encode, and either a
///   <c>/</c> or two-or-more spaces on decode (so codes copied from geocachingtoolbox decode too).</description></item>
///   <item><description>Unknown input maps to <see cref="UnknownToken"/> (<c>#</c>), matching their behaviour.</description></item>
///   <item><description>Decoding is case-insensitive in effect and always yields upper-case text.</description></item>
/// </list>
/// Accented input is handled in two tiers: a letter with its own ITU code (<c>Ä É Ö Ü Ñ À Å Ç È</c>) encodes
/// to that code; any other accented Latin letter (e.g. the Czech <c>Á Č Ď Ě Í Ň Ó Ř Š Ť Ú Ů Ý Ž</c>) folds to
/// its base letter so it encodes to real Morse rather than <c>#</c>. Because several letters legitimately
/// share one ITU code (e.g. <c>À</c> and <c>Å</c> are both <c>.--.-</c>), decoding a shared code is
/// best-effort and yields one canonical letter — Morse itself does not distinguish them.
/// </remarks>
public sealed class MorseCodec
{
    /// <summary>Placeholder emitted for a character/code that has no Morse mapping.</summary>
    public const string UnknownToken = "#";

    private const string WordSeparator = " / ";

    private static readonly Regex WordSplit = new(@"\s*/\s*|\s{2,}");

    private static readonly IReadOnlyDictionary<char, string> EncodeMap;
    private static readonly IReadOnlyDictionary<string, char> DecodeMap;

    static MorseCodec()
    {
        // Primary set — ITU letters, digits and punctuation. These win on decode.
        (char Character, string Code)[] primary =
        [
            ('A', ".-"), ('B', "-..."), ('C', "-.-."), ('D', "-.."), ('E', "."), ('F', "..-."),
            ('G', "--."), ('H', "...."), ('I', ".."), ('J', ".---"), ('K', "-.-"), ('L', ".-.."),
            ('M', "--"), ('N', "-."), ('O', "---"), ('P', ".--."), ('Q', "--.-"), ('R', ".-."),
            ('S', "..."), ('T', "-"), ('U', "..-"), ('V', "...-"), ('W', ".--"), ('X', "-..-"),
            ('Y', "-.--"), ('Z', "--.."),
            ('0', "-----"), ('1', ".----"), ('2', "..---"), ('3', "...--"), ('4', "....-"),
            ('5', "....."), ('6', "-...."), ('7', "--..."), ('8', "---.."), ('9', "----."),
            ('.', ".-.-.-"), (',', "--..--"), ('?', "..--.."), ('\'', ".----."), ('!', "-.-.--"),
            ('/', "-..-."), ('(', "-.--."), (')', "-.--.-"), ('&', ".-..."), (':', "---..."),
            (';', "-.-.-."), ('=', "-...-"), ('+', ".-.-."), ('-', "-....-"), ('_', "..--.-"),
            ('"', ".-..-."), ('$', "...-..-"), ('@', ".--.-."),
        ];

        // Accented letters with a genuine ITU code. Several share a code with a sibling (À/Å); the first
        // listed wins the decode slot, so a shared code decodes to one canonical letter (see remarks).
        (char Character, string Code)[] accented =
        [
            ('À', ".--.-"), ('Å', ".--.-"), ('Ä', ".-.-"), ('Ç', "-.-.."), ('È', ".-..-"),
            ('É', "..-.."), ('Ñ', "--.--"), ('Ö', "---."), ('Ü', "..--"),
        ];

        var encode = new Dictionary<char, string>();
        var decode = new Dictionary<string, char>(StringComparer.Ordinal);

        foreach (var (character, code) in primary)
        {
            encode[character] = code;
            decode[code] = character;
        }

        foreach (var (character, code) in accented)
        {
            if (!encode.ContainsKey(character))
            {
                encode[character] = code;
            }

            if (!decode.ContainsKey(code))
            {
                decode[code] = character;
            }
        }

        EncodeMap = encode;
        DecodeMap = decode;
    }

    /// <summary>Encodes <paramref name="text"/> to Morse. Letters are space-separated; words are
    /// separated by <c>" / "</c>; unmapped characters become <see cref="UnknownToken"/>.</summary>
    public string Encode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder();

        for (var w = 0; w < words.Length; w++)
        {
            if (w > 0)
            {
                builder.Append(WordSeparator);
            }

            var word = words[w];
            for (var i = 0; i < word.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(EncodeCharacter(char.ToUpperInvariant(word[i])) ?? UnknownToken);
            }
        }

        return builder.ToString();
    }

    /// <summary>Decodes a Morse string to upper-case text. Accepts <c>/</c> or runs of two-or-more
    /// spaces as the word gap; unmapped tokens become <see cref="UnknownToken"/>.</summary>
    public string Decode(string? morse)
    {
        if (string.IsNullOrWhiteSpace(morse))
        {
            return string.Empty;
        }

        var words = WordSplit.Split(morse.Trim());
        var builder = new StringBuilder();
        var firstWord = true;

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                continue;
            }

            if (!firstWord)
            {
                builder.Append(' ');
            }

            firstWord = false;

            var tokens = word.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                builder.Append(DecodeMap.TryGetValue(token, out var character) ? character : UnknownToken);
            }
        }

        return builder.ToString();
    }

    private static string? EncodeCharacter(char upper)
    {
        if (EncodeMap.TryGetValue(upper, out var code))
        {
            return code;
        }

        // No direct code: fold an accented Latin letter (e.g. Czech Č→C, Ř→R, Á→A) to its base letter so
        // it still encodes to real Morse instead of an unknown placeholder.
        var folded = FoldToBaseLetter(upper);
        return folded is char baseLetter && EncodeMap.TryGetValue(baseLetter, out code) ? code : null;
    }

    private static char? FoldToBaseLetter(char c)
    {
        foreach (var ch in c.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                return char.ToUpperInvariant(ch);
            }
        }

        return null;
    }
}
