using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class PrimeMathTests
{
    // ---- IsPrime ----

    [DataTestMethod]
    [DataRow(2ul)]
    [DataRow(3ul)]
    [DataRow(5ul)]
    [DataRow(7ul)]
    [DataRow(13ul)]
    [DataRow(104_743ul)]            // the 10,001st prime
    [DataRow(15_485_863ul)]         // the 1,000,000th prime
    [DataRow(9_007_199_254_740_881ul)] // largest prime below 2^53
    public void IsPrime_KnownPrime_ReturnsTrue(ulong value)
        => Assert.IsTrue(PrimeMath.IsPrime(value));

    [DataTestMethod]
    [DataRow(0ul)]
    [DataRow(1ul)]
    [DataRow(4ul)]
    [DataRow(9ul)]
    [DataRow(15ul)]
    [DataRow(561ul)]                 // Carmichael number — fools Fermat, not Miller-Rabin
    [DataRow(104_745ul)]
    [DataRow(9_007_199_254_740_991ul)] // 2^53 - 1 = 6361 × 69431 × 20394401
    [DataRow(9_007_199_254_740_992ul)] // 2^53
    public void IsPrime_KnownComposite_ReturnsFalse(ulong value)
        => Assert.IsFalse(PrimeMath.IsPrime(value));

    // ---- Next / previous / nearest ----

    [DataTestMethod]
    [DataRow(0ul, 2ul)]
    [DataRow(1ul, 2ul)]
    [DataRow(2ul, 3ul)]
    [DataRow(13ul, 17ul)]
    [DataRow(15_485_862ul, 15_485_863ul)]
    public void NextPrime_Value_ReturnsSmallestStrictlyGreaterPrime(ulong value, ulong expected)
        => Assert.AreEqual(expected, PrimeMath.NextPrime(value));

    [TestMethod]
    public void NextPrime_AtMaxValue_ReturnsNull()
        => Assert.IsNull(PrimeMath.NextPrime(PrimeMath.MaxValue));

    [DataTestMethod]
    [DataRow(3ul, 2ul)]
    [DataRow(17ul, 13ul)]
    [DataRow(15_485_864ul, 15_485_863ul)]
    public void PreviousPrime_Value_ReturnsLargestStrictlySmallerPrime(ulong value, ulong expected)
        => Assert.AreEqual(expected, PrimeMath.PreviousPrime(value));

    [TestMethod]
    public void PreviousPrime_AtMaxValue_ReturnsLargestPrimeBelowTwoToFiftyThree()
        => Assert.AreEqual(9_007_199_254_740_881ul, PrimeMath.PreviousPrime(PrimeMath.MaxValue));

    [DataTestMethod]
    [DataRow(2ul)]
    [DataRow(1ul)]
    [DataRow(0ul)]
    public void PreviousPrime_TwoOrBelow_ReturnsNull(ulong value)
        => Assert.IsNull(PrimeMath.PreviousPrime(value));

    [DataTestMethod]
    [DataRow(2ul, 2ul)]   // prime input is its own nearest prime
    [DataRow(17ul, 17ul)]
    [DataRow(10ul, 11ul)] // 11 is closer than 7
    [DataRow(20ul, 19ul)] // 19 is closer than 23
    [DataRow(9ul, 7ul)]   // equidistant (7 and 11) — the smaller wins
    public void NearestPrime_Value_ReturnsClosestPrime(ulong value, ulong expected)
        => Assert.AreEqual(expected, PrimeMath.NearestPrime(value));

    // ---- Factorize ----

    [TestMethod]
    public void Factorize_ProjectEulerNumber_ReturnsFourDistinctPrimes()
    {
        var factors = PrimeMath.Factorize(600_851_475_143ul);

        CollectionAssert.AreEqual(
            new[]
            {
                new PrimeFactor(71, 1),
                new PrimeFactor(839, 1),
                new PrimeFactor(1471, 1),
                new PrimeFactor(6857, 1),
            },
            factors.ToArray());
    }

    [TestMethod]
    public void Factorize_PowerOfTwo_ReturnsSingleFactorWithExponent()
    {
        var factors = PrimeMath.Factorize(8);

        CollectionAssert.AreEqual(new[] { new PrimeFactor(2, 3) }, factors.ToArray());
    }

    [TestMethod]
    public void Factorize_Twelve_ReturnsOrderedFactors()
    {
        var factors = PrimeMath.Factorize(12);

        CollectionAssert.AreEqual(new[] { new PrimeFactor(2, 2), new PrimeFactor(3, 1) }, factors.ToArray());
    }

    [TestMethod]
    public void Factorize_SquareOfLargePrime_ReturnsExponentTwo()
    {
        // 1000003 is prime; rho must split the square and merge the exponents.
        var factors = PrimeMath.Factorize(1_000_003ul * 1_000_003ul);

        CollectionAssert.AreEqual(new[] { new PrimeFactor(1_000_003, 2) }, factors.ToArray());
    }

    [TestMethod]
    public void Factorize_TwoToFiftyThreeMinusOne_ReturnsKnownFactors()
    {
        var factors = PrimeMath.Factorize(9_007_199_254_740_991ul);

        CollectionAssert.AreEqual(
            new[]
            {
                new PrimeFactor(6361, 1),
                new PrimeFactor(69431, 1),
                new PrimeFactor(20394401, 1),
            },
            factors.ToArray());
    }

    [TestMethod]
    public void Factorize_Prime_ReturnsItself()
    {
        var factors = PrimeMath.Factorize(104_743);

        CollectionAssert.AreEqual(new[] { new PrimeFactor(104_743, 1) }, factors.ToArray());
    }

    [DataTestMethod]
    [DataRow(0ul)]
    [DataRow(1ul)]
    public void Factorize_BelowTwo_Throws(ulong value)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PrimeMath.Factorize(value));

    [TestMethod]
    public void Factorize_AboveMaxValue_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PrimeMath.Factorize(PrimeMath.MaxValue + 1));
}
