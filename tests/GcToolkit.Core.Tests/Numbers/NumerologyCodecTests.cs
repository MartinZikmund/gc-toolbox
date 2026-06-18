using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class NumerologyCodecTests
{
    private readonly NumerologyCodec _codec = new();

    // ---- ValueOf: Pythagorean 1-9 (A=1..I=9, J=1 cyclic mod 9) ----

    [TestMethod]
    [DataRow('A', 1)]
    [DataRow('I', 9)]
    [DataRow('J', 1)]
    [DataRow('R', 9)]
    [DataRow('S', 1)]
    [DataRow('Z', 8)]
    public void ValueOf_Pythagorean1To9_MapsCyclically(char letter, int expected)
        => Assert.AreEqual(expected, _codec.ValueOf(letter, NumerologySystem.Pythagorean1To9));

    [TestMethod]
    public void ValueOf_Pythagorean1To9_IsCaseInsensitive()
        => Assert.AreEqual(1, _codec.ValueOf('a', NumerologySystem.Pythagorean1To9));

    // ---- ValueOf: Pythagorean 1-0 (A=1..I=9, J=0, K=1 ...) ----

    [TestMethod]
    [DataRow('A', 1)]
    [DataRow('I', 9)]
    [DataRow('J', 0)]
    [DataRow('K', 1)]
    [DataRow('T', 0)]
    [DataRow('U', 1)]
    public void ValueOf_Pythagorean1To0_MapsCyclicallyWithZero(char letter, int expected)
        => Assert.AreEqual(expected, _codec.ValueOf(letter, NumerologySystem.Pythagorean1To0));

    // ---- ValueOf: Chaldean (never 9) ----

    [TestMethod]
    [DataRow('A', 1)]
    [DataRow('I', 1)]
    [DataRow('J', 1)]
    [DataRow('Q', 1)]
    [DataRow('Y', 1)]
    [DataRow('B', 2)]
    [DataRow('K', 2)]
    [DataRow('R', 2)]
    [DataRow('C', 3)]
    [DataRow('G', 3)]
    [DataRow('L', 3)]
    [DataRow('D', 4)]
    [DataRow('M', 4)]
    [DataRow('V', 4)]
    [DataRow('E', 5)]
    [DataRow('H', 5)]
    [DataRow('N', 5)]
    [DataRow('X', 5)]
    [DataRow('U', 6)]
    [DataRow('W', 6)]
    [DataRow('O', 7)]
    [DataRow('Z', 7)]
    [DataRow('F', 8)]
    [DataRow('P', 8)]
    [DataRow('S', 8)]
    [DataRow('T', 8)]
    public void ValueOf_Chaldean_UsesPhoneticTable(char letter, int expected)
        => Assert.AreEqual(expected, _codec.ValueOf(letter, NumerologySystem.Chaldean));

    [TestMethod]
    public void ValueOf_Chaldean_NeverAssignsNine()
    {
        for (var c = 'A'; c <= 'Z'; c++)
        {
            Assert.AreNotEqual(9, _codec.ValueOf(c, NumerologySystem.Chaldean), $"Chaldean assigned 9 to {c}");
        }
    }

    // ---- ValueOf: Simple/Ordinal (A=1..Z=26) ----

    [TestMethod]
    [DataRow('A', 1)]
    [DataRow('B', 2)]
    [DataRow('Z', 26)]
    public void ValueOf_Simple_IsOrdinal(char letter, int expected)
        => Assert.AreEqual(expected, _codec.ValueOf(letter, NumerologySystem.Simple));

    [TestMethod]
    public void ValueOf_NonLetter_ReturnsZero()
    {
        Assert.AreEqual(0, _codec.ValueOf('5', NumerologySystem.Pythagorean1To9));
        Assert.AreEqual(0, _codec.ValueOf(' ', NumerologySystem.Simple));
        Assert.AreEqual(0, _codec.ValueOf('!', NumerologySystem.Chaldean));
    }

    // ---- Digital root reduction ----

    [TestMethod]
    [DataRow(25, 7)]
    [DataRow(23, 5)]
    [DataRow(6, 6)]
    [DataRow(0, 0)]
    [DataRow(9, 9)]
    [DataRow(10, 1)]
    [DataRow(99, 9)]
    public void Reduce_FullReduce_ReturnsDigitalRoot(int value, int expected)
        => Assert.AreEqual(expected, _codec.Reduce(value, ReductionMode.FullReduce, preserveMasterNumbers: false));

    [TestMethod]
    [DataRow(25, 7)]   // 2+5
    [DataRow(199, 19)] // 1+9+9 single step only, not reduced further
    [DataRow(38, 11)]  // 3+8
    public void Reduce_SingleStep_SumsDigitsOnce(int value, int expected)
        => Assert.AreEqual(expected, _codec.Reduce(value, ReductionMode.SingleStep, preserveMasterNumbers: false));

    [TestMethod]
    [DataRow(25, 25)]
    [DataRow(199, 199)]
    public void Reduce_RawTotal_ReturnsUnchanged(int value, int expected)
        => Assert.AreEqual(expected, _codec.Reduce(value, ReductionMode.RawTotal, preserveMasterNumbers: false));

    // ---- Master-number preservation ----

    [TestMethod]
    [DataRow(11, 11)]
    [DataRow(22, 22)]
    [DataRow(33, 33)]
    public void Reduce_FullReduce_PreservesMasterNumbers_WhenEnabled(int value, int expected)
        => Assert.AreEqual(expected, _codec.Reduce(value, ReductionMode.FullReduce, preserveMasterNumbers: true));

    [TestMethod]
    [DataRow(11, 2)]
    [DataRow(22, 4)]
    [DataRow(33, 6)]
    public void Reduce_FullReduce_ReducesMasterNumbers_WhenDisabled(int value, int expected)
        => Assert.AreEqual(expected, _codec.Reduce(value, ReductionMode.FullReduce, preserveMasterNumbers: false));

    [TestMethod]
    public void Reduce_MasterNumberPreserve_StopsAtMasterMidReduction()
    {
        // 29 -> 11 -> would normally reduce to 2, but preservation stops at the master number.
        Assert.AreEqual(11, _codec.Reduce(29, ReductionMode.FullReduce, preserveMasterNumbers: true));
        Assert.AreEqual(2, _codec.Reduce(29, ReductionMode.FullReduce, preserveMasterNumbers: false));
    }

    // ---- Analyze: known vectors ----

    [TestMethod]
    public void Analyze_Pythagorean1To9_Hello_TotalsTwentyFiveReducesToSeven()
    {
        var result = _codec.Analyze("HELLO", NumerologySystem.Pythagorean1To9, ReductionMode.FullReduce, preserveMasterNumbers: false);

        Assert.AreEqual(25, result.Total);
        Assert.AreEqual(7, result.Reduced);
        Assert.IsTrue(result.HasLetters);
    }

    [TestMethod]
    public void Analyze_Chaldean_Hello_TotalsTwentyThreeReducesToFive()
    {
        var result = _codec.Analyze("HELLO", NumerologySystem.Chaldean, ReductionMode.FullReduce, preserveMasterNumbers: false);

        Assert.AreEqual(23, result.Total);
        Assert.AreEqual(5, result.Reduced);
    }

    [TestMethod]
    public void Analyze_Simple_Abc_TotalsSix()
    {
        var result = _codec.Analyze("ABC", NumerologySystem.Simple, ReductionMode.RawTotal, preserveMasterNumbers: false);

        Assert.AreEqual(6, result.Total);
        Assert.AreEqual(6, result.Reduced);
    }

    // ---- Analyze: per-letter breakdown ----

    [TestMethod]
    public void Analyze_ListsEachLetterValue()
    {
        var result = _codec.Analyze("HELLO", NumerologySystem.Pythagorean1To9, ReductionMode.FullReduce, preserveMasterNumbers: false);

        CollectionAssert.AreEqual(
            new[]
            {
                new LetterValue('H', 8),
                new LetterValue('E', 5),
                new LetterValue('L', 3),
                new LetterValue('L', 3),
                new LetterValue('O', 6),
            },
            result.Letters.ToArray());
    }

    [TestMethod]
    public void Analyze_SkipsNonLettersInBreakdown()
    {
        var result = _codec.Analyze("A1 B!", NumerologySystem.Simple, ReductionMode.RawTotal, preserveMasterNumbers: false);

        // Only A and B contribute; digits/spaces/punctuation are ignored.
        Assert.AreEqual(3, result.Total);
        Assert.AreEqual(2, result.Letters.Count);
        Assert.AreEqual('A', result.Letters[0].Letter);
        Assert.AreEqual('B', result.Letters[1].Letter);
    }

    // ---- Analyze: per-word totals ----

    [TestMethod]
    public void Analyze_SplitsIntoPerWordResults()
    {
        var result = _codec.Analyze("ABC DEF", NumerologySystem.Simple, ReductionMode.FullReduce, preserveMasterNumbers: false);

        Assert.AreEqual(2, result.Words.Count);
        Assert.AreEqual("ABC", result.Words[0].Word);
        Assert.AreEqual(6, result.Words[0].Total);  // 1+2+3
        Assert.AreEqual(6, result.Words[0].Reduced);
        Assert.AreEqual("DEF", result.Words[1].Word);
        Assert.AreEqual(15, result.Words[1].Total); // 4+5+6
        Assert.AreEqual(6, result.Words[1].Reduced); // 1+5
    }

    [TestMethod]
    public void Analyze_PerLineTotals_SplitOnNewlines()
    {
        var result = _codec.Analyze("ABC\nDEF", NumerologySystem.Simple, ReductionMode.RawTotal, preserveMasterNumbers: false);

        // Newlines also act as word/line separators.
        Assert.AreEqual(2, result.Words.Count);
        Assert.AreEqual(6, result.Words[0].Total);
        Assert.AreEqual(15, result.Words[1].Total);
        Assert.AreEqual(21, result.Total);
    }

    // ---- Analyze: validation / empty ----

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("123 !!!")]
    [DataRow(null)]
    public void Analyze_NoLetters_ReportsHasLettersFalse(string? text)
    {
        var result = _codec.Analyze(text, NumerologySystem.Pythagorean1To9, ReductionMode.FullReduce, preserveMasterNumbers: false);

        Assert.IsFalse(result.HasLetters);
        Assert.AreEqual(0, result.Total);
        Assert.AreEqual(0, result.Letters.Count);
        Assert.AreEqual(0, result.Words.Count);
    }

    // ---- AnalyzeAll: every system at once ----

    [TestMethod]
    public void AnalyzeAll_ReturnsOneResultPerSystem()
    {
        var results = _codec.AnalyzeAll("HELLO", ReductionMode.FullReduce, preserveMasterNumbers: false);

        Assert.AreEqual(4, results.Count);
        Assert.AreEqual(25, results[NumerologySystem.Pythagorean1To9].Total);
        Assert.AreEqual(23, results[NumerologySystem.Chaldean].Total);
    }

    // ---- Master numbers flow through Analyze ----

    [TestMethod]
    public void Analyze_MasterNumberPreserve_KeepsElevenUnreduced()
    {
        // "K" in Simple = 11; with preservation the reduced value stays 11.
        var preserved = _codec.Analyze("K", NumerologySystem.Simple, ReductionMode.FullReduce, preserveMasterNumbers: true);
        Assert.AreEqual(11, preserved.Total);
        Assert.AreEqual(11, preserved.Reduced);

        var reduced = _codec.Analyze("K", NumerologySystem.Simple, ReductionMode.FullReduce, preserveMasterNumbers: false);
        Assert.AreEqual(2, reduced.Reduced);
    }
}
