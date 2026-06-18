using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class PhoneKeypadCodecTests
{
    private readonly PhoneKeypadCodec _codec = new();

    // ---- Multi-tap encode ----

    [TestMethod]
    public void EncodeMultitap_Code_ProducesGroupedTaps()
        // The headline vector: C=222, O=666, D=3, E=33, joined by '-'.
        => Assert.AreEqual("222-666-3-33", _codec.EncodeMultitap("CODE"));

    [DataTestMethod]
    [DataRow("A", "2")]
    [DataRow("B", "22")]
    [DataRow("C", "222")]
    [DataRow("S", "7777")]    // key 7 has 4 letters
    [DataRow("Z", "9999")]    // key 9 has 4 letters
    public void EncodeMultitap_SingleLetter_RepeatsTheKey(string text, string expected)
        => Assert.AreEqual(expected, _codec.EncodeMultitap(text));

    [TestMethod]
    public void EncodeMultitap_IsCaseInsensitive()
        => Assert.AreEqual(_codec.EncodeMultitap("CODE"), _codec.EncodeMultitap("code"));

    [TestMethod]
    public void EncodeMultitap_Space_BecomesZero()
        => Assert.AreEqual("2-0-22", _codec.EncodeMultitap("A B"));

    [TestMethod]
    public void EncodeMultitap_CustomSeparator_IsHonored()
        => Assert.AreEqual("222 666 3 33", _codec.EncodeMultitap("CODE", ' '));

    [TestMethod]
    public void EncodeMultitap_NonAlphabetCharacters_AreSkipped()
        => Assert.AreEqual("2", _codec.EncodeMultitap("A!"));

    // ---- Multi-tap decode ----

    [TestMethod]
    public void DecodeMultitap_GroupedTaps_ProducesCode()
        => Assert.AreEqual("CODE", _codec.DecodeMultitap("222-666-3-33"));

    [TestMethod]
    public void DecodeMultitap_WhitespaceSeparated_Works()
        => Assert.AreEqual("CODE", _codec.DecodeMultitap("222 666 3 33"));

    [TestMethod]
    public void DecodeMultitap_UnseparatedRun_BreaksOnKeyChange()
        // "227" with no separator -> key change 2->7 splits into "22" (B) and "7" (P).
        => Assert.AreEqual("BP", _codec.DecodeMultitap("227"));

    [TestMethod]
    public void DecodeMultitap_Zero_BecomesSpace()
        => Assert.AreEqual("A B", _codec.DecodeMultitap("2-0-22"));

    [TestMethod]
    public void DecodeMultitap_OverTapping_WrapsAroundKey()
        // Key 7 has 4 letters; 5 taps wraps to the 1st (P).
        => Assert.AreEqual("P", _codec.DecodeMultitap("77777"));

    [TestMethod]
    public void DecodeMultitap_StrayPunctuation_IsIgnored()
        => Assert.AreEqual("CODE", _codec.DecodeMultitap("222.666.3.33"));

    // ---- Multi-tap round-trip ----

    [DataTestMethod]
    [DataRow("CODE")]
    [DataRow("GEOCACHE")]
    [DataRow("HELLO WORLD")]
    [DataRow("PUZZLE")]
    public void RoundTrip_Multitap_IsLossless(string text)
        => Assert.AreEqual(text, _codec.DecodeMultitap(_codec.EncodeMultitap(text)));

    // ---- Key + position ----

    [TestMethod]
    public void DecodeKeyPosition_SevenThree_IsR()
        // The headline key-position vector: 3rd letter of key 7 (PQRS) is R.
        => Assert.AreEqual("R", _codec.DecodeKeyPosition("7-3"));

    [TestMethod]
    public void EncodeKeyPosition_R_IsSevenThree()
        => Assert.AreEqual("7-3", _codec.EncodeKeyPosition("R"));

    [TestMethod]
    public void DecodeKeyPosition_MultiplePairs_DecodeAll()
        => Assert.AreEqual("RAT", _codec.DecodeKeyPosition("7-3 2-1 8-1"));

    [TestMethod]
    public void DecodeKeyPosition_Zero_BecomesSpace()
        => Assert.AreEqual("A B", _codec.DecodeKeyPosition("2-1 0 2-2"));

    [TestMethod]
    public void DecodeKeyPosition_OutOfRangePosition_IsSkipped()
        // Key 2 (ABC) has no 4th letter, so the token is dropped.
        => Assert.AreEqual("", _codec.DecodeKeyPosition("2-4"));

    [DataTestMethod]
    [DataRow("RAT")]
    [DataRow("GEOCACHE")]
    [DataRow("A B C")]
    public void RoundTrip_KeyPosition_IsLossless(string text)
        => Assert.AreEqual(text, _codec.DecodeKeyPosition(_codec.EncodeKeyPosition(text)));

    // ---- T9 predictive ----

    [TestMethod]
    public void EncodeT9_Geocache_ProducesKnownSequence()
        => Assert.AreEqual("43622243", _codec.EncodeT9("GEOCACHE"));

    [TestMethod]
    public void EncodeT9_Code_ProducesShortSequence()
        => Assert.AreEqual("2633", _codec.EncodeT9("CODE"));

    [TestMethod]
    public void EnumerateT9Token_Geocache_IncludesGeocache()
    {
        var combos = _codec.EnumerateT9Token("43622243");
        CollectionAssert.Contains(combos.ToList(), "GEOCACHE");
    }

    [TestMethod]
    public void EnumerateT9Token_CountMatchesProductOfLetterCounts()
    {
        // 36 -> key 3 (DEF, 3) x key 6 (MNO, 3) = 9 combinations.
        var combos = _codec.EnumerateT9Token("36");
        Assert.AreEqual(9, combos.Count);
        CollectionAssert.Contains(combos.ToList(), "DM");
        CollectionAssert.Contains(combos.ToList(), "EN");
        CollectionAssert.Contains(combos.ToList(), "FO");
    }

    [TestMethod]
    public void EnumerateT9Token_FirstCombo_IsFirstLetterOfEachKey()
        // Stable order: first letter of each key in sequence.
        => Assert.AreEqual("DM", _codec.EnumerateT9Token("36")[0]);

    [TestMethod]
    public void EnumerateT9Token_IgnoresDigitsWithoutLetters()
        // 0 and 1 carry no letters, so "201" enumerates only key 2.
        => Assert.AreEqual(3, _codec.EnumerateT9Token("201").Count);

    [TestMethod]
    public void EnumerateT9Token_RespectsLimit()
    {
        // Each key contributes >=3 letters; a long run would explode, so the cap bounds it.
        var combos = _codec.EnumerateT9Token("9999999", limit: 100);
        Assert.IsTrue(combos.Count <= 100);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void EnumerateT9Token_EmptyOrNull_YieldsNothing(string? digits)
        => Assert.AreEqual(0, _codec.EnumerateT9Token(digits).Count);

    // ---- Multi-word split ----

    [TestMethod]
    public void SplitWords_OnZero_SplitsTokens()
    {
        var words = _codec.SplitWords("43622243043622243");
        Assert.AreEqual(2, words.Count);
        Assert.AreEqual("43622243", words[0]);
        Assert.AreEqual("43622243", words[1]);
    }

    [TestMethod]
    public void SplitWords_OnWhitespace_SplitsTokens()
    {
        var words = _codec.SplitWords("2633 4663");
        Assert.AreEqual(2, words.Count);
    }

    [TestMethod]
    public void SplitWords_CustomSeparator_IsHonored()
    {
        var words = _codec.SplitWords("2633-4663", separator: '-');
        Assert.AreEqual(2, words.Count);
    }

    // ---- Keypad model ----

    [TestMethod]
    public void Keypad_HasTenKeys()
        => Assert.AreEqual(10, PhoneKeypadCodec.Keypad.Count);

    [TestMethod]
    public void Keypad_KeySeven_HasFourLetters()
    {
        var seven = PhoneKeypadCodec.Keypad.First(k => k.Digit == '7');
        Assert.AreEqual("PQRS", seven.Letters);
    }

    [TestMethod]
    public void Keypad_KeyZero_HasNoLetters()
    {
        var zero = PhoneKeypadCodec.Keypad.First(k => k.Digit == '0');
        Assert.AreEqual("", zero.Letters);
    }

    [TestMethod]
    public void LettersFor_KnownDigit_ReturnsLetters()
        => Assert.AreEqual("ABC", _codec.LettersFor('2'));

    [TestMethod]
    public void LettersFor_UnknownChar_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, _codec.LettersFor('*'));

    // ---- Empty / null ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void EncodeMultitap_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _codec.EncodeMultitap(text));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void DecodeMultitap_EmptyOrNull_ReturnsEmpty(string? code)
        => Assert.AreEqual(string.Empty, _codec.DecodeMultitap(code));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void EncodeT9_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _codec.EncodeT9(text));
}
