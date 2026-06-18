using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public sealed class HexahueCodecTests
{
    private readonly HexahueCodec _codec = new();

    // ---- Canonical glyph layouts (pinned against the standard Hexahue chart) ----

    [TestMethod]
    public void Encode_A_HasCanonicalSixColourLayout()
    {
        // 'A' = magenta, red, green, yellow, blue, cyan (reading order TL,TR,ML,MR,BL,BR).
        var glyph = _codec.Encode("A").Glyphs.Single();
        Assert.AreEqual(
            new HexahueGlyph(
                HexahueColor.Magenta, HexahueColor.Red,
                HexahueColor.Green, HexahueColor.Yellow,
                HexahueColor.Blue, HexahueColor.Cyan),
            glyph);
    }

    [TestMethod]
    public void Encode_B_SwapsTheFirstTwoSquaresRelativeToA()
    {
        // 'B' = red, magenta, green, yellow, blue, cyan.
        var glyph = _codec.Encode("B").Glyphs.Single();
        Assert.AreEqual(
            new HexahueGlyph(
                HexahueColor.Red, HexahueColor.Magenta,
                HexahueColor.Green, HexahueColor.Yellow,
                HexahueColor.Blue, HexahueColor.Cyan),
            glyph);
    }

    [TestMethod]
    public void Encode_Z_HasCanonicalLayout()
    {
        // 'Z' = cyan, magenta, red, green, yellow, blue.
        var glyph = _codec.Encode("Z").Glyphs.Single();
        Assert.AreEqual(
            new HexahueGlyph(
                HexahueColor.Cyan, HexahueColor.Magenta,
                HexahueColor.Red, HexahueColor.Green,
                HexahueColor.Yellow, HexahueColor.Blue),
            glyph);
    }

    [TestMethod]
    public void Encode_Letter_UsesEachHueExactlyOnce()
    {
        foreach (var letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        {
            var cells = _codec.Encode(letter.ToString()).Glyphs.Single().Cells;
            var distinctHues = cells.Distinct().Count();
            Assert.AreEqual(6, distinctHues, $"Letter {letter} should use six distinct hues.");
            Assert.IsFalse(
                cells.Contains(HexahueColor.White)
                || cells.Contains(HexahueColor.Grey)
                || cells.Contains(HexahueColor.Black),
                $"Letter {letter} should not use any grey shade.");
        }
    }

    [TestMethod]
    public void Encode_Digit_UsesTwoSquaresOfEachGreyShade()
    {
        foreach (var digit in "0123456789")
        {
            var cells = _codec.Encode(digit.ToString()).Glyphs.Single().Cells;
            Assert.AreEqual(2, cells.Count(c => c == HexahueColor.White), $"Digit {digit} white count.");
            Assert.AreEqual(2, cells.Count(c => c == HexahueColor.Grey), $"Digit {digit} grey count.");
            Assert.AreEqual(2, cells.Count(c => c == HexahueColor.Black), $"Digit {digit} black count.");
        }
    }

    [TestMethod]
    public void Encode_Zero_HasCanonicalLayout()
    {
        // '0' = black, grey, white, black, grey, white.
        var glyph = _codec.Encode("0").Glyphs.Single();
        Assert.AreEqual(
            new HexahueGlyph(
                HexahueColor.Black, HexahueColor.Grey,
                HexahueColor.White, HexahueColor.Black,
                HexahueColor.Grey, HexahueColor.White),
            glyph);
    }

    [TestMethod]
    public void Encode_Period_UsesBlackAndWhiteOnly()
    {
        // '.' = black, white, white, black, black, white.
        var glyph = _codec.Encode(".").Glyphs.Single();
        Assert.AreEqual(
            new HexahueGlyph(
                HexahueColor.Black, HexahueColor.White,
                HexahueColor.White, HexahueColor.Black,
                HexahueColor.Black, HexahueColor.White),
            glyph);
        Assert.IsFalse(glyph.Cells.Contains(HexahueColor.Grey));
    }

    [TestMethod]
    public void Encode_Comma_IsTheInverseCheckerOfPeriod()
    {
        // ',' = white, black, black, white, white, black.
        var glyph = _codec.Encode(",").Glyphs.Single();
        Assert.AreEqual(
            new HexahueGlyph(
                HexahueColor.White, HexahueColor.Black,
                HexahueColor.Black, HexahueColor.White,
                HexahueColor.White, HexahueColor.Black),
            glyph);
    }

    [TestMethod]
    public void Encode_Space_IsAllWhite()
    {
        var glyph = _codec.Encode(" ").Glyphs.Single();
        Assert.IsTrue(glyph.Cells.All(c => c == HexahueColor.White));
    }

    // ---- Case folding and accent folding ----

    [TestMethod]
    public void Encode_LowerCase_MatchesUpperCase()
        => Assert.AreEqual(_codec.Encode("A").Glyphs[0], _codec.Encode("a").Glyphs[0]);

    [TestMethod]
    public void Encode_AccentedLetter_FoldsToBaseLetter()
        // Czech Č folds to C.
        => Assert.AreEqual(_codec.Encode("C").Glyphs[0], _codec.Encode("Č").Glyphs[0]);

    [TestMethod]
    public void Encode_AccentedVowel_FoldsToBaseLetter()
        => Assert.AreEqual(_codec.Encode("E").Glyphs[0], _codec.Encode("é").Glyphs[0]);

    // ---- Unsupported characters are reported, not silently dropped ----

    [TestMethod]
    public void Encode_UnsupportedCharacter_IsReportedAndSkipped()
    {
        var result = _codec.Encode("A#B");
        Assert.AreEqual(2, result.Glyphs.Count);
        Assert.AreEqual(1, result.UnsupportedCount);
        CollectionAssert.AreEqual(new[] { '#' }, result.UnsupportedCharacters.ToArray());
    }

    [TestMethod]
    public void Encode_RepeatedUnsupportedCharacters_CountsEveryOccurrence()
    {
        var result = _codec.Encode("##");
        Assert.AreEqual(0, result.Glyphs.Count);
        Assert.AreEqual(2, result.UnsupportedCount);
        // The distinct list dedupes for display, but the count reflects every skipped character.
        CollectionAssert.AreEqual(new[] { '#' }, result.UnsupportedCharacters.ToArray());
    }

    [TestMethod]
    public void Encode_AllSupported_ReportsNoUnsupported()
    {
        var result = _codec.Encode("GC, 42.");
        Assert.AreEqual(0, result.UnsupportedCount);
        Assert.IsFalse(result.HasUnsupported);
    }

    // ---- Empty / null ----

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encode_EmptyOrNull_YieldsNoGlyphs(string? text)
    {
        var result = _codec.Encode(text);
        Assert.AreEqual(0, result.Glyphs.Count);
        Assert.AreEqual(0, result.UnsupportedCount);
    }

    // ---- Decode ----

    [TestMethod]
    public void Decode_KnownGlyphSequence_ProducesText()
    {
        var glyphs = _codec.Encode("HELLO").Glyphs;
        Assert.AreEqual("HELLO", _codec.Decode(glyphs));
    }

    [TestMethod]
    public void Decode_UnknownGlyph_YieldsReplacement()
    {
        // A glyph that is not in the chart decodes to the replacement marker, never throwing.
        var bogus = new HexahueGlyph(
            HexahueColor.Red, HexahueColor.Red, HexahueColor.Red,
            HexahueColor.Red, HexahueColor.Red, HexahueColor.Red);
        Assert.AreEqual(HexahueCodec.UnknownMarker.ToString(), _codec.Decode([bogus]));
    }

    [TestMethod]
    public void Decode_EmptySequence_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, _codec.Decode([]));

    // ---- Round trips ----

    [TestMethod]
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    [DataRow("0123456789")]
    [DataRow("HELLO WORLD")]
    [DataRow("GC1234, FOUND IT.")]
    [DataRow(".,. ,.,")]
    public void RoundTrip_Decode_Of_Encode_IsNormalizedInput(string text)
    {
        var encoded = _codec.Encode(text);
        Assert.AreEqual(0, encoded.UnsupportedCount);
        Assert.AreEqual(text, _codec.Decode(encoded.Glyphs));
    }

    [TestMethod]
    public void RoundTrip_LowerCase_NormalizesToUpper()
    {
        var encoded = _codec.Encode("cache");
        Assert.AreEqual("CACHE", _codec.Decode(encoded.Glyphs));
    }

    // ---- Reference chart ----

    [TestMethod]
    public void GetAlphabet_Has38CharactersPlusSpace()
        // 26 letters + 10 digits + '.' + ',' = 38, plus the dedicated space block = 39 entries.
        => Assert.AreEqual(39, HexahueCodec.GetAlphabet().Count);

    [TestMethod]
    public void GetAlphabet_EntriesAreClickableWithDistinctCharacters()
    {
        var alphabet = HexahueCodec.GetAlphabet();
        var characters = alphabet.Select(e => e.Character).ToList();
        Assert.AreEqual(characters.Count, characters.Distinct().Count(), "Chart characters must be unique.");
        Assert.IsTrue(characters.Contains('A'));
        Assert.IsTrue(characters.Contains('9'));
        Assert.IsTrue(characters.Contains('.'));
        Assert.IsTrue(characters.Contains(','));
        Assert.IsTrue(characters.Contains(' '));
    }

    [TestMethod]
    public void GetAlphabet_EachEntryGlyphRoundTrips()
    {
        foreach (var entry in HexahueCodec.GetAlphabet())
        {
            Assert.AreEqual(entry.Character.ToString(), _codec.Decode([entry.Glyph]));
        }
    }
}
