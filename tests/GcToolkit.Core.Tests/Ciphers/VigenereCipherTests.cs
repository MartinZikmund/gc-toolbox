using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class VigenereCipherTests
{
    private readonly VigenereCipher _cipher = new();

    // ---- Encode: the canonical Wikipedia example ----

    [TestMethod]
    public void Encode_AttackAtDawn_ProducesKnownCiphertext()
        => Assert.AreEqual("LXFOPVEFRNHR", _cipher.Encode("ATTACKATDAWN", "LEMON"));

    [TestMethod]
    public void Decode_IsExactInverseOfEncode()
        => Assert.AreEqual("ATTACKATDAWN", _cipher.Decode("LXFOPVEFRNHR", "LEMON"));

    [DataTestMethod]
    [DataRow("HELLO", "KEY", "RIJVS")]
    public void Encode_ShortKey_MatchesReference(string text, string key, string expected)
        => Assert.AreEqual(expected, _cipher.Encode(text, key));

    [TestMethod]
    public void Decode_ShortKey_MatchesReference()
        => Assert.AreEqual("HELLO", _cipher.Decode("RIJVS", "KEY"));

    [TestMethod]
    public void EncodeThenDecode_RoundTripsArbitraryText()
    {
        const string plain = "The Quick Brown Fox, Jumps! Over 13 Lazy Dogs.";
        var encoded = _cipher.Encode(plain, "GEOCACHE");
        Assert.AreEqual(plain, _cipher.Decode(encoded, "GEOCACHE"));
    }

    // ---- Case preservation & non-letter passthrough ----

    [TestMethod]
    public void Encode_PreservesCase()
        => Assert.AreEqual("Rijvs", _cipher.Encode("Hello", "key"));

    [TestMethod]
    public void Encode_UpperAndLowerKeyAreEquivalent()
        => Assert.AreEqual(_cipher.Encode("Hello World", "KEY"), _cipher.Encode("Hello World", "key"));

    [TestMethod]
    public void Encode_PassesNonLettersThroughUnchanged()
    {
        // Non-letters appear verbatim; only letters are enciphered.
        var result = _cipher.Encode("N 49 12.345", "KEY");
        Assert.IsTrue(result.Contains("49"));
        Assert.IsTrue(result.Contains(' '));
        Assert.IsTrue(result.Contains('.'));
        // Digits and separators are untouched; only the two letters (N, plus none else) shift.
        Assert.AreEqual(" 49 12.345", result[1..]);
    }

    [TestMethod]
    public void Encode_KeyAdvancesOnlyOnLetters_SoTextSpacesDoNotConsumeKey()
    {
        // The enciphered letters must be identical whether or not the plaintext contains spaces,
        // because the key advances only on letters.
        var withSpaces = _cipher.Encode("AT TACK", "LEMON");
        var withoutSpaces = _cipher.Encode("ATTACK", "LEMON");
        Assert.AreEqual(withoutSpaces, string.Concat(withSpaces.Where(char.IsLetter)));
    }

    // ---- Key cycle skips spaces/punctuation in the KEY itself ----

    [TestMethod]
    public void Encode_KeyWithSpacesAndPunctuation_IgnoredInKeyCycle()
        => Assert.AreEqual(_cipher.Encode("ATTACKATDAWN", "LEMON"),
                           _cipher.Encode("ATTACKATDAWN", "L E-M.O N"));

    [TestMethod]
    public void Encode_KeyWithDigits_IgnoresNonLetterKeyChars()
        => Assert.AreEqual(_cipher.Encode("HELLO", "KEY"), _cipher.Encode("HELLO", "K3E9Y"));

    // ---- Empty / null / keyless inputs ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encode_EmptyOrNullText_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Encode(text, "KEY"));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("123!")]   // no letters in the key
    public void Encode_KeyWithoutLetters_ReturnsTextUnchanged(string? key)
        => Assert.AreEqual("HELLO", _cipher.Encode("HELLO", key));

    // ---- Alphabet / key-stream display ----

    [TestMethod]
    public void PlainAlphabet_IsAtoZ()
        => Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", _cipher.PlainAlphabet);

    [TestMethod]
    public void NormalizeKey_StripsNonLettersAndUppercases()
        => Assert.AreEqual("LEMON", _cipher.NormalizeKey("l E-m.o N"));

    [TestMethod]
    public void CipherAlphabetForKeyLetter_RotatesByLetterIndex()
    {
        // Key letter 'B' (index 1) rotates A->B..Z->A, i.e. a Caesar shift of 1.
        Assert.AreEqual("BCDEFGHIJKLMNOPQRSTUVWXYZA", _cipher.CipherAlphabetForKeyLetter('B'));
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", _cipher.CipherAlphabetForKeyLetter('A'));
    }

    // ---- Index of Coincidence ----

    [TestMethod]
    public void IndexOfCoincidence_EnglishPlaintext_NearSeventyThousandths()
    {
        var ic = _cipher.IndexOfCoincidence(EnglishCorpus);
        Assert.IsTrue(ic > 0.060, $"IC was {ic}, expected > 0.060 for English");
    }

    [TestMethod]
    public void IndexOfCoincidence_LongVigenereCiphertext_IsLowerThanPlaintext()
    {
        var ic = _cipher.IndexOfCoincidence(_cipher.Encode(EnglishCorpus, "GEOCACHING"));
        // A poly-alphabetic cipher flattens the distribution toward the random IC (~0.0385).
        Assert.IsTrue(ic < 0.055, $"ciphertext IC was {ic}, expected < 0.055");
    }

    // ---- Key-length estimation via averaged IC ----

    [TestMethod]
    public void EstimateKeyLength_LongCiphertext_ReturnsTrueLength()
    {
        var encoded = _cipher.Encode(EnglishCorpus, "CIPHER"); // length 6
        var best = _cipher.EstimateKeyLength(encoded, 12);
        Assert.AreEqual(6, best);
    }

    [TestMethod]
    public void RankKeyLengths_TopCandidateIsMultipleOfTrueLength()
    {
        var encoded = _cipher.Encode(EnglishCorpus, "LEMON"); // length 5
        var ranked = _cipher.RankKeyLengths(encoded, 12);

        Assert.IsTrue(ranked.Count > 0);
        // The top candidate's length should be the true length or a multiple of it.
        Assert.AreEqual(0, ranked[0].Length % 5, $"top key length {ranked[0].Length} is not a multiple of 5");
        // Averaged IC of the winning length should be in the English ballpark.
        Assert.IsTrue(ranked[0].IndexOfCoincidence > 0.055, $"winning IC {ranked[0].IndexOfCoincidence} too low");
    }

    // ---- Full assisted solve ----

    [TestMethod]
    public void Solve_RecoversKnownKeyAndPlaintext_FromLongText()
    {
        var encoded = _cipher.Encode(EnglishCorpus, "LEMON");

        var result = _cipher.Solve(encoded, 12);

        Assert.AreEqual("LEMON", result.Key);
        Assert.AreEqual(EnglishCorpus, result.PlainText);
    }

    [TestMethod]
    public void Solve_RecoversKeyIndependentOfCase_AndKeepsFormatting()
    {
        // The natural corpus carries mixed case + spaces + punctuation; the solver works on letters
        // only, and the decoded result preserves the original formatting exactly.
        var encoded = _cipher.Encode(EnglishProse, "DELTA");

        var result = _cipher.Solve(encoded, 12);

        Assert.AreEqual("DELTA", result.Key);
        Assert.AreEqual(EnglishProse, result.PlainText);
    }

    [TestMethod]
    public void Solve_ReportsRecoveredKeyLength()
    {
        var encoded = _cipher.Encode(EnglishCorpus, "KEY"); // length 3
        var result = _cipher.Solve(encoded, 12);
        Assert.AreEqual("KEY", result.Key);
        Assert.AreEqual(3, result.KeyLength);
    }

    // ---- Key-length search range (reference-site parity: "from x to y", min 1 - max 50) ----

    [TestMethod]
    public void RankKeyLengths_HonoursTheRequestedRange()
    {
        var encoded = _cipher.Encode(EnglishCorpus, "LEMON");

        var ranked = _cipher.RankKeyLengths(encoded, maxKeyLength: 8, minKeyLength: 4);

        Assert.IsTrue(ranked.Count > 0);
        Assert.IsTrue(ranked.All(c => c.Length is >= 4 and <= 8),
            $"got lengths [{string.Join(", ", ranked.Select(c => c.Length))}]");
    }

    [TestMethod]
    public void RankKeyLengths_MaxAboveTheSupportedCeiling_IsClampedToFifty()
    {
        var encoded = _cipher.Encode(EnglishCorpus, "LEMON");

        var ranked = _cipher.RankKeyLengths(encoded, maxKeyLength: 500);

        Assert.IsTrue(ranked.All(c => c.Length <= VigenereCipher.MaxSupportedKeyLength));
    }

    [TestMethod]
    public void RankKeyLengths_InvertedRange_ReturnsEmpty()
        => Assert.AreEqual(0, _cipher.RankKeyLengths(_cipher.Encode(EnglishCorpus, "LEMON"), maxKeyLength: 3, minKeyLength: 9).Count);

    [TestMethod]
    public void Solve_RangeExcludingTheTrueLength_StillReturnsAKeyWithoutThrowing()
    {
        var encoded = _cipher.Encode(EnglishCorpus, "LEMON"); // true length 5

        var result = _cipher.Solve(encoded, maxKeyLength: 4, minKeyLength: 2);

        // The right key is out of range, but the solver must degrade gracefully, not throw.
        Assert.IsTrue(result.Key.Length is >= 1 and <= 4);
    }

    // ---- Language profiles (reference-site parity: the solver's Language dropdown) ----

    [TestMethod]
    public void LanguageProfiles_ExposeEnglishAndCzech()
    {
        CollectionAssert.AreEqual(
            new[] { "En", "Cs" },
            VigenereLanguageProfile.All.Select(p => p.Id).ToArray());
    }

    [TestMethod]
    public void LanguageProfile_Proportions_AreNormalizedAndNonZero()
    {
        foreach (var profile in VigenereLanguageProfile.All)
        {
            Assert.AreEqual(VigenereCipher.AlphabetSize, profile.Proportions.Count);
            Assert.AreEqual(1.0, profile.Proportions.Sum(), 0.01, $"{profile.Id} proportions must sum to ~1");
            // A zero expectation would divide by zero in the chi-squared score.
            Assert.IsTrue(profile.Proportions.All(p => p > 0), $"{profile.Id} has a zero expectation");
        }
    }

    [TestMethod]
    public void Solve_WithExplicitEnglishProfile_MatchesTheDefault()
    {
        var encoded = _cipher.Encode(EnglishCorpus, "LEMON");

        var explicitEnglish = _cipher.Solve(encoded, 12, 1, VigenereLanguageProfile.English);

        Assert.AreEqual("LEMON", explicitEnglish.Key);
    }

    [TestMethod]
    public void Solve_CzechCiphertext_RecoversTheKeyWithTheCzechProfile()
    {
        var encoded = _cipher.Encode(CzechProse, "KLIC");

        var result = _cipher.Solve(encoded, 12, 1, VigenereLanguageProfile.Czech);

        Assert.AreEqual("KLIC", result.Key);
        Assert.AreEqual(CzechProse, result.PlainText);
    }

    // Natural Czech prose with diacritics stripped (the cipher only moves A-Z), long enough for the
    // frequency analysis to lock on.
    private const string CzechProse =
        "Bylo jednou jedno mesto, ktere lezelo hluboko v udoli mezi vysokymi horami a temnymi lesy. "
        + "Lide v tom meste zili klidne a spokojene, protoze pudou byla urodna a voda v rece cista. "
        + "Kazde rano vychazeli hospodari na pole a kazdy vecer se vraceli domu k svym rodinam. "
        + "Deti si hraly na namesti pred starou radnici a stari muzi sedavali na lavickach ve stinu lip. "
        + "Kdyz prisla zima, snih pokryl strechy domu a z komínu stoupal dym k sedive obloze. "
        + "Nikdo nevedel, jak dlouho uz to mesto stoji, ale vsichni verili, ze tam bude stat navzdy. "
        + "Jednoho dne prisel do mesta cizinec a prinesl s sebou zpravu, ktera vsechno zmenila. "
        + "Vypravel o zemi za horami, kde rostou stromy s zlatymi listy a kde reky teku k mori.";

    // Natural English prose (public-domain: openings of Austen & Dickens). Non-repeating text with a
    // realistic letter distribution is what the IC + chi-squared solver targets — repeated text injects
    // spurious periodicity that no real ciphertext has.
    private const string EnglishProse =
        "It is a truth universally acknowledged, that a single man in possession of a good fortune, "
        + "must be in want of a wife. However little known the feelings or views of such a man may be on "
        + "his first entering a neighbourhood, this truth is so well fixed in the minds of the surrounding "
        + "families, that he is considered the rightful property of some one or other of their daughters. "
        + "It was the best of times, it was the worst of times, it was the age of wisdom, it was the age of "
        + "foolishness, it was the epoch of belief, it was the epoch of incredulity, it was the season of "
        + "Light, it was the season of Darkness, it was the spring of hope, it was the winter of despair, "
        + "we had everything before us, we had nothing before us, we were all going direct to Heaven, "
        + "we were all going direct the other way. Call me Ishmael. Some years ago, having little or no "
        + "money in my purse, and nothing particular to interest me on shore, I thought I would sail about "
        + "a little and see the watery part of the world that surrounds this great and ancient harbour.";

    // The same prose reduced to its letters (upper-cased) — the canonical recoverable signal.
    private static readonly string EnglishCorpus =
        string.Concat(EnglishProse.Where(char.IsLetter)).ToUpperInvariant();
}
