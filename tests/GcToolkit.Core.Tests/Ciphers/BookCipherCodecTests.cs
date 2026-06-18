using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class BookCipherCodecTests
{
    private readonly BookCipherCodec _codec = new();

    private const string Book = "The quick brown fox\njumps over the lazy dog";

    // ---- Decode: whole-word mode ----

    [TestMethod]
    public void Decode_WordMode_1Based_ResolvesWords()
    {
        var format = new BookCipherFormat { Part1 = BookReferencePart.Word, NumberingStart = 1 };

        var result = _codec.Decode(Book, "1 4 9", format);

        // 1=The, 4=fox, 9=dog (global word order, blank lines collapsed)
        Assert.AreEqual("The fox dog", result.Text);
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Decode_WordMode_0Based_ShiftsByOne()
    {
        var format = new BookCipherFormat { Part1 = BookReferencePart.Word, NumberingStart = 0 };

        var result = _codec.Decode(Book, "0 3 8", format);

        Assert.AreEqual("The fox dog", result.Text);
    }

    [TestMethod]
    public void Decode_WordMode_1BasedVs0Based_DifferByOne()
    {
        var oneBased = new BookCipherFormat { NumberingStart = 1 };
        var zeroBased = new BookCipherFormat { NumberingStart = 0 };

        var a = _codec.Decode(Book, "2", oneBased).Text;
        var b = _codec.Decode(Book, "1", zeroBased).Text;

        Assert.AreEqual(a, b);
        Assert.AreEqual("quick", a);
    }

    // ---- Decode: first-letter mode ----

    [TestMethod]
    public void Decode_FirstLetterMode_PullsFirstLetters()
    {
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Word,
            Extraction = BookCipherExtraction.FirstLetter,
            IgnoreSpaces = true,
        };

        // words: 1=The(T) 2=quick(q) 3=brown(b) -> "Tqb"
        var result = _codec.Decode(Book, "1 2 3", format);

        Assert.AreEqual("Tqb", result.Text);
    }

    [TestMethod]
    public void Decode_NthLetterMode_PullsRequestedLetter()
    {
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Word,
            Extraction = BookCipherExtraction.NthLetter,
            LetterIndex = 2,
            IgnoreSpaces = true,
        };

        // 2nd letter of: The(h) quick(u) brown(r) -> "hur"
        var result = _codec.Decode(Book, "1 2 3", format);

        Assert.AreEqual("hur", result.Text);
    }

    // ---- Decode: line:word ----

    [TestMethod]
    public void Decode_LineWord_ResolvesWithinLine()
    {
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Line,
            Part2 = BookReferencePart.Word,
            NumberingStart = 1,
        };

        // line 1 word 3 = brown ; line 2 word 5 = dog
        var result = _codec.Decode(Book, "1:3 2:5", format);

        Assert.AreEqual("brown dog", result.Text);
    }

    [TestMethod]
    public void Decode_AutoInfersSeparator_DashColonDotComma()
    {
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Line,
            Part2 = BookReferencePart.Word,
        };

        Assert.AreEqual("brown", _codec.Decode(Book, "1-3", format).Text);
        Assert.AreEqual("brown", _codec.Decode(Book, "1.3", format).Text);
        Assert.AreEqual("brown", _codec.Decode(Book, "1,3", format).Text);
        Assert.AreEqual("brown", _codec.Decode(Book, "1/3", format).Text);
    }

    // ---- Decode: page:line:word with multi-page source ----

    [TestMethod]
    public void Decode_PageLineWord_ResolvesAcrossPages()
    {
        var multi = "alpha beta\ngamma delta\n---PAGE---\nzulu yankee\nxray whiskey";
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Page,
            Part2 = BookReferencePart.Line,
            Part3 = BookReferencePart.Word,
        };

        // page2 line1 word2 = yankee ; page1 line2 word1 = gamma
        var result = _codec.Decode(multi, "2:1:2 1:2:1", format);

        Assert.AreEqual("yankee gamma", result.Text);
    }

    [TestMethod]
    public void Decode_PageWordLetter_PullsCharacterOfWord()
    {
        var multi = "alpha beta\n---PAGE---\nzulu yankee";
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Page,
            Part2 = BookReferencePart.Word,
            Part3 = BookReferencePart.Character,
            IgnoreSpaces = true,
        };

        // page1 word1=alpha char3=p ; page2 word2=yankee char1=y
        var result = _codec.Decode(multi, "1:1:3 2:2:1", format);

        Assert.AreEqual("py", result.Text);
    }

    // ---- Decode: validation / error flagging ----

    [TestMethod]
    public void Decode_OutOfRangeReference_IsFlaggedNotThrown()
    {
        var format = new BookCipherFormat { Part1 = BookReferencePart.Word };

        var result = _codec.Decode(Book, "1 999", format);

        Assert.IsTrue(result.HasErrors);
        Assert.AreEqual(1, result.ErrorCount);
        Assert.AreEqual("The", result.Text); // good token still resolves
        Assert.IsTrue(result.Tokens[1].IsError);
        StringAssert.Contains(result.Tokens[1].Error!, "out of range");
    }

    [TestMethod]
    public void Decode_NonNumericReference_IsFlagged()
    {
        var format = new BookCipherFormat { Part1 = BookReferencePart.Word };

        var result = _codec.Decode(Book, "abc", format);

        Assert.IsTrue(result.HasErrors);
        StringAssert.Contains(result.Tokens[0].Error!, "not a number");
    }

    [TestMethod]
    public void Decode_WrongPartCount_IsFlagged()
    {
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Line,
            Part2 = BookReferencePart.Word,
        };

        // only one number but two parts configured
        var result = _codec.Decode(Book, "3", format);

        Assert.IsTrue(result.Tokens[0].IsError);
        StringAssert.Contains(result.Tokens[0].Error!, "Expected 2");
    }

    [TestMethod]
    public void Decode_EmptyCodes_ReturnsEmpty()
    {
        var format = new BookCipherFormat { Part1 = BookReferencePart.Word };

        var result = _codec.Decode(Book, "   ", format);

        Assert.AreEqual(string.Empty, result.Text);
        Assert.AreEqual(0, result.Tokens.Count);
    }

    [TestMethod]
    public void Decode_IgnoreSymbols_StripsBeforeIndexing()
    {
        var book = "h-e-l-l-o world";
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Word,
            IgnoreSymbols = "-",
        };

        // After stripping '-': words are "hello" "world"
        Assert.AreEqual("hello", _codec.Decode(book, "1", format).Text);
        Assert.AreEqual("world", _codec.Decode(book, "2", format).Text);
    }

    [TestMethod]
    public void Decode_IgnoreSpaces_JoinsWithoutSeparator()
    {
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Word,
            IgnoreSpaces = true,
        };

        Assert.AreEqual("Thequick", _codec.Decode(Book, "1 2", format).Text);
    }

    // ---- Decode: page-only and line-only scopes ----

    [TestMethod]
    public void Decode_PageOnly_EmitsWholePageText()
    {
        var multi = "alpha beta\n---PAGE---\nzulu yankee";
        var format = new BookCipherFormat { Part1 = BookReferencePart.Page };

        Assert.AreEqual("zulu yankee", _codec.Decode(multi, "2", format).Text);
    }

    [TestMethod]
    public void Decode_CharacterOnly_IndexesWholeBook()
    {
        var format = new BookCipherFormat { Part1 = BookReferencePart.Character, NumberingStart = 1 };

        // flat text "The quick brown fox jumps over the lazy dog", char 1 = 'T'
        Assert.AreEqual("T", _codec.Decode(Book, "1", format).Text);
        Assert.AreEqual("h", _codec.Decode(Book, "2", format).Text);
    }

    // ---- Encode ----

    [TestMethod]
    public void Encode_WordMode_GeneratesReferencesThatRoundTrip()
    {
        var format = new BookCipherFormat { Part1 = BookReferencePart.Word, NumberingStart = 1 };

        var encoded = _codec.Encode("fox dog The", Book, format);
        Assert.IsFalse(encoded.HasErrors);

        var decoded = _codec.Decode(Book, encoded.Text, format);
        Assert.AreEqual("fox dog The", decoded.Text);
    }

    [TestMethod]
    public void Encode_LineWord_RoundTrips()
    {
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Line,
            Part2 = BookReferencePart.Word,
            NumberingStart = 1,
        };

        var encoded = _codec.Encode("brown dog quick", Book, format);
        var decoded = _codec.Decode(Book, encoded.Text, format);

        Assert.AreEqual("brown dog quick", decoded.Text);
    }

    [TestMethod]
    public void Encode_FirstLetterMode_RoundTrips()
    {
        var book = "Apple Banana Cherry\nDate Egg Fig";
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Word,
            Extraction = BookCipherExtraction.FirstLetter,
            IgnoreSpaces = true,
        };

        // "BAD" -> first letters of Banana, Apple, Date
        var encoded = _codec.Encode("BAD", book, format);
        Assert.IsFalse(encoded.HasErrors);

        var decoded = _codec.Decode(book, encoded.Text, format);
        Assert.AreEqual("BAD", decoded.Text);
    }

    [TestMethod]
    public void Encode_WordNotInBook_IsFlagged()
    {
        var format = new BookCipherFormat { Part1 = BookReferencePart.Word };

        var encoded = _codec.Encode("zebra", Book, format);

        Assert.IsTrue(encoded.HasErrors);
        StringAssert.Contains(encoded.Tokens[0].Error!, "not found");
    }

    [TestMethod]
    public void Encode_PageLineWord_RoundTripsAcrossPages()
    {
        var multi = "alpha beta\ngamma delta\n---PAGE---\nzulu yankee\nxray whiskey";
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Page,
            Part2 = BookReferencePart.Line,
            Part3 = BookReferencePart.Word,
        };

        var encoded = _codec.Encode("whiskey alpha delta", multi, format);
        var decoded = _codec.Decode(multi, encoded.Text, format);

        Assert.AreEqual("whiskey alpha delta", decoded.Text);
    }

    [TestMethod]
    public void Encode_EmptyPlaintext_ReturnsEmpty()
    {
        var format = new BookCipherFormat { Part1 = BookReferencePart.Word };
        Assert.AreEqual(string.Empty, _codec.Encode("  ", Book, format).Text);
    }

    // ---- Format helpers ----

    [TestMethod]
    public void UsedParts_DropsNoneParts()
    {
        var format = new BookCipherFormat
        {
            Part1 = BookReferencePart.Page,
            Part2 = BookReferencePart.None,
            Part3 = BookReferencePart.Word,
        };

        CollectionAssert.AreEqual(
            new[] { BookReferencePart.Page, BookReferencePart.Word },
            format.UsedParts.ToArray());
    }
}
