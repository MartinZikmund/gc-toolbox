using System.Text;

namespace GcToolkit.Core.Text;

/// <summary>
/// Options controlling the <see cref="AlphabetNumbers"/> conversion in both directions.
/// </summary>
public sealed record AlphabetNumberOptions
{
    /// <summary>The shared default: a single space between numbers.</summary>
    public static AlphabetNumberOptions Default { get; } = new();

    /// <summary>
    /// The string placed between (letters→numbers) or split on (numbers→letters) adjacent numbers of
    /// the same word. Defaults to a single space. Leading/trailing whitespace the user types around a
    /// non-space separator is tolerated when decoding.
    /// </summary>
    public string Separator { get; init; } = " ";

    /// <summary>The numbering scheme. Defaults to <see cref="AlphabetMethod.A1Z26"/> (A=1 … Z=26).</summary>
    public AlphabetMethod Method { get; init; } = AlphabetMethod.A1Z26;

    /// <summary>
    /// When decoding, map a value outside the method's range back into range (e.g. 27→A) instead of
    /// treating it as unknown. Modulo is taken over the active method's range, so 31→A under a 30-letter
    /// German method.
    /// </summary>
    public bool WrapModulo { get; init; }

    /// <summary>When decoding, the output letter case. <see langword="true"/> (default) = upper-case.</summary>
    public bool UpperCase { get; init; } = true;

    /// <summary>
    /// When encoding, keep non-letter characters (digits, punctuation, symbols) verbatim in the output
    /// instead of dropping them. Word boundaries are always preserved either way.
    /// </summary>
    public bool KeepNonLetters { get; init; }

    /// <summary>
    /// When decoding, the string substituted for a token that does not map to a letter (out of range
    /// and not wrapped, or non-numeric). Empty drops the token. Ignored when
    /// <see cref="KeepOriginalUnknown"/> is set. Defaults to <see cref="AlphabetNumbers.UnknownMarker"/>.
    /// </summary>
    public string UnknownReplacement { get; init; } = AlphabetNumbers.UnknownMarker;

    /// <summary>
    /// When decoding, emit the original token text verbatim for an unmapped token instead of
    /// <see cref="UnknownReplacement"/> (geocachingtoolbox.com's "&lt;Original&gt;" behavior).
    /// </summary>
    public bool KeepOriginalUnknown { get; init; }
}

/// <summary>
/// Bidirectional numbers ↔ letters codec — the single source of truth for the transform. The active
/// <see cref="AlphabetMethod"/> defines the character ↔ value mapping (A=1…Z=26 and its 0-based,
/// reversed, German and Nordic variants); encoding joins the numbers of a word with a configurable
/// separator, decoding reverses it. Word boundaries survive a round trip.
/// Beyond geocachingtoolbox.com parity (which only decodes numbers→letters with a space separator),
/// this codec is bidirectional, supports every method, takes any separator, can keep or strip
/// non-letters when encoding, offers a configurable output case, wraps out-of-range values into the
/// method's range, and lets unmapped tokens be kept verbatim or replaced with any string.
/// </summary>
public sealed class AlphabetNumbers
{
    /// <summary>Number of letters in the Latin alphabet.</summary>
    public const int AlphabetSize = 26;

    /// <summary>Default substitution for a token that is not a valid letter index (and not wrapped).</summary>
    public const string UnknownMarker = "#";

    /// <summary>
    /// Encodes letters to their A1Z26 numbers. Letters map case-insensitively to 1–26, the numbers of a
    /// word are joined with <see cref="AlphabetNumberOptions.Separator"/>, and spaces in
    /// <paramref name="text"/> are preserved as word boundaries. Non-letters are dropped unless
    /// <see cref="AlphabetNumberOptions.KeepNonLetters"/> is set, in which case they pass through verbatim.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for null/empty input.</returns>
    public string LettersToNumbers(string? text, AlphabetNumberOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        options ??= AlphabetNumberOptions.Default;
        var method = AlphabetMethods.Get(options.Method);
        return options.KeepNonLetters
            ? EncodeKeepingNonLetters(text, options.Separator, method)
            : EncodeStrippingNonLetters(text, options.Separator, method);
    }

    /// <summary>
    /// Decodes A1Z26 numbers back to letters. Each numeric token maps 1→A … 26→Z (case per
    /// <see cref="AlphabetNumberOptions.UpperCase"/>); word boundaries become single spaces. A token
    /// outside 1–26 (or a non-numeric token) is wrapped modulo 26 when
    /// <see cref="AlphabetNumberOptions.WrapModulo"/> is set, otherwise emitted as
    /// <see cref="UnknownMarker"/>.
    /// </summary>
    /// <returns><see cref="string.Empty"/> for null/blank input.</returns>
    public string NumbersToLetters(string? text, AlphabetNumberOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        options ??= AlphabetNumberOptions.Default;
        var method = AlphabetMethods.Get(options.Method);
        var words = SplitIntoWords(text, options.Separator);

        var result = new StringBuilder();
        for (var w = 0; w < words.Count; w++)
        {
            if (w > 0)
            {
                result.Append(' ');
            }

            foreach (var token in words[w])
            {
                result.Append(DecodeToken(token, options, method));
            }
        }

        return result.ToString();
    }

