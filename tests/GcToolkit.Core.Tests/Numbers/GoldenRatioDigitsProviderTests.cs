using GcToolkit.Core.Numbers.GoldenRatio;

namespace GcToolkit.Core.Tests.Numbers;

/// <summary>
/// Verifies the embedded φ digits resource. The digits were generated from
/// isqrt(5·10^(2·(1,000,000+100))) via two independent algorithms (CPython integer
/// <c>math.isqrt</c> and libmpdec <c>Decimal.sqrt</c>, full 1M agreement) and verified against
/// published sources: OEIS A001622 b-file (99,999 decimals, exact) and the University of Arizona
/// 50,000-digit φ file (exact apart from its rounded final digit).
/// </summary>
[TestClass]
public sealed class GoldenRatioDigitsProviderTests
{
    private const string First50 = "61803398874989484820458683436563811772030917980576";

    [TestMethod]
    public async Task GetDecimalsAsync_Loads_ReturnsMillionDecimals()
    {
        GoldenRatioDigitsProvider provider = new();

        var decimals = await provider.GetDecimalsAsync();

        Assert.AreEqual(GoldenRatioDigitsProvider.MaxDecimals, decimals.Length);
    }

    [TestMethod]
    public async Task GetDecimalsAsync_First50_MatchKnownVector()
    {
        GoldenRatioDigitsProvider provider = new();

        var decimals = await provider.GetDecimalsAsync();

        Assert.AreEqual(First50, decimals[..50]);
    }

    // 1-based position → the 10 decimals starting there. Positions ≤ 100,000 are pinned against
    // the OEIS A001622 b-file; deeper ones against the double-algorithm generation cross-check.
    [TestMethod]
    [DataRow(1, "6180339887")]
    [DataRow(100, "4847540880")]
    [DataRow(1_000, "2107673893")]
    [DataRow(10_000, "3294376547")]
    [DataRow(50_000, "5156535661")]
    [DataRow(100_000, "6763351814")]
    [DataRow(250_000, "2274867512")]
    [DataRow(500_000, "1262699463")]
    [DataRow(999_991, "4153226344")]
    public async Task GetDecimalsAsync_DeepSpotChecks_MatchVerifiedSources(int position, string expectedSlice)
    {
        GoldenRatioDigitsProvider provider = new();

        var decimals = await provider.GetDecimalsAsync();

        Assert.AreEqual(expectedSlice, decimals.Substring(position - 1, 10));
    }

    [TestMethod]
    public async Task GetDecimalsAsync_CalledTwice_ReturnsSameCachedInstance()
    {
        GoldenRatioDigitsProvider provider = new();

        var first = await provider.GetDecimalsAsync();
        var second = await provider.GetDecimalsAsync();

        Assert.AreSame(first, second);
    }
}
