using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Text;

/// <summary>The seven ways the text can be reordered (geocachingtoolbox.com Reverse-text parity).</summary>
public enum TextReverseMode
{
    /// <summary>Reverse the whole string, grapheme by grapheme.</summary>
    ReverseText,

    /// <summary>Reverse each line independently, leaving the line order intact.</summary>
    ReverseTextPerLine,

    /// <summary>Reverse the order of the words across the whole text.</summary>
    ReverseWordOrder,

    /// <summary>Reverse the order of the words within each line.</summary>
    ReverseWordOrderPerLine,

    /// <summary>Reverse the character order inside each word, keeping words in place.</summary>
    ReverseCharactersPerWord,

    /// <summary>Randomly shuffle the characters inside each word (seedable for determinism).</summary>
    RandomCharactersPerWord,

    /// <summary>Map each character to an inverted ("upside down") Unicode glyph.</summary>
    UpsideDown,
}

/// <summary>Optional, composable post-transform modifiers (can be combined as flags).</summary>
[Flags]
public enum TextReverseModifiers
{
    None = 0,

    /// <summary>Re-apply the original upper-case positions after the reorder.</summary>
    KeepUpperCaseLocation = 1 << 0,

    /// <summary>Keep the first and last character of each word fixed (typoglycemia; pairs with the random mode).</summary>
    KeepFirstAndLastCharacter = 1 << 1,

    /// <summary>Swap the case of every letter.</summary>
    SwapCase = 1 << 2,

    /// <summary>Force the whole result to upper case.</summary>
    ToUpperCase = 1 << 3,

    /// <summary>Force the whole result to lower case.</summary>
    ToLowerCase = 1 << 4,

    /// <summary>Drop characters the chosen transform cannot represent (notably the upside-down map).</summary>
    RemoveUnknownCharacters = 1 << 5,
}

/// <summary>Everything that controls a single transform.</summary>
/// <param name="Mode">Which reorder to perform.</param>
/// <param name="Modifiers">Post-transform tweaks, combinable as flags.</param>
/// <param name="ReverseUpsideDown">In <see cref="TextReverseMode.UpsideDown"/>, also reverse so a flipped device reads it normally.</param>
/// <param name="Seed">Seed for <see cref="TextReverseMode.RandomCharactersPerWord"/>; <see langword="null"/> = non-deterministic.</param>
public readonly record struct TextReverseOptions(
    TextReverseMode Mode,
    TextReverseModifiers Modifiers = TextReverseModifiers.None,
    bool ReverseUpsideDown = false,
    int? Seed = null);

/// <summary>
/// A pure, grapheme-aware text-reordering codec — the single source of truth for the Text reverse
/// tool. Enumerates Unicode text elements (via <see cref="StringInfo"/>) so multi-codepoint emoji and
/// combining diacritics reverse as one unit, then layers optional case/typoglycemia modifiers.
/// Beyond geocachingtoolbox.com parity it is fully grapheme-correct, seedable for reproducible
/// shuffles, and supports a clean upside-down de-flip (flip+reverse is its own inverse).
/// </summary>
public sealed class TextReverseCodec
{
    /// <summary>
    /// Applies <paramref name="options"/> to <paramref name="text"/>: reorders per the mode, then runs
    /// the requested modifiers. Returns <see cref="string.Empty"/> for null/empty input.
    /// </summary>
    public string Transform(string? text, TextReverseOptions options)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var random = options.Mode == TextReverseMode.RandomCharactersPerWord
            ? (options.Seed is int seed ? new Random(seed) : new Random())
            : null;

        var keepEnds = options.Modifiers.HasFlag(TextReverseModifiers.KeepFirstAndLastCharacter);

