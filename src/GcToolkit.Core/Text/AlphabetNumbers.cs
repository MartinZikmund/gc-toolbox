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

    /// <summary>
    /// When decoding, map a value outside 1–26 back into range with <c>1 + ((n − 1) mod 26)</c> instead
    /// of marking it as <see cref="UnknownMarker"/>. Lets 27→A, 0→Z, −1→Y, etc.
    /// </summary>
    public bool WrapModulo { get; init; }

    /// <summary>When decoding, the output letter case. <see langword="true"/> (default) = upper-case.</summary>
    public bool UpperCase { get; init; } = true;

    /// <summary>
    /// When encoding, keep non-letter characters (digits, punctuation, symbols) verbatim in the output
    /// instead of dropping them. Word boundaries are always preserved either way.
    /// </summary>
    public bool KeepNonLetters { get; init; }
}

/// <summary>
/// Bidirectional A1Z26 codec — the single source of truth for the "numbers ↔ letters" transform.
/// Encoding maps A→1 … Z→26 (case-insensitive) and joins the numbers of a word with a configurable
/// separator; decoding reverses it, mapping 1→A … 26→Z. Word boundaries survive a round trip.
/// Beyond geocachingtoolbox.com parity (which only decodes numbers→letters with a space separator),
/// this codec is bidirectional, takes any separator, can keep or strip non-letters when encoding,
/// offers a configurable output case, and either wraps out-of-range values modulo 26 or flags them.
/// </summary>
public sealed class AlphabetNumbers
{
    /// <summary>Number of letters in the Latin alphabet.</summary>
    public const int AlphabetSize = 26;

    /// <summary>Emitted for a token that is not a valid letter index (and not wrapped).</summary>
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
        return options.KeepNonLetters
            ? EncodeKeepingNonLetters(text, options.Separator)
            : EncodeStrippingNonLetters(text, options.Separator);
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
                result.Append(DecodeToken(token, options));
            }
        }

        return result.ToString();
    }

    private static string EncodeStrippingNonLetters(string text, string separator)
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
            if (TryLetterValue(c, out var value))
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

    private static string EncodeKeepingNonLetters(string text, string separator)
    {
        // Verbatim pass-through of everything but letters; the separator is inserted only between two
        // letters that are directly adjacent (so runs within a word are split, punctuation untouched).
        var result = new StringBuilder();
        var previousWasNumber = false;

        foreach (var c in text)
        {
            if (TryLetterValue(c, out var value))
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

    private static string DecodeToken(string token, AlphabetNumberOptions options)
    {
        if (int.TryParse(token, out var n))
        {
            if (n is >= 1 and <= AlphabetSize)
            {
                return Letter(n, options.UpperCase);
            }

            if (options.WrapModulo)
            {
                return Letter(WrapIntoRange(n), options.UpperCase);
            }
        }

        return UnknownMarker;
    }

    private static string Letter(int index, bool upper)
        => ((char)((upper ? 'A' : 'a') + (index - 1))).ToString();

    /// <summary>Maps any integer into 1–26 via <c>1 + ((n − 1) mod 26)</c> (handles negatives).</summary>
    private static int WrapIntoRange(int n)
    {
        var m = (n - 1) % AlphabetSize;
        if (m < 0)
        {
            m += AlphabetSize;
        }

        return m + 1;
    }

    private static bool TryLetterValue(char c, out int value)
    {
        if (c is >= 'A' and <= 'Z')
        {
            value = c - 'A' + 1;
            return true;
        }

        if (c is >= 'a' and <= 'z')
        {
            value = c - 'a' + 1;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool IsSpaceSeparator(string separator)
        => string.IsNullOrEmpty(separator) || separator.All(char.IsWhiteSpace);
}
