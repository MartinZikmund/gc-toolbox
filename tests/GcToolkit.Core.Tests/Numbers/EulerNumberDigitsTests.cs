using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class EulerNumberDigitsTests
{
    /// <summary>The first 50 decimals of e (well-known test vector).</summary>
    private const string First50 = "71828182845904523536028747135266249775724709369995";

    private readonly EulerNumberDigits _digits = new(First50);

    // ---- Count ----

    [TestMethod]
    public void Count_KnownVector_Is50()
        => Assert.AreEqual(50, _digits.Count);

    // ---- First ----

    [TestMethod]
    public void First_Ten_ReturnsPrefix()
        => Assert.AreEqual("7182818284", _digits.First(10));

    [TestMethod]
    public void First_All_ReturnsWholeVector()
        => Assert.AreEqual(First50, _digits.First(50));

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(51)]
    public void First_CountOutOfRange_Throws(int count)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _digits.First(count));

    // ---- DigitAt (1-based: position 1 = first decimal) ----

    [TestMethod]
    [DataRow(1, '7')]
    [DataRow(2, '1')]
    [DataRow(3, '8')]
    [DataRow(50, '5')]
    public void DigitAt_ValidPosition_ReturnsDigit(int position, char expected)
        => Assert.AreEqual(expected, _digits.DigitAt(position));

    [TestMethod]
    [DataRow(0)]
    [DataRow(51)]
    public void DigitAt_PositionOutOfRange_Throws(int position)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _digits.DigitAt(position));

    // ---- Range (1-based, inclusive on both ends) ----

    [TestMethod]
    public void Range_FromStart_ReturnsPrefix()
        => Assert.AreEqual("7182818284", _digits.Range(1, 10));

    [TestMethod]
    public void Range_Middle_ReturnsInclusiveSlice()
        => Assert.AreEqual("5904523536", _digits.Range(11, 20));

    [TestMethod]
    public void Range_SinglePosition_ReturnsOneDigit()
        => Assert.AreEqual("5", _digits.Range(50, 50));

    [TestMethod]
    [DataRow(0, 5)]
    [DataRow(1, 51)]
    [DataRow(10, 9)]
    public void Range_InvalidBounds_Throws(int from, int to)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _digits.Range(from, to));

    // ---- Find ----

    [TestMethod]
    public void Find_PatternWithTwoHits_ReturnsBothPositions()
    {
        var result = _digits.Find("1828", maxMatches: 100, contextLength: 0);

        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(2, result.Matches.Count);
        Assert.AreEqual(2, result.Matches[0].Position);
        Assert.AreEqual(6, result.Matches[1].Position);
    }

    [TestMethod]
    public void Find_OverlappingHits_AreAllCounted()
    {
        EulerNumberDigits digits = new("1112");

        var result = digits.Find("11", maxMatches: 100, contextLength: 0);

        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(1, result.Matches[0].Position);
        Assert.AreEqual(2, result.Matches[1].Position);
    }

    [TestMethod]
    public void Find_MoreHitsThanMax_CountsAllButTruncatesMatches()
    {
        var result = _digits.Find("1828", maxMatches: 1, contextLength: 0);

        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(1, result.Matches.Count);
        Assert.AreEqual(2, result.Matches[0].Position);
    }

    [TestMethod]
    public void Find_WithContext_ReturnsSurroundingDigits()
    {
        // "0287" sits at positions 21–24 of the vector.
        var result = _digits.Find("0287", maxMatches: 100, contextLength: 5);

        Assert.AreEqual(1, result.TotalCount);
        var match = result.Matches[0];
        Assert.AreEqual(21, match.Position);
        Assert.AreEqual("23536", match.Before);
        Assert.AreEqual("0287", match.Match);
        Assert.AreEqual("47135", match.After);
    }

    [TestMethod]
    public void Find_MatchAtStart_ClipsLeadingContext()
    {
        var result = _digits.Find("7182", maxMatches: 100, contextLength: 5);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual(1, result.Matches[0].Position);
        Assert.AreEqual(string.Empty, result.Matches[0].Before);
    }

    [TestMethod]
    public void Find_NoHit_ReturnsEmptyResult()
    {
        var result = _digits.Find("000000", maxMatches: 100, contextLength: 0);

        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.Matches.Count);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("12a4")]
    [DataRow("1 2")]
    public void Find_InvalidPattern_Throws(string pattern)
        => Assert.ThrowsExactly<ArgumentException>(() => _digits.Find(pattern, maxMatches: 100, contextLength: 0));

    // ---- FormatGrouped ----

    private const string Digits53 = "12345678901234567890123456789012345678901234567890123";

    [TestMethod]
    public void FormatGrouped_GroupsOnly_TenDigitBlocksFiftyPerLine()
    {
        var expected =
            "1234567890 1234567890 1234567890 1234567890 1234567890" + Environment.NewLine +
            "123";

        Assert.AreEqual(expected, EulerNumberDigits.FormatGrouped(Digits53, startPosition: 1, groupDigits: true, showPositions: false));
    }

    [TestMethod]
    public void FormatGrouped_GroupsAndPositions_PrefixesLineStartPosition()
    {
        var expected =
            " 1: 1234567890 1234567890 1234567890 1234567890 1234567890" + Environment.NewLine +
            "51: 123";

        Assert.AreEqual(expected, EulerNumberDigits.FormatGrouped(Digits53, startPosition: 1, groupDigits: true, showPositions: true));
    }

    [TestMethod]
    public void FormatGrouped_PositionsRespectStartOffset()
    {
        var expected = "101: 12345";

        Assert.AreEqual(expected, EulerNumberDigits.FormatGrouped("12345", startPosition: 101, groupDigits: true, showPositions: true));
    }

    [TestMethod]
    public void FormatGrouped_PositionsOnly_WrapsWithoutInnerSpaces()
    {
        var expected =
            " 1: 12345678901234567890123456789012345678901234567890" + Environment.NewLine +
            "51: 123";

        Assert.AreEqual(expected, EulerNumberDigits.FormatGrouped(Digits53, startPosition: 1, groupDigits: false, showPositions: true));
    }

    [TestMethod]
    public void FormatGrouped_NoOptions_ReturnsRawDigits()
        => Assert.AreEqual(Digits53, EulerNumberDigits.FormatGrouped(Digits53, startPosition: 1, groupDigits: false, showPositions: false));

    [TestMethod]
    public void FormatGrouped_EmptyInput_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, EulerNumberDigits.FormatGrouped(string.Empty, startPosition: 1, groupDigits: true, showPositions: true));
}
