using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>One position in an encoded flag hoist: either a flag or a word gap.</summary>
public sealed record SignalFlagsToken(SignalFlagDescriptor? Flag)
{
    public static SignalFlagsToken Gap { get; } = new((SignalFlagDescriptor?)null);

    public bool IsGap => Flag is null;
}

/// <summary>The outcome of encoding text to signal flags.</summary>
public sealed record SignalFlagsEncodeResult(
    IReadOnlyList<SignalFlagsToken> Tokens,
    IReadOnlyList<char> SkippedCharacters)
{
    public bool HasSkipped => SkippedCharacters.Count > 0;
}

/// <summary>
/// Converts text to a sequence of ICS signal flags. Letters are case-insensitive, accented Latin
/// letters fold to their base letter (Č → C), whitespace runs become a single visual gap, and
/// characters with no flag are skipped and reported so the UI can show a gentle warning — matching
/// the geocachingtoolbox.com convention of mapping only a–z and 0–9.
/// </summary>
public sealed class SignalFlagsCodec
{
    public SignalFlagsEncodeResult Encode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new([], []);
        }

        List<SignalFlagsToken> tokens = [];
        List<char> skipped = [];
        var pendingGap = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingGap = tokens.Count > 0;
                continue;
            }

            if (TryMap(character, out var flag))
            {
                if (pendingGap)
                {
                    tokens.Add(SignalFlagsToken.Gap);
                    pendingGap = false;
                }

                tokens.Add(new(flag));
            }
            else if (!skipped.Contains(character))
            {
                skipped.Add(character);
            }
        }

        return new(tokens, skipped);
    }

    /// <summary>Rebuilds the (normalized, upper-case) text a token sequence represents.</summary>
    public static string ToText(IEnumerable<SignalFlagsToken> tokens)
    {
        StringBuilder builder = new();
        foreach (var token in tokens)
        {
            builder.Append(token.IsGap ? ' ' : token.Flag!.Symbol!.Value);
        }

        return builder.ToString();
    }

    private static bool TryMap(char character, out SignalFlagDescriptor? flag)
    {
        if (SignalFlagsAlphabet.TryGet(character, out flag))
        {
            return true;
        }

        // No direct flag: fold an accented Latin letter (e.g. Czech Č → C, Á → A) to its base letter.
        return FoldToBaseLetter(character) is char baseLetter && SignalFlagsAlphabet.TryGet(baseLetter, out flag);
    }

    private static char? FoldToBaseLetter(char character)
    {
        foreach (var candidate in character.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(candidate) != UnicodeCategory.NonSpacingMark)
            {
                return candidate;
            }
        }

        return null;
    }
}