    private static string EncodeStrippingNonLetters(string text, string separator, AlphabetMethodDefinition method)
    {
        // A word boundary must survive decoding. With a non-space separator, a single space already
        // marks the boundary unambiguously; with the default space separator we widen it (sep+" "+sep)
        // so a word gap reads as 2+ spaces while an in-word gap stays a single space.
        var wordGap = IsSpaceSeparator(separator) ? separator + " " + separator : " ";

        var result = new StringBuilder();
        var wordNumbers = new List<int>();
        var wroteWord = false;

        void FlushWord()
        {
            if (wordNumbers.Count == 0)
            {
                return;
            }

            if (wroteWord)
            {
                result.Append(wordGap);
            }

            for (var i = 0; i < wordNumbers.Count; i++)
            {
                if (i > 0)
                {
                    result.Append(separator);
                }

                result.Append(wordNumbers[i]);
            }

            wordNumbers.Clear();
            wroteWord = true;
        }

        foreach (var c in text)
        {
            if (method.TryGetValue(c, out var value))
            {
                wordNumbers.Add(value);
            }
            else if (char.IsWhiteSpace(c))
            {
                FlushWord();
            }

            // Any other non-letter is simply skipped in strip mode.
        }

        FlushWord();
        return result.ToString();
    }

    private static string EncodeKeepingNonLetters(string text, string separator, AlphabetMethodDefinition method)
    {
        // Verbatim pass-through of everything but letters; the separator is inserted only between two
        // letters that are directly adjacent (so runs within a word are split, punctuation untouched).
        var result = new StringBuilder();
        var previousWasNumber = false;

        foreach (var c in text)
        {
            if (method.TryGetValue(c, out var value))
            {
                if (previousWasNumber)
                {
                    result.Append(separator);
                }

                result.Append(value);
                previousWasNumber = true;
            }
            else
            {
                result.Append(c);
                previousWasNumber = false;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Splits the encoded text into words, each a list of numeric tokens. The
    /// <paramref name="separator"/> delimits tokens within a word; a word boundary is a run of 2+
    /// whitespace characters when the separator is itself a space, or any whitespace otherwise.
    /// </summary>
    private static List<List<string>> SplitIntoWords(string text, string separator)
        => IsSpaceSeparator(separator)
            ? SplitOnSpaces(text)
            : SplitOnSeparator(text, separator);

    /// <summary>Space separator: a single space joins tokens within a word; 2+ spaces start a new word.</summary>
    private static List<List<string>> SplitOnSpaces(string text)
    {
        var words = new List<List<string>>();
        var word = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                var spaces = 0;
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    spaces++;
                    i++;
                }

                if (spaces >= 2 && word.Count > 0)
                {
                    words.Add(word);
                    word = [];
                }

                continue;
            }

            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            word.Add(text[start..i]);
        }

        if (word.Count > 0)
        {
            words.Add(word);
        }

        return words;
    }

    /// <summary>
    /// Non-space separator: the separator joins tokens within a word; whitespace the user typed marks a
    /// word boundary, except where it merely pads a separator. A delimiter run is parsed as
    /// <c>ws* (separator ws*)*</c> — if it contained a separator it stays within the word, otherwise it
    /// starts a new word. Empty tokens (from doubled or padded separators) are ignored.
    /// </summary>
    private static List<List<string>> SplitOnSeparator(string text, string separator)
    {
        var words = new List<List<string>>();
        var word = new List<string>();
        var token = new StringBuilder();

        void EndToken()
        {
            if (token.Length > 0)
            {
                word.Add(token.ToString());
                token.Clear();
            }
        }

        bool IsSeparatorAt(int index)
            => index + separator.Length <= text.Length
               && string.CompareOrdinal(text, index, separator, 0, separator.Length) == 0;

        var i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]) || IsSeparatorAt(i))
            {
                // Consume the whole delimiter run, remembering whether it held an explicit separator.
                var hadSeparator = false;
                while (i < text.Length)
                {
                    if (IsSeparatorAt(i))
                    {
                        hadSeparator = true;
                        i += separator.Length;
                    }
                    else if (char.IsWhiteSpace(text[i]))
                    {
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }

                EndToken();
                if (!hadSeparator && word.Count > 0)
                {
                    words.Add(word);
                    word = [];
                }

                continue;
            }

            token.Append(text[i]);
            i++;
        }

        EndToken();
        if (word.Count > 0)
        {
            words.Add(word);
        }

        return words;
    }

    private static string DecodeToken(string token, AlphabetNumberOptions options, AlphabetMethodDefinition method)
    {
        if (int.TryParse(token, out var n))
        {
            if (method.TryGetChar(n, out var letter))
            {
                return Cased(letter, options.UpperCase);
            }

            if (options.WrapModulo && method.TryGetChar(method.WrapIntoRange(n), out var wrapped))
            {
                return Cased(wrapped, options.UpperCase);
            }
        }

        // Unmapped (out of range without wrap, or non-numeric): keep the original or substitute.
        return options.KeepOriginalUnknown ? token : options.UnknownReplacement;
    }

    private static string Cased(char letter, bool upper)
        => (upper ? char.ToUpperInvariant(letter) : letter).ToString();

    private static bool IsSpaceSeparator(string separator)
        => string.IsNullOrEmpty(separator) || separator.All(char.IsWhiteSpace);
}
