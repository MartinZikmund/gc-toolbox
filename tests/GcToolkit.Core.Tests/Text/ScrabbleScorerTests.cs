using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class ScrabbleScorerTests
{
    private readonly ScrabbleScorer _scorer = new();

    // ---- Known English vectors (from the issue) ----

    [TestMethod]
    [DataRow("QUIZ", 22)]
    [DataRow("HELLO", 8)]
    [DataRow("AAA", 3)]
    [DataRow("quiz", 22)] // case-insensitive
    public void Score_EnglishKnownWords_ProducesExpectedTotal(string word, int expected)
        => Assert.AreEqual(expected, _scorer.Score(word, LetterValueSystem.ScrabbleEnglish).GrandTotal);

    [TestMethod]
    public void Score_Quiz_PerLetterBreakdownListsEachTileValue()
    {
        var word = _scorer.ScoreWord("QUIZ", LetterValueSystem.ScrabbleEnglish);

        CollectionAssert.AreEqual(
            new[] { 10, 1, 1, 10 },
            word.Letters.Select(l => l.Value).ToArray());
        Assert.AreEqual("Q=10 + U=1 + I=1 + Z=10 = 22", word.Breakdown);
    }

    // ---- Reductions ----

    [TestMethod]
    public void Reduce_Total22_DigitalRootIsFour()
        => Assert.AreEqual(4, ScrabbleScorer.Reduce(22).DigitalRoot);

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(9, 9)]
    [DataRow(18, 9)]
    [DataRow(22, 4)]
    [DataRow(123, 6)]
    public void DigitalRootOf_Cases_ReducesToSingleDigit(int value, int expected)
        => Assert.AreEqual(expected, ScrabbleScorer.DigitalRootOf(value));

    [TestMethod]
    [DataRow(22, 4)]
    [DataRow(123, 6)]
    [DataRow(100, 1)]
    public void DigitSumOf_Cases_SumsDigitsOnce(int value, int expected)
        => Assert.AreEqual(expected, ScrabbleScorer.DigitSumOf(value));

    [TestMethod]
    public void Reduce_FullReductionSet_ComputesEachField()
    {
        var r = ScrabbleScorer.Reduce(123);

        Assert.AreEqual(123, r.Total);
        Assert.AreEqual(6, r.DigitalRoot);
        Assert.AreEqual(6, r.DigitSum);
        Assert.AreEqual(123 % 26, r.Mod26);
        Assert.AreEqual(3, r.Mod10);
        Assert.AreEqual(321, r.ReversedTotal);
    }

    [TestMethod]
    public void Reduce_ReversedTotal_DropsTrailingZeros()
        => Assert.AreEqual(21, ScrabbleScorer.Reduce(120).ReversedTotal);

    // ---- Non-letters ignored ----

    [TestMethod]
    public void Score_NonLetters_AreIgnored()
    {
        // "A-1 B!" => letters A and B only => 1 + 3 = 4 (two words).
        var score = _scorer.Score("A-1 B!", LetterValueSystem.ScrabbleEnglish);

        Assert.AreEqual(4, score.GrandTotal);
        Assert.AreEqual(2, score.Words.Count);
        Assert.AreEqual(1, score.Words[0].Total);
        Assert.AreEqual(3, score.Words[1].Total);
    }

    [TestMethod]
    public void Score_DigitsOnly_ScoreZeroAndAreNotLetters()
        => Assert.AreEqual(0, _scorer.Score("12345", LetterValueSystem.ScrabbleEnglish).GrandTotal);

    // ---- Per-word AND whole-text totals ----

    [TestMethod]
    public void Score_MultipleWords_ProducesPerWordAndGrandTotal()
    {
        var score = _scorer.Score("QUIZ HELLO", LetterValueSystem.ScrabbleEnglish);

        Assert.AreEqual(2, score.Words.Count);
        Assert.AreEqual(22, score.Words[0].Total);
        Assert.AreEqual(8, score.Words[1].Total);
        Assert.AreEqual(30, score.GrandTotal);
    }

    // ---- Empty / whitespace ----

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void Score_EmptyInput_YieldsNoWordsAndZeroTotal(string? input)
    {
        var score = _scorer.Score(input, LetterValueSystem.ScrabbleEnglish);

        Assert.AreEqual(0, score.Words.Count);
        Assert.AreEqual(0, score.GrandTotal);
        Assert.IsFalse(score.HasUnknown);
    }

    // ---- Alphabet-position systems ----

    [TestMethod]
    [DataRow(LetterValueSystem.A1Z26, "ABZ", 1 + 2 + 26)]
    [DataRow(LetterValueSystem.A0Z25, "ABZ", 0 + 1 + 25)]
    [DataRow(LetterValueSystem.ReversedA26Z1, "ABZ", 26 + 25 + 1)]
    [DataRow(LetterValueSystem.ReversedA25Z0, "ABZ", 25 + 24 + 0)]
    public void Score_PositionalSystems_UseAlphabetIndex(LetterValueSystem system, string word, int expected)
        => Assert.AreEqual(expected, _scorer.Score(word, system).GrandTotal);

    [TestMethod]
    public void Score_PhoneKeypad_UsesVanityDigits()
    {
        // A=2, D=3, G=4, J=5, M=6, P=7, T=8, W=9.
        Assert.AreEqual(2, _scorer.ScoreWord("A", LetterValueSystem.PhoneKeypad).Total);
        Assert.AreEqual(7, _scorer.ScoreWord("S", LetterValueSystem.PhoneKeypad).Total); // P,Q,R,S = 7
        Assert.AreEqual(9, _scorer.ScoreWord("Z", LetterValueSystem.PhoneKeypad).Total); // W,X,Y,Z = 9
    }

    [TestMethod]
    public void GetTable_PhoneKeypad_MapsEachLetterToItsKeypadDigit()
    {
        var table = ScrabbleScorer.GetTable(LetterValueSystem.PhoneKeypad);

        int[] expected =
        [
            2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 9, 9, 9, 9,
        ];
        CollectionAssert.AreEqual(expected, table.ToArray());
    }

    // ---- Language Scrabble tables ----

    [TestMethod]
    public void GetTable_English_MatchesCanonicalTileValues()
    {
        int[] expected =
        [
            1, 3, 3, 2, 1, 4, 2, 4, 1, 8, 5, 1, 3, 1, 1, 3, 10, 1, 1, 1, 1, 4, 4, 8, 4, 10,
        ];
        CollectionAssert.AreEqual(expected, ScrabbleScorer.GetTable(LetterValueSystem.ScrabbleEnglish).ToArray());
    }

    [TestMethod]
    [DataRow(LetterValueSystem.ScrabbleGerman, 'Y', 10)]   // German Y is worth 10
    [DataRow(LetterValueSystem.ScrabbleFrench, 'K', 10)]   // French K is worth 10
    [DataRow(LetterValueSystem.ScrabbleFrench, 'Q', 8)]    // French Q is worth 8 (not 10)
    [DataRow(LetterValueSystem.ScrabbleDutch, 'H', 2)]     // Dutch H is worth 2
    [DataRow(LetterValueSystem.ScrabbleSpanish, 'Q', 5)]   // Spanish Q is worth 5
    [DataRow(LetterValueSystem.ScrabbleItalian, 'G', 8)]   // Italian G is worth 8
    [DataRow(LetterValueSystem.ScrabbleItalian, 'C', 2)]   // Italian C is worth 2
    public void GetTable_LanguageVariants_DivergeFromEnglish(LetterValueSystem system, char letter, int expected)
        => Assert.AreEqual(expected, ScrabbleScorer.GetTable(system)[letter - 'A']);

    [TestMethod]
    public void ScoreWord_ItalianLetterWithoutTile_ScoresZero()
    {
        // Italian has no W tile — it scores 0 (still a known position, value 0).
        var word = _scorer.ScoreWord("W", LetterValueSystem.ScrabbleItalian);

        Assert.AreEqual(0, word.Total);
        Assert.IsTrue(word.Letters[0].HasValue); // W is a position in the A-Z table, just valued 0.
    }

    // ---- Custom table ----

    [TestMethod]
    public void Score_CustomTable_UsesSuppliedValues()
    {
        // Every letter worth 2.
        int[] table = [.. Enumerable.Repeat(2, 26)];

        Assert.AreEqual(6, _scorer.Score("ABC", table).GrandTotal);
    }

    [TestMethod]
    [DataRow(25)]
    [DataRow(27)]
    public void Score_CustomTableWrongLength_Throws(int length)
    {
        int[] table = [.. Enumerable.Repeat(1, length)];

        Assert.ThrowsExactly<ArgumentException>(() => _scorer.Score("AB", table));
    }

    // ---- Unknown / diacritic letters ----

    [TestMethod]
    public void Score_AccentedLetter_FoldsToBaseAndScores()
    {
        // "É" folds to E => value 1 in English.
        var word = _scorer.ScoreWord("É", LetterValueSystem.ScrabbleEnglish);

        Assert.AreEqual(1, word.Total);
        Assert.IsTrue(word.Letters[0].HasValue);
        Assert.IsFalse(word.HasUnknown);
    }

    [TestMethod]
    public void Score_LetterWithNoMapping_IsFlaggedAsUnknown()
    {
        // A Greek letter is a letter but has no A-Z mapping.
        var word = _scorer.ScoreWord("Ω", LetterValueSystem.ScrabbleEnglish);

        Assert.IsFalse(word.Letters[0].HasValue);
        Assert.IsTrue(word.HasUnknown);
        Assert.AreEqual(0, word.Total);
    }

    [TestMethod]
    public void Score_TextWithUnknownLetter_PropagatesHasUnknown()
    {
        var score = _scorer.Score("HELLO Ω", LetterValueSystem.ScrabbleEnglish);

        Assert.IsTrue(score.HasUnknown);
        Assert.AreEqual(8, score.GrandTotal); // omega contributes 0
    }

    // ---- Batch (per line) ----

    [TestMethod]
    public void ScoreBatch_MultipleLines_ScoresEachSeparately()
    {
        var results = _scorer.ScoreBatch("QUIZ\nHELLO\nAAA", LetterValueSystem.ScrabbleEnglish);

        Assert.AreEqual(3, results.Count);
        Assert.AreEqual(22, results[0].GrandTotal);
        Assert.AreEqual(8, results[1].GrandTotal);
        Assert.AreEqual(3, results[2].GrandTotal);
    }

    [TestMethod]
    public void ScoreBatch_BlankLines_AreSkipped()
    {
        var results = _scorer.ScoreBatch("QUIZ\n\n\nHELLO", LetterValueSystem.ScrabbleEnglish);

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public void ScoreBatch_EmptyInput_ReturnsEmpty()
        => Assert.AreEqual(0, _scorer.ScoreBatch("", LetterValueSystem.ScrabbleEnglish).Count);

    // ---- Distribution reference ----

    [TestMethod]
    public void EnglishDistribution_HasTwelveEs()
        => Assert.AreEqual(12, ScrabbleScorer.EnglishDistribution['E' - 'A']);

    [TestMethod]
    public void GetTable_Custom_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ScrabbleScorer.GetTable(LetterValueSystem.Custom));
}
