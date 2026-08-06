using GcToolkit.Core.Numbers.GoldenRatio;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public sealed class GoldenRatioDigitOperationsTests
{
    private const string First50 = "61803398874989484820458683436563811772030917980576";

    [TestMethod]
    [DataRow(1, '6')]
    [DataRow(2, '1')]
    [DataRow(25, '8')]
    [DataRow(50, '6')]
    public void DigitAt_ValidPosition_ReturnsExpectedDigit(int position, char expected)
    {
        var digit = GoldenRatioDigitOperations.DigitAt(First50, position);

        Assert.AreEqual(expected, digit);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(51)]
    public void DigitAt_OutOfRange_Throws(int position)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => GoldenRatioDigitOperations.DigitAt(First50, position));
    }

    [TestMethod]
    public void Slice_From11To20_ReturnsSecondBlock()
    {
        var slice = GoldenRatioDigitOperations.Slice(First50, 11, 20);

        Assert.AreEqual("4989484820", slice);
    }

    [TestMethod]
    public void Slice_SinglePosition_ReturnsOneDigit()
    {
        var slice = GoldenRatioDigitOperations.Slice(First50, 25, 25);

        Assert.AreEqual("8", slice);
    }

    [TestMethod]
    [DataRow(0, 10)]
    [DataRow(5, 51)]
    [DataRow(20, 10)]
    public void Slice_InvalidRange_Throws(int from, int to)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => GoldenRatioDigitOperations.Slice(First50, from, to));
    }

    [TestMethod]
    public void Context_MidString_ReturnsRadiusDigitsAroundMatch()
    {
        var (before, after) = GoldenRatioDigitOperations.Context(First50, position: 11, length: 4, radius: 5);

        Assert.AreEqual("39887", before);
        Assert.AreEqual("48482", after);
    }

    [TestMethod]
    public void Context_AtStart_ClampsBeforeToAvailableDigits()
    {
        var (before, after) = GoldenRatioDigitOperations.Context(First50, position: 2, length: 3, radius: 5);

        Assert.AreEqual("6", before);
        Assert.AreEqual("33988", after);
    }

    [TestMethod]
    public void FindOccurrences_PatternPresent_ReturnsAllPositions()
    {
        // "48" occurs at 1-based decimal positions 15 and 17 in the first 50 decimals.
        var result = GoldenRatioDigitOperations.FindOccurrences(First50, "48", maxOccurrences: 10);

        Assert.AreEqual(2, result.TotalCount);
        Assert.IsFalse(result.IsTruncated);
        Assert.AreEqual(15, result.Occurrences[0].Position);
        Assert.AreEqual(17, result.Occurrences[1].Position);
    }

    [TestMethod]
    public void FindOccurrences_OverlappingMatches_AreCounted()
    {
        var result = GoldenRatioDigitOperations.FindOccurrences("11110", "11", maxOccurrences: 10);

        Assert.AreEqual(3, result.TotalCount);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, result.Occurrences.Select(o => o.Position).ToArray());
    }

    [TestMethod]
    public void FindOccurrences_NotPresent_ReturnsEmpty()
    {
        var result = GoldenRatioDigitOperations.FindOccurrences(First50, "000", maxOccurrences: 10);

        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.Occurrences.Count);
    }

    [TestMethod]
    public void FindOccurrences_MoreThanMax_TruncatesListButCountsAll()
    {
        var result = GoldenRatioDigitOperations.FindOccurrences("11111", "1", maxOccurrences: 3);

        Assert.AreEqual(5, result.TotalCount);
        Assert.AreEqual(3, result.Occurrences.Count);
        Assert.IsTrue(result.IsTruncated);
    }

    [TestMethod]
    public void FindOccurrences_OccurrenceCarriesContext()
    {
        var result = GoldenRatioDigitOperations.FindOccurrences(First50, "4989", maxOccurrences: 10);

        Assert.AreEqual(1, result.TotalCount);
        var occurrence = result.Occurrences[0];
        // Default context radius is 10 digits on each side, clamped to the available digits.
        Assert.AreEqual(11, occurrence.Position);
        Assert.AreEqual("4989", occurrence.Match);
        Assert.AreEqual("6180339887", occurrence.Before);
        Assert.AreEqual("4848204586", occurrence.After);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("12a")]
    [DataRow("1 2")]
    public void FindOccurrences_NonDigitPattern_Throws(string pattern)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => GoldenRatioDigitOperations.FindOccurrences(First50, pattern, maxOccurrences: 10));
    }
}
