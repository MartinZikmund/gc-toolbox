using GcToolkit.Core.Numbers.Pi;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class PiDigitsEngineTests
{
    /// <summary>The first 50 decimals of π — the canonical test vector.</summary>
    private const string First50 = "14159265358979323846264338327950288419716939937510";

    // ---- First ----

    [TestMethod]
    public void First_TenDecimals_ReturnsLeadingDigits()
        => Assert.AreEqual("1415926535", PiDigitsEngine.First(First50, 10));

    [TestMethod]
    public void First_FullLength_ReturnsWholeString()
        => Assert.AreEqual(First50, PiDigitsEngine.First(First50, 50));

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(51)]
    public void First_CountOutOfRange_Throws(int count)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PiDigitsEngine.First(First50, count));

    // ---- DigitAt (1-based) ----

    [DataTestMethod]
    [DataRow(1, '1')]
    [DataRow(2, '4')]
    [DataRow(10, '5')]
    [DataRow(50, '0')]
    public void DigitAt_KnownPositions_ReturnsExpectedDigit(int position, char expected)
        => Assert.AreEqual(expected, PiDigitsEngine.DigitAt(First50, position));

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(51)]
    public void DigitAt_PositionOutOfRange_Throws(int position)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PiDigitsEngine.DigitAt(First50, position));

    // ---- Range (1-based, inclusive) ----

    [TestMethod]
    public void Range_FirstTen_ReturnsLeadingDigits()
        => Assert.AreEqual("1415926535", PiDigitsEngine.Range(First50, 1, 10));

    [TestMethod]
    public void Range_SinglePosition_ReturnsOneDigit()
        => Assert.AreEqual("9", PiDigitsEngine.Range(First50, 5, 5));

    [TestMethod]
    public void Range_InclusiveUpperBound_IncludesLastDigit()
        => Assert.AreEqual("7510", PiDigitsEngine.Range(First50, 47, 50));

    [DataTestMethod]
    [DataRow(0, 10)]
    [DataRow(1, 51)]
    [DataRow(10, 5)]
    public void Range_InvalidBounds_Throws(int from, int to)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PiDigitsEngine.Range(First50, from, to));

    // ---- Find (overlapping occurrences, 1-based positions) ----

    [TestMethod]
    public void Find_OverlappingOccurrences_ReportsEveryPosition()
    {
        var result = PiDigitsEngine.Find("9995", "99", 10);

        CollectionAssert.AreEqual(new[] { 1, 2 }, result.Positions.ToArray());
        Assert.AreEqual(2, result.TotalCount);
        Assert.IsFalse(result.IsTruncated);
    }

    [TestMethod]
    public void Find_MoreThanMaxPositions_TruncatesButCountsAll()
    {
        var result = PiDigitsEngine.Find("9999", "9", 2);

        CollectionAssert.AreEqual(new[] { 1, 2 }, result.Positions.ToArray());
        Assert.AreEqual(4, result.TotalCount);
        Assert.IsTrue(result.IsTruncated);
    }

    [TestMethod]
    public void Find_NoMatch_ReturnsEmpty()
    {
        var result = PiDigitsEngine.Find(First50, "000", 10);

        Assert.AreEqual(0, result.Positions.Count);
        Assert.AreEqual(0, result.TotalCount);
    }

    [TestMethod]
    public void Find_KnownSequence_ReturnsOneBasedPosition()
    {
        // "26535" starts at decimal position 6.
        var result = PiDigitsEngine.Find(First50, "26535", 10);

        CollectionAssert.AreEqual(new[] { 6 }, result.Positions.ToArray());
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("12a")]
    [DataRow("1.4")]
    public void Find_InvalidPattern_Throws(string pattern)
        => Assert.ThrowsExactly<ArgumentException>(() => PiDigitsEngine.Find(First50, pattern, 10));

    // ---- Context ----

    [TestMethod]
    public void Context_MidString_ReturnsSurroundingDigits()
    {
        var context = PiDigitsEngine.Context("0123456789", fromPosition: 5, length: 2, radius: 3);

        Assert.AreEqual("123", context.Before);
        Assert.AreEqual("45", context.Match);
        Assert.AreEqual("678", context.After);
        Assert.IsTrue(context.HasMoreBefore);
        Assert.IsTrue(context.HasMoreAfter);
    }

    [TestMethod]
    public void Context_AtStart_HasNoLeadingDigits()
    {
        var context = PiDigitsEngine.Context("0123456789", fromPosition: 1, length: 1, radius: 4);

        Assert.AreEqual(string.Empty, context.Before);
        Assert.AreEqual("0", context.Match);
        Assert.AreEqual("1234", context.After);
        Assert.IsFalse(context.HasMoreBefore);
        Assert.IsTrue(context.HasMoreAfter);
    }

    [TestMethod]
    public void Context_AtEnd_HasNoTrailingDigits()
    {
        var context = PiDigitsEngine.Context("0123456789", fromPosition: 10, length: 1, radius: 4);

        Assert.AreEqual("5678", context.Before);
        Assert.AreEqual("9", context.Match);
        Assert.AreEqual(string.Empty, context.After);
        Assert.IsTrue(context.HasMoreBefore);
        Assert.IsFalse(context.HasMoreAfter);
    }
}
