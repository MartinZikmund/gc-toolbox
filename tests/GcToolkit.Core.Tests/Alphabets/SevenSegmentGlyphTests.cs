using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class SevenSegmentGlyphTests
{
    [TestMethod]
    public void For_Digit_ReturnsGlyphWithExpectedSegments()
    {
        // '1' lights only the two right-side segments (b, c).
        var glyph = SevenSegmentGlyph.For('1');
        Assert.IsNotNull(glyph);
        Assert.AreEqual(new SevenSegments(false, true, true, false, false, false, false), glyph.Segments);
    }

    [TestMethod]
    public void For_UnsupportedCharacter_ReturnsNull()
        => Assert.IsNull(SevenSegmentGlyph.For('A'));

    [TestMethod]
    public void For_BeghilosLetter_ReusesResemblingDigitPattern()
    {
        // B resembles 8 — both light all seven segments.
        var b = SevenSegmentGlyph.For('B');
        var eight = SevenSegmentGlyph.For('8');
        Assert.IsNotNull(b);
        Assert.IsNotNull(eight);
        Assert.AreEqual(eight.Segments, b.Segments);
    }

    [TestMethod]
    public void Rotated180_OfEight_IsStillAllSegments()
    {
        var eight = SevenSegmentGlyph.For('8');
        Assert.IsNotNull(eight);
        Assert.AreEqual(eight.Segments, eight.Segments.Rotated180());
    }

    [TestMethod]
    public void Rotated180_SwapsTopBottomAndSides()
    {
        // '7' lights a, b, c. Rotated 180° => a<-d(off), b<-e(off), c<-f(off), d<-a(on), e<-b(on), f<-c(on).
        var seven = SevenSegmentGlyph.For('7');
        Assert.IsNotNull(seven);
        var flipped = seven.Segments.Rotated180();
        Assert.AreEqual(new SevenSegments(false, false, false, true, true, true, false), flipped);
    }

    [TestMethod]
    public void Flipped_MatchesRotated180()
    {
        var glyph = SevenSegmentGlyph.For('2');
        Assert.IsNotNull(glyph);
        Assert.AreEqual(glyph.Segments.Rotated180(), glyph.Flipped);
    }

    [TestMethod]
    public void ForText_MapsEachSupportedCharacter()
    {
        var glyphs = SevenSegmentGlyph.ForText("07734");
        Assert.AreEqual(5, glyphs.Count);
        CollectionAssert.AreEqual("07734".ToCharArray(), glyphs.Select(g => g.Character).ToArray());
    }

    [TestMethod]
    public void ForText_SkipsUnsupportedCharacters()
    {
        var glyphs = SevenSegmentGlyph.ForText("0A7");
        CollectionAssert.AreEqual(new[] { '0', '7' }, glyphs.Select(g => g.Character).ToArray());
    }

    [TestMethod]
    public void ForText_NullOrEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(0, SevenSegmentGlyph.ForText(null).Count);
        Assert.AreEqual(0, SevenSegmentGlyph.ForText("").Count);
    }
}
