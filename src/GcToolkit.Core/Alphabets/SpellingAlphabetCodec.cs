using System.Text;

namespace GcToolkit.Core.Alphabets;

/// <summary>The outcome of decoding code words back to letters.</summary>
/// <param name="Text">The recovered letters (upper-case), with a single space between words and
/// <c>?</c> standing in for any unrecognised code word.</param>
/// <param name="UnknownWords">The distinct code words that matched no entry or alias.</param>
public sealed record SpellingAlphabetDecodeResult(string Text, IReadOnlyList<string> UnknownWords)
{
    public bool HasUnknown => UnknownWords.Count > 0;
}

/// <summary>
/// Bidirectional spelling-alphabet converter (cachesleuth.com parity). <see cref="Encode"/> turns text
/// into space-separated code words for a chosen <see cref="SpellingAlphabetVariant"/> (a double space
/// marks a word break); <see cref="Decode"/> turns code words back into letters, case-insensitively and
/// tolerant of comma/whitespace separators and the variant's registered aliases. The variant tables are
/// the single source of truth, keeping the converter pure and the view model thin.
/// </summary>
public sealed class SpellingAlphabetCodec
{
    /// <summary>Stands in for an unrecognised code word in the decoded text.</summary>
    public const char UnknownLetter = '?';

    public string Encode(string? text, SpellingAlphabetVariant variant)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        var atWordStart = true;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                // Collapse a run of whitespace into one word break; a double space separates words.
                if (!atWordStart && builder.Length > 0)
                {
                    builder.Append("  ");
                    atWordStart = true;
                }

                continue;
            }

            if (variant.TryGetWord(character, out var word))
            {
                if (!atWordStart)
                {
                    builder.Append(' ');
                }

                builder.Append(word);
                atWordStart = false;
            }

            // Characters with no code word are silently skipped (matching the reference's letters-only output).
        }

        return builder.ToString();
    }

    public SpellingAlphabetDecodeResult Decode(string? input, SpellingAlphabetVariant variant)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new(string.Empty, []);
        }

        StringBuilder letters = new();
        List<string> unknown = [];
        var runs = SplitWords(input).ToList();

        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            if (run.IsBreak)
            {
                letters.Append(' ');
                continue;
            }

            // Some code words are two tokens (e.g. Russian "Ivan kratkiy", "Myagkiy znak"). Prefer the
            // longer match: try this token joined with the next before falling back to the single token.
            if (index + 1 < runs.Count && !runs[index + 1].IsBreak
                && variant.TryGetLetter($"{run.Word} {runs[index + 1].Word}", out var joined))
            {
                letters.Append(joined);
                index++;
                continue;
            }

            if (variant.TryGetLetter(run.Word, out var letter))
            {
                letters.Append(letter);
            }
            else
            {
                letters.Append(UnknownLetter);
                if (!unknown.Contains(run.Word, StringComparer.OrdinalIgnoreCase))
                {
                    unknown.Add(run.Word);
                }
            }
        }

        return new(letters.ToString().Trim(), unknown);
    }

    /// <summary>
    /// Heuristic used by the auto-detect direction (beyond parity): the input reads as code words when
    /// most of its whitespace/comma-separated tokens resolve to letters in the chosen variant.
    /// </summary>
    public bool LooksLikeCodeWords(string? input, SpellingAlphabetVariant variant)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var total = 0;
        var matched = 0;
        foreach (var run in SplitWords(input))
        {
            if (run.IsBreak)
            {
                continue;
            }

            total++;
            if (variant.TryGetLetter(run.Word, out _))
            {
                matched++;
            }
        }

        return total > 0 && matched * 2 >= total;
    }

    private readonly record struct WordRun(string Word, bool IsBreak)
    {
        public static WordRun Break { get; } = new(string.Empty, true);
    }

    /// <summary>
    /// Tokenises decode input into words and word-breaks. Single spaces or commas separate code words;
    /// a run of two or more spaces (or a newline) is a word break that becomes a space in the result.
    /// </summary>
    private static IEnumerable<WordRun> SplitWords(string input)
    {
        var i = 0;
        while (i < input.Length)
        {
            var c = input[i];

            if (c == ',')
            {
                i++;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                var spaces = 0;
                var sawNewline = false;
                while (i < input.Length && char.IsWhiteSpace(input[i]))
                {
                    sawNewline |= input[i] is '\n' or '\r';
                    spaces++;
                    i++;
                }

                if (spaces >= 2 || sawNewline)
                {
                    yield return WordRun.Break;
                }

                continue;
            }

            var start = i;
            while (i < input.Length && input[i] is not (',' or ' ' or '\t' or '\n' or '\r'))
            {
                i++;
            }

            yield return new(input[start..i], IsBreak: false);
        }
    }
}
