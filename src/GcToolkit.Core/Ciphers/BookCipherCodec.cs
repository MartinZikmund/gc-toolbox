using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// What a single ordered part of a reference code addresses inside the source ("book") text.
/// A code carries up to three parts (e.g. <c>page:line:word</c>); unused trailing parts are
/// <see cref="None"/>.
/// </summary>
public enum BookReferencePart
{
    /// <summary>The part is unused (skipped when resolving).</summary>
    None,

    /// <summary>Selects a page, delimited by <c>---PAGE---</c> markers.</summary>
    Page,

    /// <summary>Selects a line within the active scope.</summary>
    Line,

    /// <summary>Selects a word within the active scope.</summary>
    Word,

    /// <summary>Selects a single character within the active scope.</summary>
    Character,
}

/// <summary>How a resolved word is reduced to output text.</summary>
public enum BookCipherExtraction
{
    /// <summary>Emit the whole resolved word.</summary>
    WholeWord,

    /// <summary>Emit only the word's first letter (classic book-cipher behaviour).</summary>
    FirstLetter,

    /// <summary>Emit the word's <c>Nth</c> letter (1-based via <see cref="BookCipherFormat.LetterIndex"/>).</summary>
    NthLetter,
}

/// <summary>
/// The reference layout used to interpret every code group: the three ordered parts, the
/// numbering base, how words are reduced to output, which characters to ignore before indexing,
/// and whether spaces are ignored. A pure value object — change it and re-run to re-decode.
/// </summary>
public sealed record BookCipherFormat
{
    /// <summary>The first (outermost) reference part. Defaults to <see cref="BookReferencePart.Word"/>.</summary>
    public BookReferencePart Part1 { get; init; } = BookReferencePart.Word;

    /// <summary>The second reference part.</summary>
    public BookReferencePart Part2 { get; init; } = BookReferencePart.None;

    /// <summary>The third reference part.</summary>
    public BookReferencePart Part3 { get; init; } = BookReferencePart.None;

    /// <summary>The index the user's first element maps to: 1 (default, human counting) or 0.</summary>
    public int NumberingStart { get; init; } = 1;

    /// <summary>How a resolved word becomes output text.</summary>
    public BookCipherExtraction Extraction { get; init; } = BookCipherExtraction.WholeWord;

    /// <summary>The 1-based letter taken in <see cref="BookCipherExtraction.NthLetter"/> mode.</summary>
    public int LetterIndex { get; init; } = 1;

    /// <summary>Characters stripped from the book before indexing (e.g. punctuation that shouldn't count).</summary>
    public string IgnoreSymbols { get; init; } = string.Empty;

    /// <summary>When <see langword="true"/>, the resolved words are joined with no separator.</summary>
    public bool IgnoreSpaces { get; init; }

    /// <summary>The ordered, used parts (drops trailing/embedded <see cref="BookReferencePart.None"/>).</summary>
    public IReadOnlyList<BookReferencePart> UsedParts =>
        new[] { Part1, Part2, Part3 }.Where(p => p != BookReferencePart.None).ToArray();
}

/// <summary>One resolved code group: the original token, the output it produced, and whether it failed.</summary>
public readonly record struct BookCipherToken(string Reference, string Output, bool IsError, string? Error)
{
    /// <summary>A successfully resolved token.</summary>
    public static BookCipherToken Ok(string reference, string output) => new(reference, output, false, null);

    /// <summary>A token that could not be resolved (out of range, non-numeric, …).</summary>
    public static BookCipherToken Fail(string reference, string error) => new(reference, string.Empty, true, error);
}

/// <summary>The outcome of a decode: the joined <see cref="Text"/> plus the per-token detail.</summary>
public sealed record BookCipherResult(string Text, IReadOnlyList<BookCipherToken> Tokens)
{
    /// <summary>An empty result (no codes supplied).</summary>
    public static readonly BookCipherResult Empty = new(string.Empty, []);

    /// <summary><see langword="true"/> when any token failed to resolve.</summary>
    public bool HasErrors => Tokens.Any(t => t.IsError);

    /// <summary>The number of tokens that failed to resolve.</summary>
    public int ErrorCount => Tokens.Count(t => t.IsError);
}

/// <summary>
/// Pure, stateless book-cipher (Ottendorf) engine — the single source of truth for the transform.
/// <para>
/// <b>Decode</b> resolves numeric reference codes against a pasted source "book". A code has up to
/// three ordered parts, each independently set to Page / Line / Word / Character / None (e.g.
/// <c>page:line:word</c>, <c>line:word</c>, <c>word</c>, <c>page:word:letter</c>). Parts within a
/// code are separated by colon, dash, dot, comma or slash (auto-inferred); code groups are
/// separated by spaces or newlines. A multi-page source uses a <c>---PAGE---</c> delimiter.
/// Numbering base (0- or 1-based) and an "ignore these symbols" filter are configurable, and a word
/// can be reduced to its first letter (or an Nth letter). Out-of-range / non-numeric / ambiguous
/// references are flagged inline — never thrown.
/// </para>
/// <para>
/// <b>Encode</b> (beyond CacheSleuth parity) turns plaintext + a book into reference codes, picking
/// the first position in the book that yields each word (or first letter), so an encode→decode
/// round-trip recovers the message.
/// </para>
/// </summary>
public sealed class BookCipherCodec
{
    /// <summary>The marker that separates pages in a multi-page source.</summary>
    public const string PageDelimiter = "---PAGE---";