        var transformed = options.Mode switch
        {
            TextReverseMode.ReverseText => ReverseGraphemes(text),
            TextReverseMode.ReverseTextPerLine => PerLine(text, ReverseGraphemes),
            TextReverseMode.ReverseWordOrder => ReverseWordOrder(text),
            TextReverseMode.ReverseWordOrderPerLine => PerLine(text, ReverseWordOrder),
            TextReverseMode.ReverseCharactersPerWord => PerWord(text, ReverseGraphemes),
            TextReverseMode.RandomCharactersPerWord => PerWord(text, w => Shuffle(w, random!, keepEnds)),
            TextReverseMode.UpsideDown => UpsideDown(text, options),
            _ => text,
        };

        return ApplyModifiers(text, transformed, options);
    }

    // ---- Modes ----

    private static string ReverseGraphemes(string text)
    {
        var elements = EnumerateGraphemes(text);
        elements.Reverse();
        return string.Concat(elements);
    }

    /// <summary>Reverses the order of whitespace-delimited tokens, re-joining with single spaces.</summary>
    private static string ReverseWordOrder(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Array.Reverse(words);
        return string.Join(' ', words);
    }

    /// <summary>Transforms each word (run of non-whitespace) in place, leaving all whitespace untouched.</summary>
    private static string PerWord(string text, Func<string, string> wordTransform)
    {
        var builder = new StringBuilder(text.Length);
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            var atEnd = i == text.Length;
            if (atEnd || char.IsWhiteSpace(text[i]))
            {
                if (i > start)
                {
                    builder.Append(wordTransform(text[start..i]));
                }

                if (!atEnd)
                {
                    builder.Append(text[i]);
                }

                start = i + 1;
            }
        }

        return builder.ToString();
    }

    /// <summary>Applies <paramref name="lineTransform"/> to each line, preserving the original newline sequences.</summary>
    private static string PerLine(string text, Func<string, string> lineTransform)
    {
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var lineStart = i;
            while (i < text.Length && text[i] is not ('\n' or '\r'))
            {
                i++;
            }

            builder.Append(lineTransform(text[lineStart..i]));

            // Carry the newline run (\n, \r, or \r\n) through verbatim.
            if (i < text.Length)
            {
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    builder.Append("\r\n");
                    i += 2;
                }
                else
                {
                    builder.Append(text[i]);
                    i++;
                }
            }
        }

        return builder.ToString();
    }

    private static string Shuffle(string word, Random random, bool keepEnds)
    {
        var elements = EnumerateGraphemes(word);
        if (elements.Count < 2)
        {
            return word;
        }

        // Typoglycemia: fix the first and last grapheme, shuffle only the interior.
        var from = keepEnds ? 1 : 0;
        var to = keepEnds ? elements.Count - 1 : elements.Count;
        if (to - from < 2)
        {
            return word;
        }

        for (var i = to - 1; i > from; i--)
        {
            var j = random.Next(from, i + 1);
            (elements[i], elements[j]) = (elements[j], elements[i]);
        }

        return string.Concat(elements);
    }

    private string UpsideDown(string text, TextReverseOptions options)
    {
        var elements = EnumerateGraphemes(text);
        var removeUnknown = options.Modifiers.HasFlag(TextReverseModifiers.RemoveUnknownCharacters);
        var builder = new StringBuilder(text.Length);

        foreach (var element in elements)
        {
            if (UpsideDownMap.TryGetValue(element, out var flipped))
            {
                builder.Append(flipped);
            }
            else if (!removeUnknown)
            {
                builder.Append(element);
            }
        }

        var result = builder.ToString();

        // The "reverse text" sub-option flips reading order so a rotated device reads it the right way up.
        return options.ReverseUpsideDown ? ReverseGraphemes(result) : result;
    }

    // ---- Modifiers ----

    private static string ApplyModifiers(string original, string transformed, TextReverseOptions options)
    {
        var result = transformed;

        if (options.Modifiers.HasFlag(TextReverseModifiers.KeepUpperCaseLocation))
        {
            result = ReapplyUpperCaseLocations(original, result);
        }

        if (options.Modifiers.HasFlag(TextReverseModifiers.SwapCase))
        {
            result = SwapCase(result);
        }

        if (options.Modifiers.HasFlag(TextReverseModifiers.ToUpperCase))
        {
            result = result.ToUpperInvariant();
        }
        else if (options.Modifiers.HasFlag(TextReverseModifiers.ToLowerCase))
        {
            result = result.ToLowerInvariant();
        }

        return result;
    }

    /// <summary>Forces each output character to match the upper/lower state of the original at the same index.</summary>
    private static string ReapplyUpperCaseLocations(string original, string transformed)
    {
        var length = Math.Min(original.Length, transformed.Length);
        return string.Create(transformed.Length, (original, transformed, length), static (span, state) =>
        {
            var (src, dst, len) = state;
            for (var i = 0; i < dst.Length; i++)
            {
                var c = dst[i];
                if (i < len && char.IsLetter(src[i]))
                {
                    c = char.IsUpper(src[i]) ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
                }

                span[i] = c;
            }
        });
    }

    private static string SwapCase(string text)
        => string.Create(text.Length, text, static (span, src) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                var c = src[i];
                span[i] = char.IsUpper(c) ? char.ToLowerInvariant(c)
                    : char.IsLower(c) ? char.ToUpperInvariant(c)
                    : c;
            }
        });

    // ---- Helpers ----

    /// <summary>Splits a string into its Unicode grapheme clusters (text elements).</summary>
    private static List<string> EnumerateGraphemes(string text)
    {
        var result = new List<string>(text.Length);
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            result.Add((string)enumerator.Current);
        }

        return result;
    }

    /// <summary>
    /// Upside-down substitution map. Letters, digits and common punctuation rotate to their nearest
    /// 180°-rotated Unicode glyph (the conventional "flip text" mapping). The map is built to be its
    /// own inverse for letters/brackets, so flipping a flipped string yields the original lower-case
    /// text — a clean de-flip round-trip.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> UpsideDownMap = BuildUpsideDownMap();

    private static Dictionary<string, string> BuildUpsideDownMap()
    {
        // Forward pairs: base lower-case letter / digit / punctuation -> its rotated glyph.
        (string From, string To)[] pairs =
        [
            ("a", "ɐ"), ("b", "q"), ("c", "ɔ"), ("d", "p"), ("e", "ǝ"), ("f", "ɟ"),
            ("g", "ƃ"), ("h", "ɥ"), ("i", "ᴉ"), ("j", "ɾ"), ("k", "ʞ"), ("l", "l"),
            ("m", "ɯ"), ("n", "u"), ("o", "o"), ("p", "d"), ("q", "b"), ("r", "ɹ"),
            ("s", "s"), ("t", "ʇ"), ("u", "n"), ("v", "ʌ"), ("w", "ʍ"), ("x", "x"),
            ("y", "ʎ"), ("z", "z"),
            ("0", "0"), ("1", "Ɩ"), ("2", "ᄅ"), ("3", "Ɛ"), ("4", "ㄣ"), ("5", "ϛ"),
            ("6", "9"), ("7", "ㄥ"), ("8", "8"), ("9", "6"),
            (".", "˙"), (",", "'"), ("?", "¿"), ("!", "¡"),
            ("(", ")"), ("[", "]"), ("{", "}"), ("<", ">"),
            ("&", "⅋"), ("_", "‾"),
        ];

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (from, to) in pairs)
        {
            map[from] = to;

            // Reverse direction so flipping twice de-flips back to the original (round-trip).
            map.TryAdd(to, from);

            // Fold the upper-case letter onto the same flipped glyph (parity behavior).
            var upper = from.ToUpperInvariant();
            if (!string.Equals(upper, from, StringComparison.Ordinal))
            {
                map.TryAdd(upper, to);
            }
        }

        return map;
    }
}
