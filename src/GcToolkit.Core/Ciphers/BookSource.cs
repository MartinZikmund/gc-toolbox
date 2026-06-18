using System.Text;

namespace GcToolkit.Core.Ciphers;

/// <summary>
/// An indexed view of a book/source text: split into pages (on <c>---PAGE---</c>), each page into
/// lines, each line into words. Resolution walks the most-significant configured part down to the
/// least so that a Line number means "line within the selected page" and a Word number means "word
/// within the selected line (or page, or whole book)" — exactly how a human reads a reference.
/// </summary>
internal sealed class BookSource
{
    private readonly IReadOnlyList<Page> _pages;
    private readonly Page _flat; // The whole book as one page (used when no Page part is configured).

    private BookSource(IReadOnlyList<Page> pages, Page flat)
    {
        _pages = pages;
        _flat = flat;
    }

    /// <summary>Builds the index, applying the format's "ignore symbols" filter up front.</summary>
    public static BookSource Parse(string book, BookCipherFormat format)
    {
        var cleaned = StripIgnored(book, format.IgnoreSymbols);
        var pageTexts = cleaned.Split(BookCipherCodec.PageDelimiter, StringSplitOptions.None);
        var pages = pageTexts.Select(Page.Parse).ToArray();
        var flat = Page.Parse(cleaned.Replace(BookCipherCodec.PageDelimiter, "\n"));
        return new BookSource(pages, flat);
    }

    /// <summary>
    /// Resolves the (optional) page/line/word/character indices — already adjusted for the numbering
    /// base — into output, or a flagged failure. Indices are zero-based here.
    /// </summary>
    public BookCipherToken Resolve(string reference, int? page, int? line, int? word, int? character, BookCipherFormat format)
    {
        // Pick the scope: a specific page if addressed, otherwise the flattened whole book.
        Page scope;
        if (page is int p)
        {
            if (p < 0 || p >= _pages.Count)
            {
                return BookCipherToken.Fail(reference, $"page {Display(page, format)} out of range (1..{_pages.Count})");
            }

            scope = _pages[p];
        }
        else
        {
            scope = _flat;
        }

        // Narrow to a line if a Line part is configured.
        if (line is int l)
        {
            if (l < 0 || l >= scope.Lines.Count)
            {
                return BookCipherToken.Fail(reference, $"line {Display(line, format)} out of range (1..{scope.Lines.Count})");
            }

            var lineScope = scope.Lines[l];
            return ResolveWithinLine(reference, lineScope, word, character, format);
        }

        // No line addressed: words/characters index across the whole scope.
        if (character is int && word is null)
        {
            return ResolveCharacter(reference, scope.AllText, character.Value, format);
        }

        if (word is int w)
        {
            if (w < 0 || w >= scope.Words.Count)
            {
                return BookCipherToken.Fail(reference, $"word {Display(word, format)} out of range (1..{scope.Words.Count})");
            }

            return ExtractFromWord(reference, scope.Words[w], character, format);
        }

        // Only a Page was addressed — emit its whole text.
        return BookCipherToken.Ok(reference, scope.AllText);
    }

    private BookCipherToken ResolveWithinLine(string reference, Line line, int? word, int? character, BookCipherFormat format)
    {
        if (word is int w)
        {
            if (w < 0 || w >= line.Words.Count)
            {
                return BookCipherToken.Fail(reference, $"word {Display(word, format)} out of range (1..{line.Words.Count})");
            }

            return ExtractFromWord(reference, line.Words[w], character, format);
        }

        if (character is int c)
        {
            return ResolveCharacter(reference, line.Text, c, format);
        }

        // Only page+line addressed — emit the whole line.
        return BookCipherToken.Ok(reference, line.Text);
    }

    private static BookCipherToken ResolveCharacter(string reference, string text, int index, BookCipherFormat format)
    {
        if (index < 0 || index >= text.Length)
        {
            return BookCipherToken.Fail(reference, $"character {Display(index + format.NumberingStart, format)} out of range (1..{text.Length})");
        }

        return BookCipherToken.Ok(reference, text[index].ToString());
    }

    /// <summary>Applies first-letter / Nth-letter / whole-word extraction, or a character pick within the word.</summary>
    private static BookCipherToken ExtractFromWord(string reference, string word, int? character, BookCipherFormat format)
    {
        // An explicit Character part alongside a Word part means "the Nth character of that word".
        if (character is int c)
        {
            return ResolveCharacter(reference, word, c, format);
        }

        return format.Extraction switch
        {
            BookCipherExtraction.FirstLetter => BookCipherToken.Ok(reference, FirstLetter(word)),
            BookCipherExtraction.NthLetter => NthLetter(reference, word, format),
            _ => BookCipherToken.Ok(reference, word),
        };
    }

    private static string FirstLetter(string word)
        => word.Length == 0 ? string.Empty : word[..1];

    private static BookCipherToken NthLetter(string reference, string word, BookCipherFormat format)
    {
        var index = format.LetterIndex - 1; // LetterIndex is always 1-based.
        if (index < 0 || index >= word.Length)
        {
            return BookCipherToken.Fail(reference, $"letter {format.LetterIndex} out of range (word '{word}' has {word.Length})");
        }

        return BookCipherToken.Ok(reference, word[index].ToString());
    }