    private static readonly char[] PartSeparators = [':', '-', '.', ',', '/'];

    /// <summary>Resolves every code group in <paramref name="codes"/> against <paramref name="book"/>.</summary>
    public BookCipherResult Decode(string? book, string? codes, BookCipherFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (string.IsNullOrWhiteSpace(codes))
        {
            return BookCipherResult.Empty;
        }

        var source = BookSource.Parse(book ?? string.Empty, format);
        var groups = SplitCodeGroups(codes);
        var tokens = new List<BookCipherToken>(groups.Count);
        var builder = new StringBuilder();
        var first = true;

        foreach (var group in groups)
        {
            var token = ResolveGroup(group, source, format);
            tokens.Add(token);

            if (!token.IsError)
            {
                if (!first && !format.IgnoreSpaces)
                {
                    builder.Append(' ');
                }

                builder.Append(token.Output);
                first = false;
            }
        }

        return new BookCipherResult(builder.ToString(), tokens);
    }

    /// <summary>
    /// Generates reference codes for <paramref name="plaintext"/> from <paramref name="book"/>.
    /// In whole-word mode each whitespace-separated word is matched; otherwise each non-space
    /// character is matched as a first letter. Words/letters absent from the book are flagged.
    /// </summary>
    public BookCipherResult Encode(string? plaintext, string? book, BookCipherFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return BookCipherResult.Empty;
        }

        var source = BookSource.Parse(book ?? string.Empty, format);
        var separator = PreferredSeparator(format);

        var units = format.Extraction == BookCipherExtraction.WholeWord
            ? plaintext.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            : plaintext.Where(c => !char.IsWhiteSpace(c)).Select(c => c.ToString()).ToArray();

        var tokens = new List<BookCipherToken>(units.Length);
        var references = new List<string>(units.Length);

        foreach (var unit in units)
        {
            if (source.TryFindReference(unit, format, separator, out var reference))
            {
                tokens.Add(BookCipherToken.Ok(unit, reference));
                references.Add(reference);
            }
            else
            {
                tokens.Add(BookCipherToken.Fail(unit, $"'{unit}' not found in the book"));
            }
        }

        return new BookCipherResult(string.Join(' ', references), tokens);
    }

    /// <summary>Splits the codes input into individual reference groups on whitespace.</summary>
    private static List<string> SplitCodeGroups(string codes)
        => codes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();

    private static BookCipherToken ResolveGroup(string group, BookSource source, BookCipherFormat format)
    {
        var usedParts = format.UsedParts;
        if (usedParts.Count == 0)
        {
            return BookCipherToken.Fail(group, "No reference parts are configured");
        }

        if (!TryParseNumbers(group, usedParts.Count, out var numbers, out var parseError))
        {
            return BookCipherToken.Fail(group, parseError);
        }

        // Map each parsed number to the part it addresses, honouring the numbering base.
        int? page = null, line = null, word = null, character = null;
        for (var i = 0; i < usedParts.Count; i++)
        {
            var index = numbers[i] - format.NumberingStart;
            switch (usedParts[i])
            {
                case BookReferencePart.Page: page = index; break;
                case BookReferencePart.Line: line = index; break;
                case BookReferencePart.Word: word = index; break;
                case BookReferencePart.Character: character = index; break;
            }
        }

        return source.Resolve(group, page, line, word, character, format);
    }

    private static bool TryParseNumbers(string group, int expected, out int[] numbers, out string error)
    {
        var parts = SplitParts(group);
        numbers = [];
        error = string.Empty;

        if (parts.Count == 0)
        {
            error = "Empty reference";
            return false;
        }

        if (parts.Count != expected)
        {
            error = $"Expected {expected} number(s) but got {parts.Count}";
            return false;
        }

        var result = new int[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            if (!int.TryParse(parts[i], out var value))
            {
                error = $"'{parts[i]}' is not a number";
                return false;
            }

            result[i] = value;
        }

        numbers = result;
        return true;
    }

    /// <summary>Splits a single code group on any of the supported part separators (auto-inferred).</summary>
    private static List<string> SplitParts(string group)
        => group.Split(PartSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static char PreferredSeparator(BookCipherFormat format)
        => format.UsedParts.Count > 1 ? ':' : ':';
}
