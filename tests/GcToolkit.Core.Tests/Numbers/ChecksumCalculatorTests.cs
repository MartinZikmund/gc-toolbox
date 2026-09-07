using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class ChecksumCalculatorTests
{
    // ---- DigitSum: the cross sum of a number ----

    [TestMethod]
    [DataRow(0L, 0)]
    [DataRow(7L, 7)]
    [DataRow(10L, 1)]
    [DataRow(99L, 18)]
    [DataRow(12345L, 15)]
    [DataRow(1000000L, 1)]
    public void DigitSum_Number_SumsItsDigits(long value, int expected)
        => Assert.AreEqual(expected, ChecksumCalculator.DigitSum(value));

    [TestMethod]
    public void DigitSum_NegativeNumber_IgnoresTheSign()
        => Assert.AreEqual(ChecksumCalculator.DigitSum(12345), ChecksumCalculator.DigitSum(-12345));

    [TestMethod]
    public void DigitSum_LongMinValue_DoesNotOverflow()
    {
        // -9223372036854775808 has no positive counterpart, so a naive Math.Abs would throw.
        Assert.AreEqual(89, ChecksumCalculator.DigitSum(long.MinValue));
    }

    // ---- DigitalRoot: keep summing until one digit is left ----

    [TestMethod]
    [DataRow(0L, 0)]
    [DataRow(9L, 9)]
    [DataRow(18L, 9)]
    [DataRow(100L, 1)]
    [DataRow(12345L, 6)]
    [DataRow(999999999L, 9)]
    public void DigitalRoot_Number_ReducesToASingleDigit(long value, int expected)
        => Assert.AreEqual(expected, ChecksumCalculator.DigitalRoot(value));

    [TestMethod]
    public void DigitalRoot_NegativeNumber_IgnoresTheSign()
        => Assert.AreEqual(6, ChecksumCalculator.DigitalRoot(-12345));

    // ---- Reduce: the chain a geocacher writes out, e.g. 12345 = 15 = 6 ----

    [TestMethod]
    public void Reduce_MultiDigitNumber_ReturnsEveryIntermediateSum()
    {
        var steps = ChecksumCalculator.Reduce(12345);

        CollectionAssert.AreEqual(new long[] { 12345, 15, 6 }, steps.ToArray());
    }

    [TestMethod]
    public void Reduce_SingleDigit_ReturnsJustThatDigit()
    {
        CollectionAssert.AreEqual(new long[] { 6 }, ChecksumCalculator.Reduce(6).ToArray());
        CollectionAssert.AreEqual(new long[] { 0 }, ChecksumCalculator.Reduce(0).ToArray());
    }

    [TestMethod]
    public void Reduce_NegativeValue_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ChecksumCalculator.Reduce(-1));

    // ---- ReduceMagnitude: the sign-agnostic chain the word-value tool shares ----

    [TestMethod]
    public void ReduceMagnitude_NegativeValue_ReducesTheMagnitude()
        => CollectionAssert.AreEqual(new long[] { 12345, 15, 6 }, ChecksumCalculator.ReduceMagnitude(-12345).ToArray());

    [TestMethod]
    public void ReduceMagnitude_PositiveValue_MatchesReduce()
        => CollectionAssert.AreEqual(ChecksumCalculator.Reduce(12345).ToArray(), ChecksumCalculator.ReduceMagnitude(12345).ToArray());

    [TestMethod]
    public void ReduceMagnitude_LongMinValue_DoesNotOverflow()
        => CollectionAssert.AreEqual(new long[] { 89, 17, 8 }, ChecksumCalculator.ReduceMagnitude(long.MinValue).ToArray());

    // ---- LetterValue: A=1 … Z=26 ----

    [TestMethod]
    [DataRow('A', 1)]
    [DataRow('a', 1)]
    [DataRow('M', 13)]
    [DataRow('Z', 26)]
    [DataRow('z', 26)]
    [DataRow('5', 0)]
    [DataRow('-', 0)]
    [DataRow(' ', 0)]
    public void LetterValue_Character_ScoresLettersOnly(char character, int expected)
        => Assert.AreEqual(expected, ChecksumCalculator.LetterValue(character));

    // ---- Analyze: empty and blank input ----

    [TestMethod]
    public void Analyze_NullInput_ReturnsEmptyAnalysis()
    {
        var result = ChecksumCalculator.Analyze(null);

        Assert.IsFalse(result.HasContent);
        Assert.AreEqual(0, result.Total);
        Assert.AreEqual(0, result.TotalRoot);
        Assert.AreEqual(0, result.Parts.Count);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(" -.,!? ")]
    public void Analyze_NothingScorable_HasNoContent(string input)
    {
        var result = ChecksumCalculator.Analyze(input);

        Assert.IsFalse(result.HasContent);
        Assert.AreEqual(0, result.Digits.Count);
        Assert.AreEqual(0, result.Letters.Count);
    }

    // ---- Analyze: digits ----

    [TestMethod]
    public void Analyze_DigitsOnly_SumsThemAndReducesToTheRoot()
    {
        var result = ChecksumCalculator.Analyze("12345");

        Assert.AreEqual(5, result.Digits.Count);
        Assert.AreEqual(15, result.Digits.Value);
        Assert.AreEqual(6, result.Digits.Root);
        CollectionAssert.AreEqual(new long[] { 15, 6 }, result.Digits.Reduction.ToArray());
        Assert.AreEqual(0, result.Letters.Count);
        Assert.AreEqual(15, result.Total);
        Assert.AreEqual(6, result.TotalRoot);
        Assert.IsFalse(result.IsMixed);
    }

    [TestMethod]
    public void Analyze_DigitsOnly_KeepsEachDigitAsATermSoTheWorkingCanBeShown()
    {
        var terms = ChecksumCalculator.Analyze("12345").Digits.Terms;

        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, terms.Select(t => t.Value).ToArray());
        CollectionAssert.AreEqual("12345".ToCharArray(), terms.Select(t => t.Character).ToArray());
    }

    [TestMethod]
    public void Analyze_Zero_CountsTheDigitButSumsToZero()
    {
        var result = ChecksumCalculator.Analyze("0");

        Assert.IsTrue(result.HasContent);
        Assert.AreEqual(1, result.Digits.Count);
        Assert.AreEqual(0, result.Digits.Value);
        Assert.AreEqual(0, result.TotalRoot);
    }

    [TestMethod]
    public void Analyze_MinusSign_IsIgnoredLikeAnyOtherPunctuation()
    {
        var negative = ChecksumCalculator.Analyze("-12345");

        Assert.AreEqual(15, negative.Total);
        Assert.AreEqual(1, negative.IgnoredCount);
    }

    // ---- Analyze: letters ----

    [TestMethod]
    public void Analyze_LettersOnly_ScoresThemA1Z26()
    {
        var result = ChecksumCalculator.Analyze("CACHE");

        // 3 + 1 + 3 + 8 + 5
        Assert.AreEqual(5, result.Letters.Count);
        Assert.AreEqual(20, result.Letters.Value);
        Assert.AreEqual(2, result.Letters.Root);
        Assert.AreEqual(0, result.Digits.Count);
    }

    [TestMethod]
    public void Analyze_LetterCase_DoesNotChangeTheResult()
        => Assert.AreEqual(
            ChecksumCalculator.Analyze("CACHE").Total,
            ChecksumCalculator.Analyze("cache").Total);

    [TestMethod]
    public void Analyze_LettersOnly_KeepsEachLetterAsATerm()
    {
        var terms = ChecksumCalculator.Analyze("Zoe").Letters.Terms;

        CollectionAssert.AreEqual(new[] { 26, 15, 5 }, terms.Select(t => t.Value).ToArray());
        // Terms carry the upper-cased letter so the working reads "Z 26 + O 15 + E 5".
        CollectionAssert.AreEqual("ZOE".ToCharArray(), terms.Select(t => t.Character).ToArray());
    }

    // ---- Analyze: mixed text and digits ----

    [TestMethod]
    public void Analyze_LettersAndDigits_KeepsTheTwoSumsApartAndTotalsThem()
    {
        var result = ChecksumCalculator.Analyze("GC12345");

        Assert.AreEqual(10, result.Letters.Value);  // G 7 + C 3
        Assert.AreEqual(15, result.Digits.Value);   // 1+2+3+4+5
        Assert.AreEqual(25, result.Total);
        Assert.AreEqual(7, result.TotalRoot);       // 25 -> 7
        Assert.IsTrue(result.IsMixed);
    }

    [TestMethod]
    public void Analyze_CoordinateLine_IgnoresPunctuationAndWhitespace()
    {
        var result = ChecksumCalculator.Analyze("N 49° 12.345'");

        Assert.AreEqual(14, result.Letters.Value);                  // N
        Assert.AreEqual(28, result.Digits.Value);                   // 4+9+1+2+3+4+5
        Assert.AreEqual(42, result.Total);
        Assert.AreEqual(6, result.TotalRoot);
        Assert.AreEqual(5, result.IgnoredCount);                    // two spaces, °, . and '
    }

    // ---- Analyze: accented and non-Latin letters ----

    [TestMethod]
    public void Analyze_AccentedLetters_FoldToTheirBaseLetterByDefault()
        => Assert.AreEqual(
            ChecksumCalculator.Analyze("Zlutoucky").Total,
            ChecksumCalculator.Analyze("Žluťoučký").Total);

    [TestMethod]
    public void Analyze_AccentedLetters_AreIgnoredWhenFoldingIsOff()
    {
        var result = ChecksumCalculator.Analyze("café", foldDiacritics: false);

        Assert.AreEqual(10, result.Letters.Value);  // c 3 + a 1 + f 6, é dropped
        Assert.AreEqual(3, result.Letters.Count);
        Assert.AreEqual(1, result.IgnoredCount);
    }

    [TestMethod]
    public void Analyze_Eszett_FoldsToS()
        => Assert.AreEqual(19, ChecksumCalculator.Analyze("ß").Letters.Value);

    [TestMethod]
    [DataRow("Ω")]
    [DataRow("漢字")]
    [DataRow("Привет")]
    public void Analyze_NonLatinLetters_AreIgnoredRatherThanScored(string input)
    {
        var result = ChecksumCalculator.Analyze(input);

        Assert.IsFalse(result.HasContent);
        Assert.AreEqual(input.Length, result.IgnoredCount);
    }

    // ---- Analyze: long input ----

    [TestMethod]
    public void Analyze_VeryLongInput_KeepsTheSumExactAndTruncatesOnlyTheWorking()
    {
        var result = ChecksumCalculator.Analyze(new string('9', 10_000));

        Assert.AreEqual(10_000, result.Digits.Count);
        Assert.AreEqual(90_000, result.Digits.Value);
        Assert.AreEqual(9, result.Digits.Root);
        Assert.IsTrue(result.Digits.TermsTruncated);
        Assert.AreEqual(ChecksumCalculator.MaxDetailTerms, result.Digits.Terms.Count);
    }

    [TestMethod]
    public void Analyze_ShortInput_DoesNotFlagTruncation()
    {
        var result = ChecksumCalculator.Analyze("12345");

        Assert.IsFalse(result.Digits.TermsTruncated);
        Assert.IsFalse(result.Letters.TermsTruncated);
    }

    // ---- Analyze: per-part breakdown ----

    [TestMethod]
    public void Analyze_SeparateParts_ScoresEachRunOfLettersAndDigits()
    {
        var parts = ChecksumCalculator.Analyze("GC123 ABC").Parts;

        Assert.AreEqual(2, parts.Count);
        Assert.AreEqual("GC123", parts[0].Text);
        Assert.AreEqual(10, parts[0].LetterSum);
        Assert.AreEqual(6, parts[0].DigitSum);
        Assert.AreEqual(16, parts[0].Total);
        Assert.AreEqual(7, parts[0].Root);
        Assert.AreEqual("ABC", parts[1].Text);
        Assert.AreEqual(6, parts[1].Total);
    }

    [TestMethod]
    public void Analyze_SeparateParts_SplitOnAnyIgnoredCharacter()
    {
        var parts = ChecksumCalculator.Analyze("12.345").Parts;

        Assert.AreEqual(2, parts.Count);
        Assert.AreEqual("12", parts[0].Text);
        Assert.AreEqual("345", parts[1].Text);
    }

    [TestMethod]
    public void Analyze_SeparateParts_ShowTheFoldedUpperCasedText()
    {
        var parts = ChecksumCalculator.Analyze("Žlutý").Parts;

        Assert.AreEqual(1, parts.Count);
        Assert.AreEqual("ZLUTY", parts[0].Text);
    }

    [TestMethod]
    public void Analyze_ManyParts_StopsCollectingAtTheCap()
    {
        var input = string.Join(' ', Enumerable.Repeat("12", ChecksumCalculator.MaxParts + 50));

        var result = ChecksumCalculator.Analyze(input);

        Assert.AreEqual(ChecksumCalculator.MaxParts, result.Parts.Count);
        Assert.IsTrue(result.PartsTruncated);
        // The cap must not cost accuracy: every "12" still counts.
        Assert.AreEqual(3L * (ChecksumCalculator.MaxParts + 50), result.Total);
    }

    [TestMethod]
    public void Analyze_OneUnbrokenRun_ProducesASinglePartMatchingTheTotal()
    {
        // One part carries the same numbers as the headline, so the caller can hide the list.
        var result = ChecksumCalculator.Analyze("12345");

        Assert.AreEqual(1, result.Parts.Count);
        Assert.AreEqual(result.Total, result.Parts[0].Total);
    }
}