    /// <summary>Finds the first book position that yields <paramref name="unit"/>, formatted per <paramref name="format"/>.</summary>
    public bool TryFindReference(string unit, BookCipherFormat format, char separator, out string reference)
    {
        reference = string.Empty;
        var usedParts = format.UsedParts;
        if (usedParts.Count == 0)
        {
            return false;
        }

        var wantFirstLetter = format.Extraction != BookCipherExtraction.WholeWord;
        var comparison = StringComparison.OrdinalIgnoreCase;

        // Page-aware search when a Page part is present; otherwise search the flat book.
        var pages = HasPart(usedParts, BookReferencePart.Page)
            ? _pages.Select((pg, idx) => (page: (int?)idx, pg)).ToArray()
            : [(page: (int?)null, pg: _flat)];

        foreach (var (pageIndex, pg) in pages)
        {
            for (var lineIndex = 0; lineIndex < pg.Lines.Count; lineIndex++)
            {
                var lineWords = pg.Lines[lineIndex].Words;
                for (var wordIndex = 0; wordIndex < lineWords.Count; wordIndex++)
                {
                    var word = lineWords[wordIndex];
                    var candidate = wantFirstLetter ? FirstLetter(word) : word;
                    if (!string.Equals(candidate, unit, comparison))
                    {
                        continue;
                    }

                    reference = FormatReference(format, separator, pageIndex, lineIndex, wordIndex, pg, word);
                    return true;
                }
            }
        }

        return false;
    }

    private static string FormatReference(BookCipherFormat format, char separator, int? pageIndex, int lineIndex, int wordIndex, Page page, string word)
    {
        var globalWordIndex = page.GlobalWordIndex(lineIndex, wordIndex);
        var parts = new List<string>(3);
        foreach (var part in format.UsedParts)
        {
            var value = part switch
            {
                BookReferencePart.Page => (pageIndex ?? 0) + format.NumberingStart,
                BookReferencePart.Line => lineIndex + format.NumberingStart,
                BookReferencePart.Word => (HasPart(format.UsedParts, BookReferencePart.Line) ? wordIndex : globalWordIndex) + format.NumberingStart,
                BookReferencePart.Character => format.LetterIndex, // first/Nth letter index
                _ => format.NumberingStart,
            };
            parts.Add(value.ToString());
        }

        return string.Join(separator, parts);
    }

    private static bool HasPart(IReadOnlyList<BookReferencePart> parts, BookReferencePart part)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] == part)
            {
                return true;
            }
        }

        return false;
    }

    private static string Display(int? zeroBased, BookCipherFormat format)
        => zeroBased is int v ? (v + format.NumberingStart).ToString() : "?";

    private static string StripIgnored(string book, string ignoreSymbols)
    {
        if (string.IsNullOrEmpty(ignoreSymbols) || string.IsNullOrEmpty(book))
        {
            return book;
        }

        var ignore = new HashSet<char>(ignoreSymbols);
        var builder = new StringBuilder(book.Length);
        foreach (var c in book)
        {
            if (!ignore.Contains(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>One page: its lines, the words/text flattened across the page, and a global word map.</summary>
    private sealed class Page
    {
        private readonly int[] _lineWordOffsets; // running word count before each line

        private Page(IReadOnlyList<Line> lines, IReadOnlyList<string> words, string allText, int[] lineWordOffsets)
        {
            Lines = lines;
            Words = words;
            AllText = allText;
            _lineWordOffsets = lineWordOffsets;
        }

        public IReadOnlyList<Line> Lines { get; }

        /// <summary>Every word on the page in reading order (used when no Line part is configured).</summary>
        public IReadOnlyList<string> Words { get; }

        /// <summary>The page text with normalized single spaces (used for page/line character indexing).</summary>
        public string AllText { get; }

        public static Page Parse(string text)
        {
            var rawLines = text.Split('\n');
            var lines = new List<Line>(rawLines.Length);
            var words = new List<string>();
            var offsets = new List<int>(rawLines.Length);

            foreach (var raw in rawLines)
            {
                var trimmed = raw.Trim('\r', ' ', '\t');
                // Skip blank lines so "line 1" is the first line with content (matches how puzzles count).
                if (trimmed.Length == 0)
                {
                    continue;
                }

                offsets.Add(words.Count);
                var line = Line.Parse(trimmed);
                lines.Add(line);
                words.AddRange(line.Words);
            }

            var allText = string.Join(' ', lines.Select(l => l.Text));
            return new Page(lines, words, allText, offsets.ToArray());
        }

        /// <summary>The page-global word index for a word at <paramref name="wordInLine"/> of <paramref name="lineIndex"/>.</summary>
        public int GlobalWordIndex(int lineIndex, int wordInLine)
            => (lineIndex >= 0 && lineIndex < _lineWordOffsets.Length ? _lineWordOffsets[lineIndex] : 0) + wordInLine;
    }

    /// <summary>One line: its words and its normalized text.</summary>
    private sealed class Line
    {
        private Line(IReadOnlyList<string> words, string text)
        {
            Words = words;
            Text = text;
        }

        public IReadOnlyList<string> Words { get; }

        public string Text { get; }

        public static Line Parse(string text)
        {
            var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return new Line(words, string.Join(' ', words));
        }
    }
}
