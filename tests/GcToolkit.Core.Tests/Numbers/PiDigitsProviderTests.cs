using GcToolkit.Core.Numbers.Pi;

namespace GcToolkit.Core.Tests.Numbers;

/// <summary>
/// Verifies the embedded 1,000,000 π decimals. The deep-position vectors were cross-checked
/// against three independent sources: a Chudnovsky binary-splitting computation, angio.net,
/// and Princeton's pi-10million.txt — all agree on the full 1,000,000 decimals.
/// </summary>
[TestClass]
public class PiDigitsProviderTests
{
    private const string First50 = "14159265358979323846264338327950288419716939937510";

    private static string? _decimals;

    [ClassInitialize]
    public static async Task LoadDecimals(TestContext context)
        => _decimals = await new PiDigitsProvider().GetDecimalsAsync();

    private static string Decimals => _decimals!;

    [TestMethod]
    public void GetDecimalsAsync_EmbeddedResource_HasExactlyOneMillionDecimals()
        => Assert.AreEqual(PiDigitsProvider.DecimalCount, Decimals.Length);

    [TestMethod]
    public void GetDecimalsAsync_EmbeddedResource_StartsWithKnownFirst50()
        => Assert.IsTrue(Decimals.StartsWith(First50, StringComparison.Ordinal));

    [TestMethod]
    public void GetDecimalsAsync_FeynmanPoint_SixNinesAtPosition762()
        => Assert.AreEqual("999999", PiDigitsEngine.Range(Decimals, 762, 767));

    [DataTestMethod]
    [DataRow(100, '9')]
    [DataRow(1_000, '9')]
    [DataRow(31_415, '1')]
    [DataRow(500_000, '2')]
    [DataRow(999_999, '5')]
    [DataRow(1_000_000, '1')]
    public void GetDecimalsAsync_DeepPositions_MatchVerifiedVectors(int position, char expected)
        => Assert.AreEqual(expected, PiDigitsEngine.DigitAt(Decimals, position));

    [TestMethod]
    public void GetDecimalsAsync_AllCharacters_AreDigits()
        => Assert.IsTrue(Decimals.All(char.IsAsciiDigit));

    [TestMethod]
    public async Task GetDecimalsAsync_CalledTwice_ReturnsCachedInstance()
    {
        PiDigitsProvider provider = new();

        var first = await provider.GetDecimalsAsync();
        var second = await provider.GetDecimalsAsync();

        Assert.AreSame(first, second);
    }
}
