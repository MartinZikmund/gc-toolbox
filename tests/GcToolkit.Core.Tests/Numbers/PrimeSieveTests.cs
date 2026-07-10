using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class PrimeSieveTests
{
    private static PrimeSieve _sieve = null!;

    [ClassInitialize]
    public static void BuildSieve(TestContext _) => _sieve = new PrimeSieve();

    // ---- NthPrime ----

    [DataTestMethod]
    [DataRow(1, 2L)]
    [DataRow(2, 3L)]
    [DataRow(3, 5L)]
    [DataRow(4, 7L)]
    [DataRow(6, 13L)]
    [DataRow(25, 97L)]
    [DataRow(10_001, 104_743L)]
    [DataRow(1_000_000, 15_485_863L)]
    public void NthPrime_ValidOrdinal_ReturnsKnownPrime(int n, long expected)
        => Assert.AreEqual(expected, _sieve.NthPrime(n));

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(1_000_001)]
    public void NthPrime_OutOfRange_Throws(int n)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _sieve.NthPrime(n));

    // ---- PositionOf ----

    [DataTestMethod]
    [DataRow(2L, 1)]
    [DataRow(3L, 2)]
    [DataRow(97L, 25)]
    [DataRow(104_743L, 10_001)]
    [DataRow(15_485_863L, 1_000_000)]
    public void PositionOf_Prime_ReturnsOrdinal(long value, int expected)
        => Assert.AreEqual(expected, _sieve.PositionOf(value));

    [DataTestMethod]
    [DataRow(1L)]
    [DataRow(4L)]
    [DataRow(104_745L)]
    public void PositionOf_Composite_ReturnsNull(long value)
        => Assert.IsNull(_sieve.PositionOf(value));

    [DataTestMethod]
    [DataRow(0L)]
    [DataRow(-7L)]
    [DataRow(15_485_864L)] // beyond the sieve limit
    public void PositionOf_OutOfRange_ReturnsNull(long value)
        => Assert.IsNull(_sieve.PositionOf(value));

    // ---- CountUpTo ----

    [DataTestMethod]
    [DataRow(1L, 0)]
    [DataRow(2L, 1)]
    [DataRow(10L, 4)]
    [DataRow(100L, 25)]
    [DataRow(15_485_863L, 1_000_000)]
    public void CountUpTo_Value_ReturnsPrimeCount(long value, int expected)
        => Assert.AreEqual(expected, _sieve.CountUpTo(value));

    [TestMethod]
    public void CountUpTo_AboveLimit_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _sieve.CountUpTo(PrimeSieve.Limit + 1));

    // ---- Shared instance ----

    [TestMethod]
    public async Task GetSharedAsync_CalledTwice_ReturnsSameInstance()
    {
        var first = await PrimeSieve.GetSharedAsync();
        var second = await PrimeSieve.GetSharedAsync();

        Assert.AreSame(first, second);
    }
}
