using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class KamasutraCipherTests
{
    private readonly KamasutraCipher _cipher = new();

    // ---- Default A–Z split pairing (A/N, B/O, … M/Z) ----

    [TestMethod]
    public void BuildDefaultPairs_Produces13Pairs_FirstHalfAgainstSecondHalf()
    {
        var pairs = _cipher.BuildDefaultPairs();

        Assert.AreEqual(13, pairs.Count);
        Assert.AreEqual('A', pairs[0].First);
        Assert.AreEqual('N', pairs[0].Second);
        Assert.AreEqual('M', pairs[12].First);
        Assert.AreEqual('Z', pairs[12].Second);
    }

    [TestMethod]
    public void Transform_DefaultPairs_MapsAtoNandBack()
    {
        var pairs = _cipher.BuildDefaultPairs();
        Assert.AreEqual("NOP", _cipher.Transform("ABC", pairs));
        Assert.AreEqual("ABC", _cipher.Transform("NOP", pairs));
    }

    [TestMethod]
    public void Transform_DefaultPairs_IsInvolution()
    {
        var pairs = _cipher.BuildDefaultPairs();
        const string plain = "The Quick Brown Fox";
        var once = _cipher.Transform(plain, pairs);
        Assert.AreNotEqual(plain, once);
        Assert.AreEqual(plain, _cipher.Transform(once, pairs)); // encode == decode
    }

    [TestMethod]
    public void Transform_DefaultPairs_PreservesCase()
    {
        var pairs = _cipher.BuildDefaultPairs();
        Assert.AreEqual("nop", _cipher.Transform("abc", pairs));
        Assert.AreEqual("Nop", _cipher.Transform("Abc", pairs));
    }

    [TestMethod]
    public void Transform_PassesThroughDigitsPunctuationAndSpace()
    {
        var pairs = _cipher.BuildDefaultPairs();
        // Letters are swapped (A->N, space/digit/punct untouched).
        Assert.AreEqual("N 49 13.456", _cipher.Transform("A 49 13.456", pairs));
    }

    [TestMethod]
    public void Transform_LeavesUnpairedLettersUnchanged()
    {
        // Only A<->B paired; every other letter passes through.
        var pairs = new[] { new KamasutraPair('A', 'B') };
        Assert.AreEqual("BACDE", _cipher.Transform("ABCDE", pairs));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Transform_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Transform(text, _cipher.BuildDefaultPairs()));

    // ---- Keyword-seeded pairing ----

    [TestMethod]
    public void BuildFromKeyword_DedupesKeywordThenAppendsAlphabet_PairsColumnWise()
    {
        // "KAMASUTRA" dedupes (case-insensitive) to K A M S U T R, then the rest of A–Z follows:
        // K A M S U T R B C D E F G | H I J L N O P Q V W X Y Z
        var pairs = _cipher.BuildFromKeyword("KAMASUTRA");

        Assert.AreEqual(13, pairs.Count);
        Assert.AreEqual('K', pairs[0].First);
        Assert.AreEqual('H', pairs[0].Second);
        Assert.AreEqual('A', pairs[1].First);
        Assert.AreEqual('I', pairs[1].Second);
    }

    [TestMethod]
    public void BuildFromKeyword_RoundTrips()
    {
        var pairs = _cipher.BuildFromKeyword("SECRET");
        const string plain = "ATTACK AT DAWN";
        Assert.AreEqual(plain, _cipher.Transform(_cipher.Transform(plain, pairs), pairs));
    }

    [TestMethod]
    public void BuildFromKeyword_EmptyKeyword_EqualsDefaultPairing()
    {
        var fromKeyword = _cipher.BuildFromKeyword("");
        var defaults = _cipher.BuildDefaultPairs();
        for (var i = 0; i < defaults.Count; i++)
        {
            Assert.AreEqual(defaults[i].First, fromKeyword[i].First);
            Assert.AreEqual(defaults[i].Second, fromKeyword[i].Second);
        }
    }

    // ---- Manual / lenient parsing ----

    [TestMethod]
    public void ParsePairs_AcceptsSpacesCommasAndNewlines()
    {
        var pairs = _cipher.ParsePairs("AB, CD\nEF GH");
        Assert.AreEqual(4, pairs.Count);
        Assert.AreEqual('A', pairs[0].First);
        Assert.AreEqual('B', pairs[0].Second);
        Assert.AreEqual('G', pairs[3].First);
        Assert.AreEqual('H', pairs[3].Second);
    }

    [TestMethod]
    public void Transform_ManualCustomPairs_SwapsTheChosenLetters()
    {
        var pairs = _cipher.ParsePairs("AB CD");
        Assert.AreEqual("BADC", _cipher.Transform("ABCD", pairs));
    }

    [TestMethod]
    public void ParsePairs_UppercasesAndIgnoresStrayWhitespace()
    {
        var pairs = _cipher.ParsePairs("  ab \n cd  ");
        Assert.AreEqual(2, pairs.Count);
        Assert.AreEqual('A', pairs[0].First);
        Assert.AreEqual('B', pairs[0].Second);
    }

    // ---- Validation ----

    [TestMethod]
    public void Validate_DefaultPairs_IsValid()
    {
        var result = _cipher.Validate(_cipher.BuildDefaultPairs());
        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.OffendingPair);
    }

    [TestMethod]
    public void Validate_DuplicateLetter_ReportsTheOffendingPair()
    {
        // 'A' appears twice: pair AB and pair AC.
        var pairs = new[] { new KamasutraPair('A', 'B'), new KamasutraPair('A', 'C') };
        var result = _cipher.Validate(pairs);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(KamasutraValidationError.DuplicateLetter, result.Error);
        Assert.AreEqual("AC", result.OffendingPair);
    }

    [TestMethod]
    public void Validate_SameLetterPairedWithItself_ReportsDuplicate()
    {
        var pairs = new[] { new KamasutraPair('A', 'A') };
        var result = _cipher.Validate(pairs);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(KamasutraValidationError.DuplicateLetter, result.Error);
        Assert.AreEqual("AA", result.OffendingPair);
    }

    [TestMethod]
    public void Validate_NonLetterCharacter_ReportsInvalidCharacter()
    {
        var pairs = new[] { new KamasutraPair('A', '1') };
        var result = _cipher.Validate(pairs);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(KamasutraValidationError.InvalidCharacter, result.Error);
    }

    [TestMethod]
    public void ParsePairs_OddTrailingLetter_IsReportedAsOddCount()
    {
        // "ABC" -> pair AB, then a dangling 'C'.
        var pairs = _cipher.ParsePairs("ABC");
        var result = _cipher.Validate(pairs);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(KamasutraValidationError.OddLetterCount, result.Error);
    }

    // ---- Random pairing ----

    [TestMethod]
    public void BuildRandomPairs_Produces13ValidPairs()
    {
        var pairs = _cipher.BuildRandomPairs();
        Assert.AreEqual(13, pairs.Count);
        Assert.IsTrue(_cipher.Validate(pairs).IsValid);
    }

    [TestMethod]
    public void BuildRandomPairs_RoundTrips()
    {
        var pairs = _cipher.BuildRandomPairs();
        const string plain = "GEOCACHE";
        Assert.AreEqual(plain, _cipher.Transform(_cipher.Transform(plain, pairs), pairs));
    }

    [TestMethod]
    public void FormatPairs_RendersUppercaseTwoLetterGroups()
    {
        var formatted = _cipher.FormatPairs(_cipher.BuildDefaultPairs());
        StringAssert.StartsWith(formatted, "AN");
        StringAssert.Contains(formatted, "MZ");
    }
}
