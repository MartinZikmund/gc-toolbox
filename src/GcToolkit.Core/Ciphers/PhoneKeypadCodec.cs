using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>How letters map onto telephone-keypad digits.</summary>
public enum PhoneKeypadMode
{
    /// <summary>Each letter collapses to the single digit of its key (A,B,C → 2). The classic "vanity
    /// number" form — compact but lossy, so decoding is ambiguous (see
    /// <see cref="PhoneKeypadCodec.DecodeVanityCandidates"/>).</summary>
    Vanity,

    /// <summary>Each letter is the key pressed by its position (A=2, B=22, C=222). The old SMS "multitap"
    /// form — verbose but fully reversible when groups are separated.</summary>
    Multitap,
}

/// <summary>One digit of an ambiguous vanity code and every letter it could stand for.</summary>
public readonly record struct VanityDigitCandidates(char Digit, string Letters);

/// <summary>
/// Bidirectional telephone-keypad (vanity / multitap) codec over the standard ITU&#160;E.161 layout
/// (2&#160;ABC … 9&#160;WXYZ; 0&#160;=&#160;space). It converts text to keypad digits in two conventions and back:
/// <see cref="PhoneKeypadMode.Multitap"/> round-trips exactly, while <see cref="PhoneKeypadMode.Vanity"/>
/// is lossy so decoding surfaces the candidate letters per digit instead of guessing words. This goes
/// beyond the geocachingtoolbox.com "vanity code" (which only does single-digit mapping and needs a word
/// dictionary to decode) by adding the reversible multitap convention and a fully offline, dictionary-free
/// decode. Accented input folds to its base letter so Czech text still encodes.
/// </summary>
public sealed class PhoneKeypadCodec
{
    /// <summary>Emitted for a multitap group that maps to no letter.</summary>
    public const char Unknown = '#';

    private const char SpaceDigit = '0';

    private static readonly (char Digit, string Letters)[] Layout =
    [
        ('2', "ABC"), ('3', "DEF"), ('4', "GHI"), ('5', "JKL"),
        ('6', "MNO"), ('7', "PQRS"), ('8', "TUV"), ('9', "WXYZ"),
    ];

    private static readonly Dictionary<char, string> DigitToLetters;
    private static readonly Dictionary<char, (char Digit, int Presses)> LetterToKey;

    static PhoneKeypadCodec()
    {
        DigitToLetters = Layout.ToDictionary(static e => e.Digit, static e => e.Letters);
        LetterToKey = new Dictionary<char, (char, int)>();
        foreach (var (digit, letters) in Layout)
        {
            for (var i = 0; i < letters.Length; i++)
            {
                LetterToKey[letters[i]] = (digit, i + 1);
            }
        }
    }

    /// <summary>The letters printed on <paramref name="digit"/>'s key, or empty for keys without letters.</summary>
    public string LettersFor(char digit) => DigitToLetters.GetValueOrDefault(digit, string.Empty);

    /// <summary>The keypad layout (digit → letters) for rendering the on-screen keys or a legend.</summary>
    public IReadOnlyList<(char Digit, string Letters)> KeypadLayout => Layout;

    /// <summary>Encodes <paramref name="text"/> to keypad digits in the chosen <paramref name="mode"/>.
    /// Spaces become <c>0</c>; characters that aren't letters pass through unchanged.</summary>
    public string Encode(string? text, PhoneKeypadMode mode)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Vanity concatenates one digit per letter; multitap separates the repeated-press groups with spaces.
        return mode == PhoneKeypadMode.Multitap
            ? string.Join(' ', EncodeTokens(text, multitap: true))
            : string.Concat(EncodeTokens(text, multitap: false));
    }

    /// <summary>Per-character keypad tokens: a space → <c>"0"</c>, a letter → its key digit (repeated by
    /// position when <paramref name="multitap"/>), any other character passes through unchanged.</summary>
    private static IEnumerable<string> EncodeTokens(string text, bool multitap)
    {
        foreach (var raw in text)
        {
            if (raw == ' ')
            {
                yield return SpaceDigit.ToString();
                continue;
            }

            var upper = char.ToUpperInvariant(raw);
            var letter = LetterToKey.ContainsKey(upper) ? upper : FoldToBaseLetter(upper) ?? upper;

            yield return LetterToKey.TryGetValue(letter, out var key)
                ? new string(key.Digit, multitap ? key.Presses : 1)
                : raw.ToString();
        }
    }

    /// <summary>Decodes a space-separated multitap string (e.g. <c>"44 33 555 555 666"</c>) back to text.
    /// A <c>0</c> group becomes a space; an unrecognized group becomes <see cref="Unknown"/>.</summary>
    public string DecodeMultitap(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var groups = code.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder(groups.Length);

        foreach (var group in groups)
        {
            builder.Append(DecodeGroup(group));
        }

        return builder.ToString();
    }

    private static char DecodeGroup(string group)
    {
        var key = group[0];
        var uniform = group.All(ch => ch == key);

        if (uniform && key == SpaceDigit)
        {
            return ' ';
        }

        if (uniform && DigitToLetters.TryGetValue(key, out var letters))
        {
            return letters[(group.Length - 1) % letters.Length];
        }

        return Unknown;
    }

    /// <summary>Decodes a vanity code to its per-digit possibilities. Because one digit covers several
    /// letters the result is the candidate set for each digit (e.g. <c>2 → ABC</c>) rather than a single
    /// word — a fully offline alternative to a dictionary lookup. Whitespace is ignored; <c>0</c>/<c>1</c>
    /// are treated as spaces.</summary>
    public IReadOnlyList<VanityDigitCandidates> DecodeVanityCandidates(string? digits)
    {
        if (string.IsNullOrWhiteSpace(digits))
        {
            return [];
        }

        var result = new List<VanityDigitCandidates>();
        foreach (var ch in digits)
        {
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            if (ch is '0' or '1')
            {
                result.Add(new VanityDigitCandidates(ch, " "));
            }
            else if (DigitToLetters.TryGetValue(ch, out var letters))
            {
                result.Add(new VanityDigitCandidates(ch, letters));
            }
        }

        return result;
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
