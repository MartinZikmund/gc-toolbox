using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class PrimeFormatTests
{
    [TestMethod]
    public void FormatFactorization_DistinctPrimes_JoinsWithMultiplicationSign()
    {
        var factors = PrimeMath.Factorize(600_851_475_143ul);

        Assert.AreEqual("71 × 839 × 1471 × 6857", PrimeFormat.FormatFactorization(factors));
    }

    [TestMethod]
    public void FormatFactorization_RepeatedFactor_UsesSuperscriptExponent()
        => Assert.AreEqual("2³", PrimeFormat.FormatFactorization(PrimeMath.Factorize(8)));

    [TestMethod]
    public void FormatFactorization_MixedExponents_FormatsEachFactor()
        => Assert.AreEqual("2² × 3", PrimeFormat.FormatFactorization(PrimeMath.Factorize(12)));

    [TestMethod]
    public void FormatFactorization_TwoDigitExponent_UsesAllSuperscriptDigits()
        => Assert.AreEqual("2¹⁰", PrimeFormat.FormatFactorization(PrimeMath.Factorize(1024)));
}
