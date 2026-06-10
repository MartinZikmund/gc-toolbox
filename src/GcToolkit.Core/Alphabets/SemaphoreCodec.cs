using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>Whether letter figures currently read as letters or as digits.</summary>
public enum SemaphoreMode
{
    Letters,
    Numbers,
}

/// <summary>One encoded figure plus its display character ("G", "7"; empty for the special signs).</summary>
public sealed record SemaphoreToken(SemaphoreFigure Figure, string Display);

/// <summary>Encode output: the figure sequence plus the distinct characters that had no figure.</summary>
public sealed record SemaphoreEncodeResult(IReadOnlyList<SemaphoreToken> Tokens, string UnknownCharacters)
{
    public bool HasUnknown => UnknownCharacters.Length > 0;
}

/// <summary>
/// Bidirectional text ⇄ flag-semaphore converter with the exact mode semantics of the reference
/// chart (verified live on geocachingtoolbox.com): the Numbers sign precedes a digit run, digits
/// reuse their letter figures (A=1 … I=9, K=0), the Letters sign returns to letters, and the Space
/// sign does <b>not</b> reset number mode. Accented letters fold to their base letter (Č → C);
/// anything else is skipped and reported via <see cref="SemaphoreEncodeResult.UnknownCharacters"/>.
/// </summary>
public sealed class SemaphoreCodec
{
    /// <summary>Encodes text into the semaphore figure sequence.</summary>
    public SemaphoreEncodeResult Encode(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new SemaphoreEncodeResult([], string.Empty);
        }

        List<SemaphoreToken> tokens = [];
        StringBuilder unknown = new();
        var mode = SemaphoreMode.Letters;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                tokens.Add(new SemaphoreToken(SemaphoreAlphabet.Space, string.Empty));
            }
            else if (SemaphoreAlphabet.DigitToLetter.TryGetValue(c, out var digitLetter))
            {
                if (mode != SemaphoreMode.Numbers)
                {
                    tokens.Add(new SemaphoreToken(SemaphoreAlphabet.NumbersSign, string.Empty));
                    mode = SemaphoreMode.Numbers;
                }

                tokens.Add(new SemaphoreToken(SemaphoreAlphabet.Letters[digitLetter], c.ToString()));
            }
            else if (TryMapLetter(c) is char letter)
            {
                if (mode != SemaphoreMode.Letters)
                {
                    tokens.Add(new SemaphoreToken(SemaphoreAlphabet.LettersSign, string.Empty));
                    mode = SemaphoreMode.Letters;
                }

                tokens.Add(new SemaphoreToken(SemaphoreAlphabet.Letters[letter], letter.ToString()));
            }
            else if (!unknown.ToString().Contains(c))
            {
                unknown.Append(c);
            }
        }

        return new SemaphoreEncodeResult(tokens, unknown.ToString());
    }

    /// <summary>Decodes a figure sequence back to text, tracking Letters/Numbers mode statefully.
    /// A figure with no digit equivalent read in number mode falls back to its letter.</summary>
    public string Decode(IEnumerable<SemaphoreFigure> figures)
    {
        StringBuilder builder = new();
        var mode = SemaphoreMode.Letters;

        foreach (var figure in figures)
        {
            switch (figure.Kind)
            {
                case SemaphoreFigureKind.Space:
                    builder.Append(' ');
                    break;
                case SemaphoreFigureKind.NumbersSign:
                    mode = SemaphoreMode.Numbers;
                    break;
                case SemaphoreFigureKind.LettersSign:
                    mode = SemaphoreMode.Letters;
                    break;
                default:
                    builder.Append(mode == SemaphoreMode.Numbers && figure.Digit is char digit
                        ? digit
                        : char.ToLowerInvariant(figure.Letter));
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>The mode in effect after encoding <paramref name="text"/> — what the next appended
    /// character would be read as. Spaces and unknown characters leave the mode untouched.</summary>
    public SemaphoreMode GetFinalMode(string? text)
    {
        var mode = SemaphoreMode.Letters;
        if (string.IsNullOrEmpty(text))
        {
            return mode;
        }

        foreach (var c in text)
        {
            if (SemaphoreAlphabet.DigitToLetter.ContainsKey(c))
            {
                mode = SemaphoreMode.Numbers;
            }
            else if (TryMapLetter(c) is not null)
            {
                mode = SemaphoreMode.Letters;
            }
        }

        return mode;
    }

    private static char? TryMapLetter(char c)
    {
        var upper = char.ToUpperInvariant(c);
        if (SemaphoreAlphabet.Letters.ContainsKey(upper))
        {
            return upper;
        }

        // Fold an accented Latin letter (Č→C, Á→A, …) to its base letter so Czech text still signals.
        foreach (var ch in upper.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                var folded = char.ToUpperInvariant(ch);
                return SemaphoreAlphabet.Letters.ContainsKey(folded) ? folded : null;
            }
        }

        return null;
    }
}
