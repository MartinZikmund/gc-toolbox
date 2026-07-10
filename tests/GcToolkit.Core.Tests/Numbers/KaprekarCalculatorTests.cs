using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class KaprekarCalculatorTests
{
    private readonly KaprekarCalculator _calculator = new();

    // ---- Routine: the canonical 4-digit example (3524 -> 6174 in 3 steps) ----

    [TestMethod]
    public void Routine_3524FourDigits_ReachesConstantInThreeSteps()
    {
        var result = _calculator.Routine(3524, 4);

        Assert.IsTrue(result.ReachedConstant);
        Assert.IsFalse(result.IsRepdigit);
        Assert.AreEqual(6174, result.Constant);
        Assert.AreEqual(3, result.StepCount);
        Assert.AreEqual(3, result.Steps.Count);
    }

    [TestMethod]
    public void Routine_3524FourDigits_ProducesExactIntermediateValues()
    {
        var steps = _calculator.Routine(3524, 4).Steps;

        // 5432 - 2345 = 3087
        Assert.AreEqual(5432, steps[0].Descending);
        Assert.AreEqual(2345, steps[0].Ascending);
        Assert.AreEqual(3087, steps[0].Difference);

        // 8730 - 0378 = 8352
        Assert.AreEqual(8730, steps[1].Descending);
        Assert.AreEqual(378, steps[1].Ascending);
        Assert.AreEqual(8352, steps[1].Difference);

        // 8532 - 2358 = 6174
        Assert.AreEqual(8532, steps[2].Descending);
        Assert.AreEqual(2358, steps[2].Ascending);
        Assert.AreEqual(6174, steps[2].Difference);
    }

    // ---- Routine: every non-repdigit 4-digit seed reaches 6174 in at most 7 steps ----

    [TestMethod]
    public void Routine_AllFourDigitNonRepdigits_Reach6174WithinSevenSteps()
    {
        for (var n = 0; n <= 9999; n++)
        {
            if (IsRepdigit(n, 4))
            {
                continue;
            }

            var result = _calculator.Routine(n, 4);
            Assert.IsTrue(result.ReachedConstant, $"{n} did not reach the constant");
            Assert.AreEqual(6174, result.Constant, $"{n} converged to the wrong constant");
            Assert.IsTrue(result.StepCount <= 7, $"{n} took {result.StepCount} steps");
        }
    }

    // ---- Routine: 3-digit numbers reach 495 ----

    [TestMethod]
    public void Routine_ThreeDigitNumber_Reaches495()
    {
        var result = _calculator.Routine(123, 3);

        Assert.IsTrue(result.ReachedConstant);
        Assert.AreEqual(495, result.Constant);
        Assert.IsTrue(result.StepCount >= 1);
    }

    [TestMethod]
    public void Routine_AllThreeDigitNonRepdigits_Reach495WithinSixSteps()
    {
        for (var n = 0; n <= 999; n++)
        {
            if (IsRepdigit(n, 3))
            {
                continue;
            }

            var result = _calculator.Routine(n, 3);
            Assert.IsTrue(result.ReachedConstant, $"{n} did not reach the constant");
            Assert.AreEqual(495, result.Constant, $"{n} converged to the wrong constant");
            Assert.IsTrue(result.StepCount <= 6, $"{n} took {result.StepCount} steps");
        }
    }

    // ---- Routine: repdigits collapse to 0 (degenerate, must not loop forever) ----

    [TestMethod]
    public void Routine_Repdigit1111_FlaggedDegenerateAndCollapsesToZero()
    {
        var result = _calculator.Routine(1111, 4);

        Assert.IsTrue(result.IsRepdigit);
        Assert.IsFalse(result.ReachedConstant);
        Assert.AreEqual(0, result.Constant);
        Assert.AreEqual(1, result.Steps.Count);
        Assert.AreEqual(0, result.Steps[0].Difference);
    }

    [TestMethod]
    public void Routine_Repdigit555_FlaggedDegenerate()
    {
        var result = _calculator.Routine(555, 3);

        Assert.IsTrue(result.IsRepdigit);
        Assert.IsFalse(result.ReachedConstant);
        Assert.AreEqual(0, result.Constant);
    }

    [TestMethod]
    public void Routine_ConstantItself_TakesZeroSteps()
    {
        var result = _calculator.Routine(6174, 4);

        Assert.IsTrue(result.ReachedConstant);
        Assert.AreEqual(6174, result.Constant);
        Assert.AreEqual(0, result.StepCount);
    }

    [TestMethod]
    public void Routine_PadsWithLeadingZeros()
    {
        // 5 padded to 4 digits is 0005 -> 5000 - 0005 = 4995.
        var result = _calculator.Routine(5, 4);

        Assert.AreEqual(5000, result.Steps[0].Descending);
        Assert.AreEqual(5, result.Steps[0].Ascending);
        Assert.AreEqual(4995, result.Steps[0].Difference);
        Assert.IsTrue(result.ReachedConstant);
        Assert.AreEqual(6174, result.Constant);
    }

    // ---- IsKaprekarNumber ----

    [DataTestMethod]
    [DataRow(1, true)]
    [DataRow(9, true)]
    [DataRow(45, true)]
    [DataRow(55, true)]
    [DataRow(99, true)]
    [DataRow(297, true)]
    [DataRow(703, true)]
    [DataRow(100, false)]
    [DataRow(46, false)]
    [DataRow(0, false)]
    public void IsKaprekarNumber_KnownValues_MatchExpected(int n, bool expected)
        => Assert.AreEqual(expected, _calculator.IsKaprekarNumber(n));

    [TestMethod]
    public void TrySplitSquare_45_ProducesTwentyAndTwentyFive()
    {
        var ok = _calculator.TryDescribeKaprekar(45, out var description);

        Assert.IsTrue(ok);
        Assert.AreEqual(2025, description.Square);
        Assert.AreEqual(20, description.Left);
        Assert.AreEqual(25, description.Right);
    }

    // ---- ListKaprekar ----

    [TestMethod]
    public void ListKaprekar_UpToHundred_ReturnsKnownSet()
    {
        var list = _calculator.ListKaprekar(100);
        CollectionAssert.AreEqual(new long[] { 1, 9, 45, 55, 99 }, list.ToArray());
    }

    [TestMethod]
    public void ListKaprekar_UpToThousand_Includes297And703()
    {
        var list = _calculator.ListKaprekar(1000);
        CollectionAssert.Contains(list.ToArray(), 297L);
        CollectionAssert.Contains(list.ToArray(), 703L);
    }

    [TestMethod]
    public void ListKaprekar_AboveCap_IsClamped()
    {
        // Should not throw or hang; the cap keeps the work bounded.
        var list = _calculator.ListKaprekar(5_000_000);
        Assert.IsTrue(list.Count > 0);
        Assert.IsTrue(list[^1] <= KaprekarCalculator.MaxListLimit);
    }

    private static bool IsRepdigit(int n, int width)
    {
        var padded = n.ToString().PadLeft(width, '0');
        return padded.All(c => c == padded[0]);
    }
}
