using System.Numerics;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class CollatzCalculatorTests
{
    private readonly CollatzCalculator _calculator = new();

    // ---- Trace: sequence ----

    [TestMethod]
    public void Trace_Six_ProducesExpectedSequence()
    {
        var trace = _calculator.Trace(6);

        CollectionAssert.AreEqual(
            new BigInteger[] { 6, 3, 10, 5, 16, 8, 4, 2, 1 },
            trace.Sequence.ToArray());
    }

    [TestMethod]
    public void Trace_Six_HasEightSteps()
        => Assert.AreEqual(8, _calculator.Trace(6).Steps);

    [TestMethod]
    public void Trace_Six_LengthIsNineIncludingStartAndOne()
        => Assert.AreEqual(9, _calculator.Trace(6).Length);

    [TestMethod]
    public void Trace_One_IsJustOneWithZeroSteps()
    {
        var trace = _calculator.Trace(1);

        CollectionAssert.AreEqual(new BigInteger[] { 1 }, trace.Sequence.ToArray());
        Assert.AreEqual(0, trace.Steps);
        Assert.AreEqual(1, trace.Length);
    }

    [TestMethod]
    public void Trace_Two_IsTwoThenOne()
    {
        var trace = _calculator.Trace(2);

        CollectionAssert.AreEqual(new BigInteger[] { 2, 1 }, trace.Sequence.ToArray());
        Assert.AreEqual(1, trace.Steps);
    }

    // ---- Trace: stopping time / peak ----

    [TestMethod]
    public void Trace_TwentySeven_Has111Steps()
        => Assert.AreEqual(111, _calculator.Trace(27).Steps);

    [TestMethod]
    public void Trace_TwentySeven_PeaksAt9232()
        => Assert.AreEqual(new BigInteger(9232), _calculator.Trace(27).Peak);

    [TestMethod]
    public void Trace_TwentySeven_PeakIndexPointsAtPeakValue()
    {
        var trace = _calculator.Trace(27);

        Assert.AreEqual(trace.Peak, trace.Sequence[trace.PeakIndex]);
    }

    [TestMethod]
    public void Trace_Six_PeakIs16AtIndexFour()
    {
        var trace = _calculator.Trace(6);

        Assert.AreEqual(new BigInteger(16), trace.Peak);
        Assert.AreEqual(4, trace.PeakIndex);
    }

    [TestMethod]
    public void Trace_PowerOfTwo_PeakIsTheStartItself()
    {
        // 16 -> 8 -> 4 -> 2 -> 1 monotonically descends, so the start is the peak.
        var trace = _calculator.Trace(16);

        Assert.AreEqual(new BigInteger(16), trace.Peak);
        Assert.AreEqual(0, trace.PeakIndex);
        Assert.AreEqual(4, trace.Steps);
    }

    // ---- Trace: beyond-parity split + parity bits ----

    [TestMethod]
    public void Trace_Six_SplitsEvenAndOddSteps()
    {
        // 6,3,10,5,16,8,4,2,1: operations applied at 6,3,10,5,16,8,4,2 -> even at 6,10,16,8,4,2 (6),
        // odd at 3,5 (2).
        var trace = _calculator.Trace(6);

        Assert.AreEqual(6, trace.EvenSteps);
        Assert.AreEqual(2, trace.OddSteps);
        Assert.AreEqual(trace.Steps, trace.EvenSteps + trace.OddSteps);
    }

    [TestMethod]
    public void Trace_Six_ParityBitsMatchEachStartingValue()
    {
        // One char per step (excluding the terminal 1): 'E' when that value was even, 'O' when odd.
        // 6(E) 3(O) 10(E) 5(O) 16(E) 8(E) 4(E) 2(E).
        Assert.AreEqual("EOEOEEEE", _calculator.Trace(6).ParityBits);
    }

    [TestMethod]
    public void Trace_One_HasEmptyParityBits()
        => Assert.AreEqual(string.Empty, _calculator.Trace(1).ParityBits);

    // ---- Trace: BigInteger does not overflow ----

    [TestMethod]
    public void Trace_LargeStarter_DoesNotOverflowAndEndsAtOne()
    {
        // Beyond long range; 3n+1 on a huge odd value must stay exact.
        var big = BigInteger.Pow(2, 100) + 1;
        var trace = _calculator.Trace(big);

        Assert.AreEqual(big, trace.Sequence[0]);
        Assert.AreEqual(BigInteger.One, trace.Sequence[^1]);
        Assert.IsTrue(trace.Peak >= big);
    }

    // ---- Trace: validation ----

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(-27)]
    public void Trace_NonPositive_Throws(int value)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _calculator.Trace(value));

    // ---- MostStubborn ----

    [TestMethod]
    public void MostStubborn_OneToTen_IsNineWithNineteenSteps()
    {
        var result = _calculator.MostStubborn(10);

        // 9 -> ... -> 1 takes 19 steps, the longest among 1..10.
        Assert.AreEqual(new BigInteger(9), result.Starter);
        Assert.AreEqual(19, result.Steps);
    }

    [TestMethod]
    public void MostStubborn_OneToOne_IsOneWithZeroSteps()
    {
        var result = _calculator.MostStubborn(1);

        Assert.AreEqual(BigInteger.One, result.Starter);
        Assert.AreEqual(0, result.Steps);
    }

    [TestMethod]
    public void MostStubborn_OneHundred_IsNinetySevenWith118Steps()
    {
        var result = _calculator.MostStubborn(100);

        Assert.AreEqual(new BigInteger(97), result.Starter);
        Assert.AreEqual(118, result.Steps);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void MostStubborn_NonPositiveLimit_Throws(int limit)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _calculator.MostStubborn(limit));

    [TestMethod]
    public void MostStubborn_AboveCap_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _calculator.MostStubborn(CollatzCalculator.MaxSearchLimit + 1));

    [TestMethod]
    public void MaxSearchLimit_Is1Million()
    {
        var cap = CollatzCalculator.MaxSearchLimit;

        Assert.AreEqual(1_000_000, cap);
    }

    [TestMethod]
    public void MostStubborn_SmallLimit_ReturnsSaneResult()
    {
        // Exercise the real search on a tiny range (no full 1M run): the result must be a valid
        // in-range starter with a non-negative stopping time that matches a direct StepCount.
        var result = _calculator.MostStubborn(5);

        Assert.IsTrue(result.Starter >= CollatzCalculator.MinValue && result.Starter <= 5);
        Assert.IsTrue(result.Steps >= 0);
        Assert.AreEqual(_calculator.StepCount(result.Starter), result.Steps);
    }
}
