using System;
using System.Linq;
using System.Numerics;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class FibonacciCalculatorTests
{
    private readonly FibonacciCalculator _calculator = new();

    // ---- At: known values (zero-based: F(0) = 0, F(1) = 1) ----

    [DataTestMethod]
    [DataRow(0, "0")]
    [DataRow(1, "1")]
    [DataRow(2, "1")]
    [DataRow(3, "2")]
    [DataRow(10, "55")]
    [DataRow(20, "6765")]
    [DataRow(100, "354224848179261915075")]
    public void At_KnownIndex_ReturnsExpectedValue(int index, string expected)
        => Assert.AreEqual(BigInteger.Parse(expected), _calculator.At(index));

    [TestMethod]
    public void At_10000_Has2090Digits()
        => Assert.AreEqual(2090, FibonacciCalculator.GetDigitCount(_calculator.At(10_000)));

    [TestMethod]
    public void At_MaxIndex_Has20899Digits()
        => Assert.AreEqual(20_899, FibonacciCalculator.GetDigitCount(_calculator.At(FibonacciCalculator.MaxIndex)));

    [TestMethod]
    public void At_NegativeIndex_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _calculator.At(-1));

    [TestMethod]
    public void At_AboveMaxIndex_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _calculator.At(FibonacciCalculator.MaxIndex + 1));

    // ---- Range ----

    [TestMethod]
    public void Range_ZeroToTen_ReturnsElevenEntriesEndingIn55()
    {
        var entries = _calculator.Range(0, 10);

        Assert.AreEqual(11, entries.Count);
        Assert.AreEqual(0, entries[0].Index);
        Assert.AreEqual(BigInteger.Zero, entries[0].Value);
        Assert.AreEqual(10, entries[^1].Index);
        Assert.AreEqual(new BigInteger(55), entries[^1].Value);
    }

    [TestMethod]
    public void Range_SingleIndex_ReturnsOneEntry()
    {
        var entries = _calculator.Range(7, 7);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(new BigInteger(13), entries[0].Value);
    }

    [TestMethod]
    public void Range_FromGreaterThanTo_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _calculator.Range(5, 4));

    [TestMethod]
    public void Range_MatchesAtForEveryEntry()
    {
        var entries = _calculator.Range(95, 105);

        foreach (var entry in entries)
        {
            Assert.AreEqual(_calculator.At(entry.Index), entry.Value);
        }
    }

    // ---- Locate: membership + index + neighbors ----

    [TestMethod]
    public void Locate_89_IsMemberAtIndex11()
    {
        var lookup = _calculator.Locate(new BigInteger(89));

        Assert.IsNotNull(lookup);
        Assert.IsTrue(lookup.IsMember);
        Assert.AreEqual(11, lookup.Index);
    }

    [TestMethod]
    public void Locate_Zero_IsMemberAtIndex0()
    {
        var lookup = _calculator.Locate(BigInteger.Zero);

        Assert.IsNotNull(lookup);
        Assert.IsTrue(lookup.IsMember);
        Assert.AreEqual(0, lookup.Index);
    }

    [TestMethod]
    public void Locate_One_ReportsFirstOccurrenceIndex1()
    {
        // 1 appears twice (F(1) = F(2) = 1); the first occurrence is reported.
        var lookup = _calculator.Locate(BigInteger.One);

        Assert.IsNotNull(lookup);
        Assert.IsTrue(lookup.IsMember);
        Assert.AreEqual(1, lookup.Index);
    }

    [TestMethod]
    public void Locate_100_IsNotMemberWithNeighbors89And144()
    {
        var lookup = _calculator.Locate(new BigInteger(100));

        Assert.IsNotNull(lookup);
        Assert.IsFalse(lookup.IsMember);
        Assert.IsNotNull(lookup.Below);
        Assert.IsNotNull(lookup.Above);
        Assert.AreEqual(11, lookup.Below.Value.Index);
        Assert.AreEqual(new BigInteger(89), lookup.Below.Value.Value);
        Assert.AreEqual(12, lookup.Above.Value.Index);
        Assert.AreEqual(new BigInteger(144), lookup.Above.Value.Value);
    }

    [TestMethod]
    public void Locate_4_IsNotMemberWithNeighbors3And5()
    {
        var lookup = _calculator.Locate(new BigInteger(4));

        Assert.IsNotNull(lookup);
        Assert.IsFalse(lookup.IsMember);
        Assert.AreEqual(new BigInteger(3), lookup.Below!.Value.Value);
        Assert.AreEqual(new BigInteger(5), lookup.Above!.Value.Value);
    }

    [TestMethod]
    public void Locate_LargestSupportedValue_IsMemberAtMaxIndex()
    {
        var max = _calculator.At(FibonacciCalculator.MaxIndex);

        var lookup = _calculator.Locate(max);

        Assert.IsNotNull(lookup);
        Assert.IsTrue(lookup.IsMember);
        Assert.AreEqual(FibonacciCalculator.MaxIndex, lookup.Index);
    }

    [TestMethod]
    public void Locate_BeyondLargestSupportedValue_ReturnsNull()
    {
        var beyond = _calculator.At(FibonacciCalculator.MaxIndex) + 1;

        Assert.IsNull(_calculator.Locate(beyond));
    }

    [TestMethod]
    public void Locate_NegativeValue_ReturnsNull()
        => Assert.IsNull(_calculator.Locate(BigInteger.MinusOne));

    // ---- WithDigitCount ----

    [TestMethod]
    public void WithDigitCount_3_ReturnsF12ThroughF16()
    {
        var entries = _calculator.WithDigitCount(3);

        CollectionAssert.AreEqual(new[] { 12, 13, 14, 15, 16 }, entries.Select(e => e.Index).ToArray());
        CollectionAssert.AreEqual(
            new[] { "144", "233", "377", "610", "987" },
            entries.Select(e => e.Value.ToString()).ToArray());
    }

    [TestMethod]
    public void WithDigitCount_1_StartsAtF0()
    {
        var entries = _calculator.WithDigitCount(1);

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6 }, entries.Select(e => e.Index).ToArray());
        CollectionAssert.AreEqual(
            new[] { "0", "1", "1", "2", "3", "5", "8" },
            entries.Select(e => e.Value.ToString()).ToArray());
    }

    [TestMethod]
    public void WithDigitCount_2090_ContainsF10000()
    {
        var entries = _calculator.WithDigitCount(2090);

        Assert.IsTrue(entries.Any(e => e.Index == 10_000));
        Assert.IsTrue(entries.All(e => FibonacciCalculator.GetDigitCount(e.Value) == 2090));
    }

    [TestMethod]
    public void WithDigitCount_AboveMaxDigitCount_ReturnsEmpty()
        => Assert.AreEqual(0, _calculator.WithDigitCount(FibonacciCalculator.MaxDigitCount + 1).Count);

    [TestMethod]
    public void WithDigitCount_Zero_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _calculator.WithDigitCount(0));

    // ---- GetDigitCount ----

    [DataTestMethod]
    [DataRow("0", 1)]
    [DataRow("9", 1)]
    [DataRow("10", 2)]
    [DataRow("999", 3)]
    [DataRow("1000", 4)]
    public void GetDigitCount_KnownValues_ReturnsDecimalDigitCount(string value, int expected)
        => Assert.AreEqual(expected, FibonacciCalculator.GetDigitCount(BigInteger.Parse(value)));
}
