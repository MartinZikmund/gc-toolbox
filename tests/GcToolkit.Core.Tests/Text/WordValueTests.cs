using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class WordValueTests
{
    private readonly WordValueCalculator _calculator = new();

    // ---- Letter value of a single character (A=1 .. Z=26) ----

    [DataTestMethod]
    [DataRow('A', 1)]
    [DataRow('Z', 26)]
    [DataRow('a', 1)]
    [DataRow('z', 26)]
    [DataRow('G', 7)]
    public void LetterValue_Letter_IsAlphabetPosition(char c, int expected)
        => Assert.AreEqual(expected, _calculator.LetterValue(c));

    [DataTestMethod]
    [DataRow('1')]
    [DataRow(' ')]
    [DataRow('!')]
    [DataRow('-')]
    public void LetterValue_NonLetter_IsZero(char c)
        => Assert.AreEqual(0, _calculator.LetterValue(c));

    // ---- Word value (letter sum) ----

    [DataTestMethod]
    [DataRow("GEO", 27)]        // 7 + 5 + 15
    [DataRow("ABC", 6)]         // 1 + 2 + 3
    [DataRow("geo", 27)]        // case-insensitive
    [DataRow("A", 1)]
    [DataRow("Z", 26)]
    public void WordValue_Word_SumsLetters(string word, int expected)
        => Assert.AreEqual(expected, _calculator.WordValue(word));

    [TestMethod]
    public void WordValue_IgnoresNonLetters()
        => Assert.AreEqual(27, _calculator.WordValue("G.E-O!"));   // same as GEO

    [DataTestMethod]
    [DataRow("")]
    [DataRow("123")]
    [DataRow("   ")]
    [DataRow("!!!")]
    public void WordValue_NoLetters_IsZero(string word)
        => Assert.AreEqual(0, _calculator.WordValue(word));

    // ---- Cross sum (sum of decimal digits) ----

    [DataTestMethod]
    [DataRow(12345L, 15)]
    [DataRow(27L, 9)]
    [DataRow(0L, 0)]
    [DataRow(9L, 9)]
    [DataRow(100L, 1)]
    public void CrossSum_Number_SumsDigits(long value, int expected)
        => Assert.AreEqual(expected, _calculator.CrossSum(value));

    // ---- Digital root (iterate digit-sum to one digit) ----

    [DataTestMethod]
    [DataRow(12345L, 6)]   // 1+2+3+4+5=15 -> 1+5=6
    [DataRow(27L, 9)]      // 2+7=9
    [DataRow(0L, 0)]       // 0 stays 0
    [DataRow(9L, 9)]
    [DataRow(10L, 1)]
    [DataRow(99L, 9)]      // 18 -> 9
    [DataRow(123456789L, 9)]
    public void DigitalRoot_Number_ReducesToSingleDigit(long value, int expected)
        => Assert.AreEqual(expected, _calculator.DigitalRoot(value));

    [TestMethod]
    public void DigitalRoot_PositiveNumber_IsAlwaysBetween1And9()
    {
        for (long n = 1; n <= 2000; n++)
        {
            var root = _calculator.DigitalRoot(n);
            Assert.IsTrue(root is >= 1 and <= 9, $"digital root of {n} was {root}");
        }
    }

    [TestMethod]
    public void DigitalRoot_Zero_IsZero()
        => Assert.AreEqual(0, _calculator.DigitalRoot(0));

    // ---- Reduction steps (the chain shown to the user) ----

    [TestMethod]
    public void ReductionSteps_MultiDigit_ShowsEachStageEndingAtRoot()
    {
        // 12345 -> 15 -> 6
        CollectionAssert.AreEqual(new[] { 12345L, 15L, 6L }, _calculator.ReductionSteps(12345).ToArray());
    }

    [TestMethod]
    public void ReductionSteps_SingleDigit_IsJustItself()
        => CollectionAssert.AreEqual(new[] { 7L }, _calculator.ReductionSteps(7).ToArray());

    [TestMethod]
    public void ReductionSteps_LastStepEqualsDigitalRoot()
    {
        var steps = _calculator.ReductionSteps(999999);
        Assert.AreEqual(_calculator.DigitalRoot(999999), (int)steps[^1]);
    }

    // ---- Analyze: the end-to-end result used by the ViewModel ----

    [TestMethod]
    public void Analyze_SingleWord_ProducesWordTotalAndDigitalRoot()
    {
        var result = _calculator.Analyze("GEO", countNumbers: false);

        Assert.AreEqual(1, result.Words.Count);
        Assert.AreEqual("GEO", result.Words[0].Text);
        Assert.AreEqual(27, result.Words[0].Value);
        Assert.AreEqual(27, result.LetterTotal);
        Assert.AreEqual(9, result.LetterCrossSum);
        Assert.AreEqual(9, result.LetterDigitalRoot);
    }

    [TestMethod]
    public void Analyze_MultipleWords_TotalsAddUp()
    {
        var result = _calculator.Analyze("ABC GEO", countNumbers: false);

        Assert.AreEqual(2, result.Words.Count);
        Assert.AreEqual(6, result.Words[0].Value);   // ABC
        Assert.AreEqual(27, result.Words[1].Value);  // GEO
        Assert.AreEqual(33, result.LetterTotal);     // 6 + 27
        Assert.AreEqual(6, result.LetterCrossSum);   // 3 + 3
        Assert.AreEqual(6, result.LetterDigitalRoot);
    }

    [TestMethod]
    public void Analyze_SplitsOnSpacesAndNewlines()
    {
        var result = _calculator.Analyze("ABC\nGEO  DEF", countNumbers: false);

        Assert.AreEqual(3, result.Words.Count);
        CollectionAssert.AreEqual(
            new[] { "ABC", "GEO", "DEF" },
            result.Words.Select(w => w.Text).ToArray());
    }

    [TestMethod]
    public void Analyze_EmptyInput_HasNoWordsAndZeroTotals()
    {
        var result = _calculator.Analyze("   ", countNumbers: true);

        Assert.AreEqual(0, result.Words.Count);
        Assert.AreEqual(0, result.LetterTotal);
        Assert.AreEqual(0, result.LetterDigitalRoot);
        Assert.IsFalse(result.HasContent);
        Assert.AreEqual(0, result.Numbers.Count);
    }

    [TestMethod]
    public void Analyze_WordWithPunctuation_CountsOnlyLetters()
    {
        var result = _calculator.Analyze("GEO-cache!", countNumbers: false);

        Assert.AreEqual(1, result.Words.Count);
        // G E O c a c h e = 7+5+15+3+1+3+8+5
        Assert.AreEqual(47, result.Words[0].Value);
        Assert.AreEqual(47, result.LetterTotal);
    }

    // ---- Numbers in the input ----

    [TestMethod]
    public void Analyze_CountNumbers_AddsNumericTokensToTheNumericTotal()
    {
        var result = _calculator.Analyze("N 49 13.456", countNumbers: true);

        // Three pure-digit groups are found: 49, 13 and 456.
        CollectionAssert.AreEqual(new[] { 49L, 13L, 456L }, result.Numbers.ToArray());
        Assert.AreEqual(49 + 13 + 456, result.NumberTotal);
        Assert.IsTrue(result.HasNumbers);
        Assert.AreEqual(_calculator.DigitalRoot(49 + 13 + 456), result.NumberDigitalRoot);
    }

    [TestMethod]
    public void Analyze_CountNumbersOff_IgnoresNumbers()
    {
        var result = _calculator.Analyze("AB 12 CD", countNumbers: false);

        Assert.AreEqual(0, result.Numbers.Count);
        Assert.AreEqual(0, result.NumberTotal);
        Assert.IsFalse(result.HasNumbers);
        // Letters still counted: AB=3, CD=7
        Assert.AreEqual(10, result.LetterTotal);
    }

    [TestMethod]
    public void Analyze_DigitsAttachedToLetters_LettersCountedWordsSplitFromNumbers()
    {
        // "GC123" -> letters G,C contribute; "123" is the number when counting numbers.
        var result = _calculator.Analyze("GC123", countNumbers: true);

        Assert.AreEqual(7 + 3, result.LetterTotal);  // G=7, C=3
        CollectionAssert.AreEqual(new[] { 123L }, result.Numbers.ToArray());
    }

    [TestMethod]
    public void Analyze_HasContent_TrueWhenLettersPresent()
    {
        var result = _calculator.Analyze("hello", countNumbers: false);
        Assert.IsTrue(result.HasContent);
    }
}
