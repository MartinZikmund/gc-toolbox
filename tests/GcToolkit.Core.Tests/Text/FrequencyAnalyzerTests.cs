using System.Linq;
using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class FrequencyAnalyzerTests
{
    private readonly FrequencyAnalyzer _analyzer = new();

    // ---- Totals ----

    [TestMethod]
    public void Analyze_Hello_CountsLettersAndTotal()
    {
        var result = _analyzer.Analyze("HELLO");
        Assert.AreEqual(5, result.Totals.Letters);
        Assert.AreEqual(5, result.Totals.Characters);
        Assert.AreEqual(0, result.Totals.Digits);
        Assert.AreEqual(0, result.Totals.Spaces);
        Assert.AreEqual(1, result.Totals.Words);
    }

    [TestMethod]
    public void Analyze_MixedText_CountsEveryCategory()
    {
        // "AB 12!\nx" -> letters A,B,x=3 ; digits 1,2=2 ; symbol !=1 ; space=1 ; newline excluded from chars-count-as-symbol
        var result = _analyzer.Analyze("AB 12!\nx");
        Assert.AreEqual(3, result.Totals.Letters);
        Assert.AreEqual(2, result.Totals.Digits);
        Assert.AreEqual(1, result.Totals.Symbols);
        Assert.AreEqual(1, result.Totals.Spaces);
        Assert.AreEqual(2, result.Totals.Lines);
        Assert.AreEqual(3, result.Totals.Words);
    }

    [TestMethod]
    public void Analyze_Words_CountsWhitespaceSeparatedTokens()
    {
        var result = _analyzer.Analyze("the quick  brown\nfox");
        Assert.AreEqual(4, result.Totals.Words);
    }

    [TestMethod]
    public void Analyze_Empty_ReportsAllZero()
    {
        var result = _analyzer.Analyze("");
        Assert.AreEqual(0, result.Totals.Characters);
        Assert.AreEqual(0, result.Totals.Words);
        Assert.AreEqual(0, result.Totals.Lines);
        Assert.AreEqual(0, result.CharacterFrequencies.Count);
    }

    [TestMethod]
    public void Analyze_WhitespaceOnly_HasNoWordsButCountsSpaces()
    {
        var result = _analyzer.Analyze("   ");
        Assert.AreEqual(0, result.Totals.Words);
        Assert.AreEqual(3, result.Totals.Spaces);
        Assert.AreEqual(0, result.Totals.Letters);
    }

    [TestMethod]
    public void Analyze_UniqueCharacters_CountsDistinctFolded()
    {
        // case folded by default: H,E,L,O = 4 unique letters
        var result = _analyzer.Analyze("HELLO");
        Assert.AreEqual(4, result.Totals.UniqueCharacters);
    }

    // ---- Per-character frequency ----

    [TestMethod]
    public void Analyze_Hello_FoldedFrequencyTable()
    {
        var freq = _analyzer.Analyze("HELLO").CharacterFrequencies
            .ToDictionary(e => e.Display, e => e.Count);
        Assert.AreEqual(1, freq["H"]);
        Assert.AreEqual(1, freq["E"]);
        Assert.AreEqual(2, freq["L"]);
        Assert.AreEqual(1, freq["O"]);
        Assert.AreEqual(4, freq.Count);
    }

    [TestMethod]
    public void Analyze_Percentages_SumToHundredOverCountedCharacters()
    {
        var result = _analyzer.Analyze("HELLO");
        var sum = result.CharacterFrequencies.Sum(e => e.Percentage);
        Assert.AreEqual(100.0, sum, 0.0001);
    }

    [TestMethod]
    public void Analyze_CaseSensitive_SplitsUpperAndLower()
    {
        var options = new FrequencyOptions { CaseSensitive = true };
        var freq = _analyzer.Analyze("Aa", options).CharacterFrequencies
            .ToDictionary(e => e.Display, e => e.Count);
        Assert.AreEqual(1, freq["A"]);
        Assert.AreEqual(1, freq["a"]);
        Assert.AreEqual(2, freq.Count);
    }

    [TestMethod]
    public void Analyze_CaseFolded_CombinesUpperAndLower()
    {
        var freq = _analyzer.Analyze("Aa").CharacterFrequencies
            .ToDictionary(e => e.Display, e => e.Count);
        Assert.AreEqual(2, freq["A"]);
        Assert.AreEqual(1, freq.Count);
    }

    // ---- Sort orders ----

    [TestMethod]
    public void Analyze_SortMostCommonFirst_OrdersByDescendingCount()
    {
        var options = new FrequencyOptions { Sort = FrequencySort.MostCommonFirst };
        var entries = _analyzer.Analyze("AAB", options).CharacterFrequencies;
        Assert.AreEqual("A", entries[0].Display);
        Assert.AreEqual("B", entries[1].Display);
    }

    [TestMethod]
    public void Analyze_SortLeastCommonFirst_OrdersByAscendingCount()
    {
        var options = new FrequencyOptions { Sort = FrequencySort.LeastCommonFirst };
        var entries = _analyzer.Analyze("AAB", options).CharacterFrequencies;
        Assert.AreEqual("B", entries[0].Display);
        Assert.AreEqual("A", entries[1].Display);
    }

    [TestMethod]
    public void Analyze_SortAlphabetical_OrdersByCharacter()
    {
        var options = new FrequencyOptions { Sort = FrequencySort.Alphabetical };
        var entries = _analyzer.Analyze("BCA", options).CharacterFrequencies;
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, entries.Select(e => e.Display).ToArray());
    }

    [TestMethod]
    public void Analyze_LettersOnlyScope_IgnoresDigitsAndSymbols()
    {
        var options = new FrequencyOptions { Scope = FrequencyScope.LettersOnly };
        var entries = _analyzer.Analyze("A1!A", options).CharacterFrequencies;
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("A", entries[0].Display);
        Assert.AreEqual(2, entries[0].Count);
    }

    [TestMethod]
    public void Analyze_StripAccents_FoldsDiacriticsToBaseLetter()
    {
        var options = new FrequencyOptions { StripAccents = true };
        var freq = _analyzer.Analyze("éE", options).CharacterFrequencies
            .ToDictionary(e => e.Display, e => e.Count);
        Assert.AreEqual(2, freq["E"]);
    }

    // ---- N-grams ----

    [TestMethod]
    public void Bigrams_Abab_CountsAbTwiceBaOnce()
    {
        var grams = _analyzer.Analyze("ABAB").Bigrams.ToDictionary(e => e.Display, e => e.Count);
        Assert.AreEqual(2, grams["AB"]);
        Assert.AreEqual(1, grams["BA"]);
    }

    [TestMethod]
    public void Trigrams_Counts3CharWindows()
    {
        var grams = _analyzer.Analyze("ABCABC").Trigrams.ToDictionary(e => e.Display, e => e.Count);
        Assert.AreEqual(2, grams["ABC"]);
        Assert.AreEqual(1, grams["BCA"]);
        Assert.AreEqual(1, grams["CAB"]);
    }

    [TestMethod]
    public void NGrams_DoNotSpanWhitespace_ByDefault()
    {
        // "AB CD" with bigrams: AB, CD only (no "B C" / across the gap)
        var grams = _analyzer.Analyze("AB CD").Bigrams.Select(e => e.Display).ToArray();
        CollectionAssert.Contains(grams, "AB");
        CollectionAssert.Contains(grams, "CD");
        Assert.AreEqual(2, grams.Length);
    }

    [TestMethod]
    public void NGrams_ConfigurableSize_ReturnsRequestedWindows()
    {
        var grams = _analyzer.NGrams("ABCDE", 4).ToDictionary(e => e.Display, e => e.Count);
        Assert.AreEqual(1, grams["ABCD"]);
        Assert.AreEqual(1, grams["BCDE"]);
        Assert.AreEqual(2, grams.Count);
    }

    [TestMethod]
    public void Words_MostFrequent_CountsWholeWordsCaseFolded()
    {
        var words = _analyzer.Analyze("the cat THE dog the").Words
            .ToDictionary(e => e.Display, e => e.Count);
        Assert.AreEqual(3, words["the"]);
        Assert.AreEqual(1, words["cat"]);
        Assert.AreEqual(1, words["dog"]);
    }

    // ---- Index of Coincidence ----

    [TestMethod]
    public void IndexOfCoincidence_UniformDistribution_IsLow()
    {
        // 26 distinct letters once each -> IoC = 0 (no repeated pairs)
        var alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var ioc = _analyzer.Analyze(alphabet).IndexOfCoincidence;
        Assert.AreEqual(0.0, ioc, 0.0001);
    }

    [TestMethod]
    public void IndexOfCoincidence_SingleRepeatedLetter_IsOne()
    {
        // all same letter -> IoC = 1.0
        var ioc = _analyzer.Analyze("AAAAAA").IndexOfCoincidence;
        Assert.AreEqual(1.0, ioc, 0.0001);
    }

    [TestMethod]
    public void IndexOfCoincidence_SkewedHigherThanUniform()
    {
        var uniform = _analyzer.Analyze("ABCDEF").IndexOfCoincidence;
        var skewed = _analyzer.Analyze("AAAABC").IndexOfCoincidence;
        Assert.IsTrue(skewed > uniform, $"expected skewed ({skewed}) > uniform ({uniform})");
    }

    [TestMethod]
    public void IndexOfCoincidence_KnownVector_AABB()
    {
        // 4 letters A,A,B,B: sum n(n-1)=2+2=4 ; N(N-1)=12 ; IoC=4/12=0.3333
        var ioc = _analyzer.Analyze("AABB").IndexOfCoincidence;
        Assert.AreEqual(1.0 / 3.0, ioc, 0.0001);
    }

    // ---- Key length estimate ----

    [TestMethod]
    public void EstimatedKeyLength_EnglishLikeText_IsOne()
    {
        // High-IoC monoalphabetic-like text estimates a short (1) key length.
        const string sample = "THISISARATHERLONGSAMPLEOFENGLISHLIKETEXTFORANALYSIS";
        var estimate = _analyzer.Analyze(sample).EstimatedKeyLength;
        Assert.IsTrue(estimate >= 1, "key length estimate should be at least 1");
    }

    [TestMethod]
    public void EstimatedKeyLength_NoLetters_IsZero()
    {
        var estimate = _analyzer.Analyze("123 456").EstimatedKeyLength;
        Assert.AreEqual(0, estimate);
    }

    // ---- Suggested mapping ----

    [TestMethod]
    public void SuggestedMapping_AlignsObservedOrderToEnglishOrder()
    {
        // Most common observed letter maps to 'E' (most common in English).
        var result = _analyzer.Analyze("EEEEXXXY");
        var map = result.SuggestedMapping.ToDictionary(m => m.Cipher, m => m.Plain);
        Assert.AreEqual('E', map['E']); // most common observed 'E' -> English most common 'E'
        Assert.AreEqual('T', map['X']); // 2nd most common observed 'X' -> English 2nd 'T'
    }

    // ---- Expected frequency comparison ----

    [TestMethod]
    public void EnglishFrequencies_AreProvidedAndSumToAboutHundred()
    {
        var sum = FrequencyTables.English.Values.Sum();
        Assert.AreEqual(100.0, sum, 1.0);
    }

    [TestMethod]
    public void CzechFrequencies_AreProvidedAndSumToAboutHundred()
    {
        var sum = FrequencyTables.Czech.Values.Sum();
        Assert.AreEqual(100.0, sum, 1.0);
    }

    [TestMethod]
    public void Analyze_LineCount_CountsNonEmptyLineBreaks()
    {
        Assert.AreEqual(3, _analyzer.Analyze("a\nb\nc").Totals.Lines);
        Assert.AreEqual(1, _analyzer.Analyze("single").Totals.Lines);
        Assert.AreEqual(0, _analyzer.Analyze("").Totals.Lines);
    }

    [TestMethod]
    public void ToCsv_ProducesHeaderAndRows()
    {
        var result = _analyzer.Analyze("AAB");
        var csv = result.ToCsv();
        var lines = csv.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        Assert.AreEqual("Character,Count,Percentage", lines[0]);
        StringAssert.StartsWith(lines[1], "A,2,");
        StringAssert.StartsWith(lines[2], "B,1,");
    }
}
