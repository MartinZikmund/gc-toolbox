using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class AlphabetNumbersTests
{
    private readonly AlphabetNumbers _codec = new();

    // ---- Letters -> numbers ----

    [DataTestMethod]
    [DataRow("ABC", "1 2 3")]
    [DataRow("abc", "1 2 3")]          // case-insensitive
    [DataRow("A", "1")]
    [DataRow("Z", "26")]
    [DataRow("AZ", "1 26")]
    public void LettersToNumbers_BasicLetters_MapsAOneToZTwentySix(string text, string expected)
        => Assert.AreEqual(expected, _codec.LettersToNumbers(text));

    [TestMethod]
    public void LettersToNumbers_MultipleWords_PreservesWordBoundaryWithSpace()
        => Assert.AreEqual("8 9   3 1 20", _codec.LettersToNumbers("HI CAT"));

    [TestMethod]
    public void LettersToNumbers_CustomSeparator_JoinsGroupsWithIt()
        => Assert.AreEqual("8-9", _codec.LettersToNumbers("HI", new AlphabetNumberOptions { Separator = "-" }));

    [TestMethod]
    public void LettersToNumbers_StripNonLetters_DropsPunctuationByDefault()
        => Assert.AreEqual("8 9", _codec.LettersToNumbers("H.I!"));

    [TestMethod]
    public void LettersToNumbers_KeepNonLetters_EmitsThemVerbatim()
        => Assert.AreEqual("8.9!", _codec.LettersToNumbers("H.I!", new AlphabetNumberOptions { KeepNonLetters = true }));

    [TestMethod]
    public void LettersToNumbers_KeepNonLetters_KeepsSeparatorOnlyBetweenAdjacentNumbers()
        // "HI, CAT" with keep: numbers within a word still separated, punctuation/space verbatim.
        => Assert.AreEqual("8 9, 3 1 20", _codec.LettersToNumbers("HI, CAT", new AlphabetNumberOptions { KeepNonLetters = true }));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void LettersToNumbers_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _codec.LettersToNumbers(text));

    [TestMethod]
    public void LettersToNumbers_FullAlphabet_ProducesOneThroughTwentySix()
        => Assert.AreEqual(
            "1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26",
            _codec.LettersToNumbers("ABCDEFGHIJKLMNOPQRSTUVWXYZ"));

    // ---- Numbers -> letters ----

    [DataTestMethod]
    [DataRow("1 2 3", "ABC")]
    [DataRow("26", "Z")]
    [DataRow("1", "A")]
    [DataRow("8 9", "HI")]
    public void NumbersToLetters_InRange_MapsToUpperLetters(string text, string expected)
        => Assert.AreEqual(expected, _codec.NumbersToLetters(text));

    [TestMethod]
    public void NumbersToLetters_LowerCaseOption_ProducesLowerLetters()
        => Assert.AreEqual("abc", _codec.NumbersToLetters("1 2 3", new AlphabetNumberOptions { UpperCase = false }));

    [TestMethod]
    public void NumbersToLetters_WordBoundaries_PreservedAsSpace()
        // Two groups separated by a doubled separator (word gap) round-trips to a space.
        => Assert.AreEqual("HI CAT", _codec.NumbersToLetters("8 9   3 1 20"));

    [TestMethod]
    public void NumbersToLetters_CustomSeparator_SplitsOnIt()
        => Assert.AreEqual("HI", _codec.NumbersToLetters("8-9", new AlphabetNumberOptions { Separator = "-" }));

    [TestMethod]
    public void NumbersToLetters_OutOfRangeWithoutWrap_MarksWithHash()
        => Assert.AreEqual("A#", _codec.NumbersToLetters("1 27"));

    [DataTestMethod]
    [DataRow("27", "A")]               // 27 wraps to 1 -> A
    [DataRow("28", "B")]
    [DataRow("52", "Z")]               // 52 wraps to 26 -> Z
    [DataRow("0", "Z")]                // 0 -> 1 + ((0-1) mod 26) = 26 -> Z
    [DataRow("53", "A")]               // 53 -> 1
    public void NumbersToLetters_OutOfRangeWithWrap_WrapsModTwentySix(string text, string expected)
        => Assert.AreEqual(expected, _codec.NumbersToLetters(text, new AlphabetNumberOptions { WrapModulo = true }));

    [TestMethod]
    public void NumbersToLetters_NegativeWithWrap_WrapsIntoRange()
        // -1 -> 1 + ((-1-1) mod 26) = 1 + 24 = 25 -> Y
        => Assert.AreEqual("Y", _codec.NumbersToLetters("-1", new AlphabetNumberOptions { WrapModulo = true }));

    [TestMethod]
    public void NumbersToLetters_NonNumericToken_MarkedWithHash()
        => Assert.AreEqual("A#C", _codec.NumbersToLetters("1 foo 3"));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void NumbersToLetters_EmptyOrWhitespace_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _codec.NumbersToLetters(text));

    [TestMethod]
    public void NumbersToLetters_CommaSeparated_AlsoSplitsOnWhitespaceFallback()
        // A comma separator with surrounding spaces still groups correctly.
        => Assert.AreEqual("ABC", _codec.NumbersToLetters("1, 2, 3", new AlphabetNumberOptions { Separator = "," }));

    // ---- Round trips ----

    [TestMethod]
    public void RoundTrip_LettersToNumbersAndBack_RecoversUpperText()
    {
        const string plain = "GEOCACHE";
        var numbers = _codec.LettersToNumbers(plain);
        Assert.AreEqual(plain, _codec.NumbersToLetters(numbers));
    }

    [TestMethod]
    public void RoundTrip_MultiWord_PreservesWordBoundaries()
    {
        const string plain = "THE QUICK BROWN FOX";
        var numbers = _codec.LettersToNumbers(plain);
        Assert.AreEqual(plain, _codec.NumbersToLetters(numbers));
    }
}
