using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class AlphabetNumbersTests
{
    private readonly AlphabetNumbers _codec = new();

    // ---- Letters -> numbers ----

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    // ---- Methods: letters -> numbers ----

    [TestMethod]
    [DataRow(AlphabetMethod.A1Z26, "A", "1")]
    [DataRow(AlphabetMethod.A1Z26, "Z", "26")]
    [DataRow(AlphabetMethod.A0Z25, "A", "0")]
    [DataRow(AlphabetMethod.A0Z25, "Z", "25")]
    [DataRow(AlphabetMethod.A26Z1, "A", "26")]
    [DataRow(AlphabetMethod.A26Z1, "Z", "1")]
    [DataRow(AlphabetMethod.A25Z0, "A", "25")]
    [DataRow(AlphabetMethod.A25Z0, "Z", "0")]
    public void LettersToNumbers_Method_MapsPerMethod(AlphabetMethod method, string text, string expected)
        => Assert.AreEqual(expected, _codec.LettersToNumbers(text, new AlphabetNumberOptions { Method = method }));

    // ---- Methods: numbers -> letters ----

    [TestMethod]
    [DataRow(AlphabetMethod.A0Z25, "0", "a")]
    [DataRow(AlphabetMethod.A0Z25, "25", "z")]
    [DataRow(AlphabetMethod.A26Z1, "26", "a")]
    [DataRow(AlphabetMethod.A26Z1, "1", "z")]
    [DataRow(AlphabetMethod.A25Z0, "0", "z")]
    [DataRow(AlphabetMethod.A25Z0, "25", "a")]
    public void NumbersToLetters_Method_MapsPerMethod(AlphabetMethod method, string text, string expected)
        => Assert.AreEqual(expected, _codec.NumbersToLetters(text, new AlphabetNumberOptions { Method = method, UpperCase = false }));

    // ---- German / Nordic extended characters ----

    [TestMethod]
    [DataRow("ä", "27")]
    [DataRow("ö", "28")]
    [DataRow("ü", "29")]
    [DataRow("ß", "30")]
    [DataRow("Ä", "27")]   // case-insensitive
    public void LettersToNumbers_German_MapsExtendedChars(string text, string expected)
        => Assert.AreEqual(expected, _codec.LettersToNumbers(text, new AlphabetNumberOptions { Method = AlphabetMethod.A1Z26German }));

    [TestMethod]
    [DataRow("27", "ä")]
    [DataRow("30", "ß")]
    public void NumbersToLetters_German_DecodesExtendedChars(string text, string expected)
        => Assert.AreEqual(expected, _codec.NumbersToLetters(text, new AlphabetNumberOptions { Method = AlphabetMethod.A1Z26German, UpperCase = false }));

    [TestMethod]
    public void NumbersToLetters_GermanUpperCase_EszettStaysEszett()
        => Assert.AreEqual("ß", _codec.NumbersToLetters("30", new AlphabetNumberOptions { Method = AlphabetMethod.A1Z26German, UpperCase = true }));

    [TestMethod]
    [DataRow("å", "27")]
    [DataRow("ä", "28")]
    [DataRow("ö", "29")]
    public void LettersToNumbers_Nordic_MapsExtendedChars(string text, string expected)
        => Assert.AreEqual(expected, _codec.LettersToNumbers(text, new AlphabetNumberOptions { Method = AlphabetMethod.A1Z26Nordic }));

    [TestMethod]
    public void LettersToNumbers_LatinMethod_DropsExtendedChars()
        // ä is not a letter under a plain Latin method, so it is dropped like other non-letters.
        => Assert.AreEqual("1 2", _codec.LettersToNumbers("AäB", new AlphabetNumberOptions { Method = AlphabetMethod.A1Z26 }));

    // ---- Replace unknown ----

    [TestMethod]
    public void NumbersToLetters_CustomUnknownReplacement_UsesIt()
        => Assert.AreEqual("A?C", _codec.NumbersToLetters("1 99 3", new AlphabetNumberOptions { UnknownReplacement = "?" }));

    [TestMethod]
    public void NumbersToLetters_EmptyUnknownReplacement_DropsToken()
        => Assert.AreEqual("AC", _codec.NumbersToLetters("1 99 3", new AlphabetNumberOptions { UnknownReplacement = "" }));

    [TestMethod]
    public void NumbersToLetters_KeepOriginalUnknown_KeepsNumericToken()
        => Assert.AreEqual("A99C", _codec.NumbersToLetters("1 99 3", new AlphabetNumberOptions { KeepOriginalUnknown = true }));

    [TestMethod]
    public void NumbersToLetters_KeepOriginalUnknown_KeepsNonNumericToken()
        => Assert.AreEqual("AfooC", _codec.NumbersToLetters("1 foo 3", new AlphabetNumberOptions { KeepOriginalUnknown = true }));

    // ---- Method-aware wrap ----

    [TestMethod]
    public void NumbersToLetters_GermanWrap_WrapsIntoThirtyRange()
        // German range is 1–30, so 31 wraps to 1 -> a.
        => Assert.AreEqual("a", _codec.NumbersToLetters("31", new AlphabetNumberOptions { Method = AlphabetMethod.A1Z26German, WrapModulo = true, UpperCase = false }));

    [TestMethod]
    public void NumbersToLetters_ZeroBasedWrap_WrapsIntoZeroToTwentyFive()
        // A=0…Z=25 range is 0–25, so 26 wraps to 0 -> a.
        => Assert.AreEqual("a", _codec.NumbersToLetters("26", new AlphabetNumberOptions { Method = AlphabetMethod.A0Z25, WrapModulo = true, UpperCase = false }));

    // ---- Conversion table ----

    [TestMethod]
    public void AlphabetMethods_A1Z26_HasTwentySixEntriesInOrder()
    {
        var entries = AlphabetMethods.Get(AlphabetMethod.A1Z26).Entries;
        Assert.AreEqual(26, entries.Count);
        Assert.AreEqual("a", entries[0].Character);
        Assert.AreEqual(1, entries[0].Value);
        Assert.AreEqual("z", entries[25].Character);
        Assert.AreEqual(26, entries[25].Value);
    }

    [TestMethod]
    public void AlphabetMethods_German_HasThirtyEntriesEndingWithEszett()
    {
        var entries = AlphabetMethods.Get(AlphabetMethod.A1Z26German).Entries;
        Assert.AreEqual(30, entries.Count);
        Assert.AreEqual("ß", entries[29].Character);
        Assert.AreEqual(30, entries[29].Value);
    }

    [TestMethod]
    public void AlphabetEntry_ToString_IsCharacterAndValue_NotTheRecordForm()
        // A table tile with no explicit automation name announces ToString().
        => Assert.AreEqual("a = 1", new AlphabetEntry("a", 1).ToString());

    [TestMethod]
    public void AlphabetMethods_All_ExposesEightMethodsWithLabels()
    {
        Assert.AreEqual(8, AlphabetMethods.All.Count);
        Assert.AreEqual("A=1 ... Z=26", AlphabetMethods.Get(AlphabetMethod.A1Z26).Label);
        Assert.AreEqual("A=1 ... Z=26, ä=27, ö=28, ü=29, ß=30", AlphabetMethods.Get(AlphabetMethod.A1Z26German).Label);
    }
}
