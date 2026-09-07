using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class WordValueTests
{
    private readonly WordValueCalculator _calculator = new();

    private static WordValueScheme Scheme(WordValueMethod method) => WordValueSchemes.For(method);

    private static int ValueOf(WordValueMethod method, char c) => Scheme(method).ValueOf(c);

    // ---- Cross sum (sum of decimal digits) ----

    [TestMethod]
    [DataRow(12345L, 15)]
    [DataRow(27L, 9)]
    [DataRow(0L, 0)]
    [DataRow(9L, 9)]
    [DataRow(100L, 1)]
    public void CrossSum_Number_SumsDigits(long value, int expected)
        => Assert.AreEqual(expected, _calculator.CrossSum(value));

    // ---- Digital root (iterate digit-sum to one digit) ----

    [TestMethod]
    [DataRow(12345L, 6)]
    [DataRow(27L, 9)]
    [DataRow(0L, 0)]
    [DataRow(9L, 9)]
    [DataRow(10L, 1)]
    [DataRow(99L, 9)]
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
    public void ReductionSteps_MultiDigit_ShowsEachStageEndingAtRoot()
        => CollectionAssert.AreEqual(new[] { 12345L, 15L, 6L }, _calculator.ReductionSteps(12345).ToArray());

    [TestMethod]
    public void ReductionSteps_SingleDigit_IsJustItself()
        => CollectionAssert.AreEqual(new[] { 7L }, _calculator.ReductionSteps(7).ToArray());

    [TestMethod]
    public void ReductionSteps_NegativeValue_ReducesTheMagnitude()
        => CollectionAssert.AreEqual(new[] { 12345L, 15L, 6L }, _calculator.ReductionSteps(-12345).ToArray());

    [TestMethod]
    public void ReductionSteps_LongMinValue_DoesNotOverflow()
        => CollectionAssert.AreEqual(new[] { 89L, 17L, 8L }, _calculator.ReductionSteps(long.MinValue).ToArray());

    [TestMethod]
    public void CrossSum_LongMinValue_DoesNotOverflow()
        => Assert.AreEqual(89, _calculator.CrossSum(long.MinValue));

    [TestMethod]
    public void DigitalRoot_LongMinValue_DoesNotOverflow()
        => Assert.AreEqual(8, _calculator.DigitalRoot(long.MinValue));

    // ---- Scheme values: standard A=1..Z=26 and the simple variants ----

    [TestMethod]
    [DataRow('a', 1)]
    [DataRow('z', 26)]
    [DataRow('A', 1)]   // case-insensitive
    [DataRow('g', 7)]
    public void Scheme_A1Z26_IsAlphabetPosition(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.A1Z26, c));

    [TestMethod]
    [DataRow('a', 0)]
    [DataRow('z', 25)]
    public void Scheme_A0Z25_IsZeroBased(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.A0Z25, c));

    [TestMethod]
    [DataRow('a', 26)]
    [DataRow('z', 1)]
    public void Scheme_A26Z1_IsReversed(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.A26Z1, c));

    [TestMethod]
    [DataRow('a', 25)]
    [DataRow('z', 0)]
    public void Scheme_A25Z0_IsReversedZeroBased(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.A25Z0, c));

    // ---- Vanity (telephone keypad) A=2..Z=9 ----

    [TestMethod]
    [DataRow('a', 2)]
    [DataRow('b', 2)]
    [DataRow('c', 2)]
    [DataRow('d', 3)]
    [DataRow('f', 3)]
    [DataRow('g', 4)]
    [DataRow('s', 7)]   // pqrs
    [DataRow('t', 8)]
    [DataRow('v', 8)]
    [DataRow('w', 9)]
    [DataRow('z', 9)]   // wxyz
    public void Scheme_Vanity_MatchesPhoneKeypad(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.Vanity, c));

    // ---- Scrabble variants ----

    [TestMethod]
    [DataRow('a', 1)]
    [DataRow('q', 10)]
    [DataRow('z', 10)]
    [DataRow('k', 5)]
    [DataRow('j', 8)]
    public void Scheme_ScrabbleEnglish_MatchesTileValues(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.ScrabbleEnglish, c));

    [TestMethod]
    [DataRow('a', 1)]
    [DataRow('z', 4)]
    [DataRow('q', 10)]
    public void Scheme_ScrabbleDutch_MatchesTileValues(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.ScrabbleDutch, c));

    [TestMethod]
    [DataRow('a', 1)]
    [DataRow('z', 3)]
    [DataRow('q', 10)]
    [DataRow('y', 10)]
    public void Scheme_ScrabbleGerman_MatchesTileValues(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.ScrabbleGerman, c));

    // ---- Table cycles ----

    [TestMethod]
    [DataRow('a', 0)]
    [DataRow('j', 9)]
    [DataRow('k', 0)]
    [DataRow('z', 5)]
    public void Scheme_Table0to9_CyclesZeroToNine(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.Table0to9, c));

    [TestMethod]
    [DataRow('a', 1)]
    [DataRow('j', 0)]
    [DataRow('k', 1)]
    [DataRow('z', 6)]
    public void Scheme_Table1to0_CyclesOneToZero(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.Table1to0, c));

    [TestMethod]
    [DataRow('a', 1)]
    [DataRow('i', 9)]
    [DataRow('j', 1)]
    [DataRow('z', 8)]
    public void Scheme_Table1to9_CyclesOneToNine(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.Table1to9, c));

    // ---- Diacritic schemes (German / Swedish) ----

    [TestMethod]
    [DataRow('a', 1)]
    [DataRow('z', 26)]
    [DataRow('ä', 27)]
    [DataRow('ö', 28)]
    [DataRow('ü', 29)]
    [DataRow('ß', 30)]
    public void Scheme_German1_ExtendsAlphabetWithUmlauts(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.German1, c));

    [TestMethod]
    [DataRow('a', 0)]
    [DataRow('z', 25)]
    [DataRow('ä', 26)]
    [DataRow('ß', 29)]
    public void Scheme_German0_ExtendsAlphabetZeroBased(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.German0, c));

    [TestMethod]
    [DataRow('a', 1)]
    [DataRow('z', 26)]
    [DataRow('å', 27)]
    [DataRow('ä', 28)]
    [DataRow('ö', 29)]
    public void Scheme_Swedish1_ExtendsAlphabetWithSwedishVowels(char c, int expected)
        => Assert.AreEqual(expected, ValueOf(WordValueMethod.Swedish1, c));

    [TestMethod]
    public void Scheme_StandardMethods_AllowDiacriticRemoval()
    {
        Assert.IsTrue(Scheme(WordValueMethod.A1Z26).AllowsDiacriticRemoval);
        Assert.IsTrue(Scheme(WordValueMethod.Vanity).AllowsDiacriticRemoval);
    }

    [TestMethod]
    public void Scheme_DiacriticMethods_DoNotAllowDiacriticRemoval()
    {
        Assert.IsFalse(Scheme(WordValueMethod.German1).AllowsDiacriticRemoval);
        Assert.IsFalse(Scheme(WordValueMethod.German0).AllowsDiacriticRemoval);
        Assert.IsFalse(Scheme(WordValueMethod.Swedish1).AllowsDiacriticRemoval);
        Assert.IsFalse(Scheme(WordValueMethod.Swedish0).AllowsDiacriticRemoval);
    }

    [TestMethod]
    public void Schemes_All_CoversEveryMethod()
        => Assert.AreEqual(Enum.GetValues<WordValueMethod>().Length, WordValueSchemes.All.Count);

    [TestMethod]
    public void Scheme_Characters_StartWithLatinAlphabet()
    {
        var chars = Scheme(WordValueMethod.A1Z26).Characters;
        Assert.AreEqual(26, chars.Count);
        Assert.AreEqual('a', chars[0]);
        Assert.AreEqual('z', chars[25]);
    }

    [TestMethod]
    public void Scheme_German1_CharactersIncludeUmlauts()
    {
        var chars = Scheme(WordValueMethod.German1).Characters;
        Assert.AreEqual(30, chars.Count);
        CollectionAssert.AreEqual(new[] { 'ä', 'ö', 'ü', 'ß' }, chars.Skip(26).ToArray());
    }

    // ---- Diacritic folding ----

    [TestMethod]
    [DataRow("café", "cafe")]
    [DataRow("ä", "a")]
    [DataRow("ö", "o")]
    [DataRow("ü", "u")]
    [DataRow("ñ", "n")]
    [DataRow("ç", "c")]
    [DataRow("å", "a")]
    [DataRow("ø", "o")]
    [DataRow("ß", "s")]
    [DataRow("æ", "ae")]
    [DataRow("Œuvre", "OEuvre")]
    public void DiacriticFolder_Fold_RemovesAccents(string input, string expected)
        => Assert.AreEqual(expected, DiacriticFolder.Fold(input));

    // ---- Analyze: terms + total + reduction ----

    [TestMethod]
    public void Analyze_SingleWord_ProducesTermsTotalAndReduction()
    {
        var result = _calculator.Analyze("GEO", Scheme(WordValueMethod.A1Z26), NumberHandling.Ignore, removeDiacritics: false);

        CollectionAssert.AreEqual(new[] { 7, 5, 15 }, result.Terms.ToArray());
        Assert.AreEqual(27, result.Total);
        CollectionAssert.AreEqual(new[] { 27L, 9L }, result.ReductionSteps.ToArray());
    }

    [TestMethod]
    public void Analyze_Cleaner_OmitsTermsForSpacesAndPunctuation()
    {
        // The space and the punctuation contribute no term (cleaner mode); only letters do.
        var result = _calculator.Analyze("ahoj df!", Scheme(WordValueMethod.A1Z26), NumberHandling.Ignore, removeDiacritics: false);

        CollectionAssert.AreEqual(new[] { 1, 8, 15, 10, 4, 6 }, result.Terms.ToArray());
        Assert.AreEqual(44, result.Total);
    }

    [TestMethod]
    public void Analyze_ZeroValuedLetter_StillProducesATerm()
    {
        // Under A=0, the letter 'a' scores 0 but is still a counted character, so it appears as a 0 term.
        var result = _calculator.Analyze("ab", Scheme(WordValueMethod.A0Z25), NumberHandling.Ignore, removeDiacritics: false);

        CollectionAssert.AreEqual(new[] { 0, 1 }, result.Terms.ToArray());
        Assert.AreEqual(1, result.Total);
    }

    // ---- Number handling ----

    [TestMethod]
    public void Analyze_DigitsInTotal_AddsEachDigitInline()
    {
        var result = _calculator.Analyze("ab12", Scheme(WordValueMethod.A1Z26), NumberHandling.DigitsInTotal, removeDiacritics: false);

        CollectionAssert.AreEqual(new[] { 1, 2, 1, 2 }, result.Terms.ToArray());
        Assert.AreEqual(6, result.Total);
        Assert.AreEqual(0, result.Numbers.Count);
    }

    [TestMethod]
    public void Analyze_NumbersSeparate_KeepsMultiDigitNumbersOutOfLetterTotal()
    {
        var result = _calculator.Analyze("ab12 c34", Scheme(WordValueMethod.A1Z26), NumberHandling.NumbersSeparate, removeDiacritics: false);

        // Letters only in the main terms/total.
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, result.Terms.ToArray());
        Assert.AreEqual(6, result.Total);
        // Multi-digit numbers summed separately.
        CollectionAssert.AreEqual(new[] { 12L, 34L }, result.Numbers.ToArray());
        Assert.AreEqual(46, result.NumberTotal);
        CollectionAssert.AreEqual(new[] { 46L, 10L, 1L }, result.NumberReductionSteps.ToArray());
    }

    [TestMethod]
    public void Analyze_Ignore_DropsDigitsEntirely()
    {
        var result = _calculator.Analyze("ab12", Scheme(WordValueMethod.A1Z26), NumberHandling.Ignore, removeDiacritics: false);

        CollectionAssert.AreEqual(new[] { 1, 2 }, result.Terms.ToArray());
        Assert.AreEqual(3, result.Total);
        Assert.AreEqual(0, result.Numbers.Count);
    }

    // ---- Diacritic removal applied during analysis ----

    [TestMethod]
    public void Analyze_RemoveDiacritics_FoldsBeforeCounting()
    {
        var result = _calculator.Analyze("é", Scheme(WordValueMethod.A1Z26), NumberHandling.Ignore, removeDiacritics: true);
        // é -> e -> 5
        CollectionAssert.AreEqual(new[] { 5 }, result.Terms.ToArray());
    }

    [TestMethod]
    public void Analyze_RemoveDiacriticsOff_AccentedLetterIsNotCounted()
    {
        var result = _calculator.Analyze("é", Scheme(WordValueMethod.A1Z26), NumberHandling.Ignore, removeDiacritics: false);
        Assert.AreEqual(0, result.Terms.Count);
        Assert.AreEqual(0, result.Total);
    }

    [TestMethod]
    public void Analyze_DiacriticScheme_IgnoresRemoveDiacriticsAndCountsUmlaut()
    {
        // German scheme keeps ä even when removeDiacritics is requested (the option is N/A there).
        var result = _calculator.Analyze("ä", Scheme(WordValueMethod.German1), NumberHandling.Ignore, removeDiacritics: true);
        CollectionAssert.AreEqual(new[] { 27 }, result.Terms.ToArray());
    }

    // ---- Separate words ----

    [TestMethod]
    public void Analyze_SplitsWordsOnWhitespaceWithPerWordReduction()
    {
        var result = _calculator.Analyze("df sdf", Scheme(WordValueMethod.A1Z26), NumberHandling.Ignore, removeDiacritics: false);

        Assert.AreEqual(2, result.Words.Count);

        Assert.AreEqual("df", result.Words[0].Text);
        CollectionAssert.AreEqual(new[] { 4, 6 }, result.Words[0].Terms.ToArray());
        CollectionAssert.AreEqual(new[] { 10L, 1L }, result.Words[0].ReductionSteps.ToArray());

        Assert.AreEqual("sdf", result.Words[1].Text);
        Assert.AreEqual(29, result.Words[1].Total);   // 19 + 4 + 6
        CollectionAssert.AreEqual(new[] { 29L, 11L, 2L }, result.Words[1].ReductionSteps.ToArray());
    }

    [TestMethod]
    public void Analyze_PerWordTotals_SumToTheGrandTotal()
    {
        var result = _calculator.Analyze("abc geo def", Scheme(WordValueMethod.A1Z26), NumberHandling.DigitsInTotal, removeDiacritics: false);
        Assert.AreEqual(result.Total, result.Words.Sum(w => w.Total));
    }

    [TestMethod]
    public void Analyze_EmptyInput_HasNoContent()
    {
        var result = _calculator.Analyze("   ", Scheme(WordValueMethod.A1Z26), NumberHandling.DigitsInTotal, removeDiacritics: true);

        Assert.AreEqual(0, result.Words.Count);
        Assert.AreEqual(0, result.Total);
        Assert.IsFalse(result.HasContent);
    }

    [TestMethod]
    public void Analyze_VanityWord_UsesPhoneKeypadValues()
    {
        // "abc" on the keypad = 2 + 2 + 2 = 6
        var result = _calculator.Analyze("abc", Scheme(WordValueMethod.Vanity), NumberHandling.Ignore, removeDiacritics: false);
        Assert.AreEqual(6, result.Total);
    }
}
