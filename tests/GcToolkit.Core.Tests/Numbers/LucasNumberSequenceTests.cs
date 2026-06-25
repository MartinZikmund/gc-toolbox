using System.Numerics;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class LucasNumberSequenceTests
{
    private readonly LucasNumberSequence _sequence = new();

    // ---- At: anchors L0..L10 ----

    [DataTestMethod]
    [DataRow(0, 2)]
    [DataRow(1, 1)]
    [DataRow(2, 3)]
    [DataRow(3, 4)]
    [DataRow(4, 7)]
    [DataRow(5, 11)]
    [DataRow(6, 18)]
    [DataRow(7, 29)]
    [DataRow(8, 47)]
    [DataRow(9, 76)]
    [DataRow(10, 123)]
    public void At_AnchorIndices_ReturnsKnownLucasNumbers(int index, int expected)
        => Assert.AreEqual(new BigInteger(expected), _sequence.At(index));

    [TestMethod]
    public void At_FollowsRecurrence_EachIsSumOfPreviousTwo()
    {
        for (var n = 2; n <= 50; n++)
        {
            Assert.AreEqual(_sequence.At(n - 1) + _sequence.At(n - 2), _sequence.At(n));
        }
    }

    [TestMethod]
    public void At_Index100_MatchesKnownValue()
        => Assert.AreEqual(BigInteger.Parse("792070839848372253127"), _sequence.At(100));

    [TestMethod]
    public void At_Index10000_HasExpectedDigitLength()
        => Assert.AreEqual(2090, _sequence.At(10000).ToString().Length);

    [TestMethod]
    public void At_LargeIndex_DoesNotOverflow()
    {
        // BigInteger means no overflow; the value is simply very large.
        var value = _sequence.At(5000);
        Assert.IsTrue(value > BigInteger.Zero);
        Assert.AreEqual(1045, value.ToString().Length);
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(-100)]
    public void At_NegativeIndex_Throws(int index)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _sequence.At(index));

    // ---- Range ----

    [TestMethod]
    public void Range_InclusiveBounds_ReturnsEveryIndex()
    {
        var result = _sequence.Range(2, 5).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                (2, new BigInteger(3)),
                (3, new BigInteger(4)),
                (4, new BigInteger(7)),
                (5, new BigInteger(11)),
            },
            result);
    }

    [TestMethod]
    public void Range_SingleIndex_ReturnsOneItem()
    {
        var result = _sequence.Range(7, 7).ToArray();

        Assert.AreEqual(1, result.Length);
        Assert.AreEqual(7, result[0].Index);
        Assert.AreEqual(new BigInteger(29), result[0].Value);
    }

    [TestMethod]
    public void Range_StartGreaterThanEnd_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() => _sequence.Range(5, 2).ToArray());

    [DataTestMethod]
    [DataRow(-1, 5)]
    [DataRow(0, -1)]
    public void Range_NegativeBound_Throws(int start, int end)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _sequence.Range(start, end).ToArray());

    // ---- IndexOf ----

    [DataTestMethod]
    [DataRow(2, 0)]
    [DataRow(1, 1)]
    [DataRow(3, 2)]
    [DataRow(4, 3)]
    [DataRow(7, 4)]
    [DataRow(11, 5)]
    [DataRow(76, 9)]
    [DataRow(123, 10)]
    public void IndexOf_LucasNumber_ReturnsItsIndex(int value, int expectedIndex)
        => Assert.AreEqual(expectedIndex, _sequence.IndexOf(new BigInteger(value)));

    [TestMethod]
    public void IndexOf_One_ReturnsIndexOne()
    {
        // 1 appears only at index 1 (L1 = 1); we return the unambiguous strictly-increasing position.
        Assert.AreEqual(1, _sequence.IndexOf(BigInteger.One));
    }

    [DataTestMethod]
    [DataRow(100)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(0)]
    public void IndexOf_NonLucasNumber_ReturnsNull(int value)
        => Assert.IsNull(_sequence.IndexOf(new BigInteger(value)));

    [TestMethod]
    public void IndexOf_NegativeValue_ReturnsNull()
        => Assert.IsNull(_sequence.IndexOf(new BigInteger(-7)));

    [TestMethod]
    public void IndexOf_LargeLucasNumber_RoundTripsWithAt()
    {
        var value = _sequence.At(60);
        Assert.AreEqual(60, _sequence.IndexOf(value));
    }

    // ---- WithDigitCount ----

    [TestMethod]
    public void WithDigitCount_OneDigit_ReturnsIndices0To4()
    {
        var result = _sequence.WithDigitCount(1).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                (0, new BigInteger(2)),
                (1, new BigInteger(1)),
                (2, new BigInteger(3)),
                (3, new BigInteger(4)),
                (4, new BigInteger(7)),
            },
            result);
    }

    [TestMethod]
    public void WithDigitCount_TwoDigits_ReturnsIndices5To9()
    {
        var result = _sequence.WithDigitCount(2).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                (5, new BigInteger(11)),
                (6, new BigInteger(18)),
                (7, new BigInteger(29)),
                (8, new BigInteger(47)),
                (9, new BigInteger(76)),
            },
            result);
    }

    [TestMethod]
    public void WithDigitCount_ThreeDigits_ReturnsIndices10To14()
    {
        var result = _sequence.WithDigitCount(3).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                (10, new BigInteger(123)),
                (11, new BigInteger(199)),
                (12, new BigInteger(322)),
                (13, new BigInteger(521)),
                (14, new BigInteger(843)),
            },
            result);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void WithDigitCount_NonPositive_Throws(int digits)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _sequence.WithDigitCount(digits).ToArray());

    // ---- Validation guards ----

    [TestMethod]
    public void At_JustAboveMaxIndex_Throws()
    {
        // Fast boundary check: the guard must reject MaxIndex + 1 without computing the enormous
        // L(MaxIndex) value. Big-value coverage is provided by At_Index10000_HasExpectedDigitLength.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _sequence.At(LucasNumberSequence.MaxIndex + 1));
    }
}
