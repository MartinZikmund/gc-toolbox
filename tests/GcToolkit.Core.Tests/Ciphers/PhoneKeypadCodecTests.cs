using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class PhoneKeypadCodecTests
{
    private readonly PhoneKeypadCodec _codec = new();

    // ---- Vanity encode: each letter -> its single keypad digit ----

    [DataTestMethod]
    [DataRow("FLOWERS", "3569377")]
    [DataRow("HELLO", "43556")]
    [DataRow("CACHE", "22243")]
    [DataRow("abc", "222")]            // lower-case folds to upper
    public void Encode_Vanity_MapsLettersToSingleDigit(string text, string expected)
        => Assert.AreEqual(expected, _codec.Encode(text, PhoneKeypadMode.Vanity));

    [TestMethod]
    public void Encode_Vanity_SpaceBecomesZero()
        => Assert.AreEqual("35693770369", _codec.Encode("FLOWERS FOX", PhoneKeypadMode.Vanity));

    [TestMethod]
    public void Encode_Vanity_FoldsAccentedLetters()
        => Assert.AreEqual("222", _codec.Encode("ČÁB", PhoneKeypadMode.Vanity)); // Č->C(2) Á->A(2) B(2)

    // ---- Multitap encode: letter -> repeated key presses, groups space-separated ----

    [DataTestMethod]
    [DataRow("HELLO", "44 33 555 555 666")]
    [DataRow("ABC", "2 22 222")]
    [DataRow("SOS", "7777 666 7777")]   // S is the 4th letter on 7
    public void Encode_Multitap_RepeatsKeyByLetterPosition(string text, string expected)
        => Assert.AreEqual(expected, _codec.Encode(text, PhoneKeypadMode.Multitap));

    [TestMethod]
    public void Encode_Multitap_SpaceBecomesZeroGroup()
        => Assert.AreEqual("44 444 0 2", _codec.Encode("HI A", PhoneKeypadMode.Multitap));

    // ---- Empty / null ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Encode_EmptyOrNull_ReturnsEmpty(string? text)
    {
        Assert.AreEqual(string.Empty, _codec.Encode(text, PhoneKeypadMode.Vanity));
        Assert.AreEqual(string.Empty, _codec.Encode(text, PhoneKeypadMode.Multitap));
    }

    // ---- Multitap decode: exact, reversible ----

    [DataTestMethod]
    [DataRow("44 33 555 555 666", "HELLO")]
    [DataRow("2 22 222", "ABC")]
    [DataRow("7777 666 7777", "SOS")]
    public void DecodeMultitap_ParsesPressGroups(string code, string expected)
        => Assert.AreEqual(expected, _codec.DecodeMultitap(code));

    [TestMethod]
    public void DecodeMultitap_ZeroGroupBecomesSpace()
        => Assert.AreEqual("HI A", _codec.DecodeMultitap("44 444 0 2"));

    [TestMethod]
    public void DecodeMultitap_WrapsExtraPresses()
        => Assert.AreEqual("A", _codec.DecodeMultitap("2222")); // 4 presses on ABC wraps back to A

    [TestMethod]
    public void DecodeMultitap_UnknownGroupBecomesPlaceholder()
        => Assert.AreEqual("A#", _codec.DecodeMultitap("2 xyz"));

    [TestMethod]
    public void Multitap_RoundTripsLettersAndSpaces()
    {
        const string plain = "MEET AT THE CACHE";
        var code = _codec.Encode(plain, PhoneKeypadMode.Multitap);
        Assert.AreEqual(plain, _codec.DecodeMultitap(code));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void DecodeMultitap_EmptyOrNull_ReturnsEmpty(string? code)
        => Assert.AreEqual(string.Empty, _codec.DecodeMultitap(code));

    // ---- Vanity decode: ambiguous, surfaces per-digit candidate letters ----

    [TestMethod]
    public void DecodeVanityCandidates_ListsLettersPerDigit()
    {
        var candidates = _codec.DecodeVanityCandidates("269");

        Assert.AreEqual(3, candidates.Count);
        Assert.AreEqual('2', candidates[0].Digit);
        Assert.AreEqual("ABC", candidates[0].Letters);
        Assert.AreEqual('6', candidates[1].Digit);
        Assert.AreEqual("MNO", candidates[1].Letters);
        Assert.AreEqual('9', candidates[2].Digit);
        Assert.AreEqual("WXYZ", candidates[2].Letters);
    }

    [TestMethod]
    public void DecodeVanityCandidates_ZeroAndOneAreSpaces()
    {
        var candidates = _codec.DecodeVanityCandidates("2 0 2");

        // whitespace in the input is ignored; only the digits map
        Assert.AreEqual(3, candidates.Count);
        Assert.AreEqual('0', candidates[1].Digit);
        Assert.AreEqual(" ", candidates[1].Letters);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void DecodeVanityCandidates_EmptyOrNull_ReturnsEmpty(string? digits)
        => Assert.AreEqual(0, _codec.DecodeVanityCandidates(digits).Count);

    // ---- Selectable space digit (geocachingtoolbox.com parity) ----

    [TestMethod]
    public void Encode_Vanity_UsesTheChosenSpaceDigit()
        => Assert.AreEqual("35693771369", _codec.Encode("FLOWERS FOX", PhoneKeypadMode.Vanity, spaceDigit: '1'));

    [TestMethod]
    public void Encode_Multitap_UsesTheChosenSpaceDigit()
        => Assert.AreEqual("44 444 1 2", _codec.Encode("HI A", PhoneKeypadMode.Multitap, spaceDigit: '1'));

    [TestMethod]
    public void DecodeMultitap_TreatsBothSpaceDigitsAsSpace()
        => Assert.AreEqual("HI A", _codec.DecodeMultitap("44 444 1 2"));

    // ---- Vanity codes & tokenizing (what the dictionary decoder is built on) ----

    [DataTestMethod]
    [DataRow("CACHE", "22243")]
    [DataRow("digit", "34448")]
    [DataRow("Č", "2")]                    // accents fold to the base letter
    public void VanityCode_MapsWordToDigits(string word, string expected)
        => Assert.AreEqual(expected, PhoneKeypadCodec.VanityCode(word));

    [DataTestMethod]
    [DataRow("a-b")]
    [DataRow("1st")]
    [DataRow("hi there")]
    public void VanityCode_WordWithoutAKey_ReturnsNull(string word)
        => Assert.IsNull(PhoneKeypadCodec.VanityCode(word));

    [TestMethod]
    public void DigitFor_UnmappedCharacter_ReturnsNul()
        => Assert.AreEqual('\0', PhoneKeypadCodec.DigitFor('-'));

    [TestMethod]
    public void SplitVanityTokens_SplitsOnSpaceDigitsAndPunctuation()
    {
        // 1-800-FLOWERS pasted as digits: only the letter-bearing runs are words.
        var tokens = PhoneKeypadCodec.SplitVanityTokens("1-800-356-9377");

        CollectionAssert.AreEqual(new[] { "8", "356", "9377" }, tokens.ToArray());
    }

    [TestMethod]
    public void SplitVanityTokens_SplitsOnEitherSpaceDigit()
    {
        CollectionAssert.AreEqual(new[] { "22243", "34448" }, PhoneKeypadCodec.SplitVanityTokens("22243034448").ToArray());
        CollectionAssert.AreEqual(new[] { "22243", "34448" }, PhoneKeypadCodec.SplitVanityTokens("22243134448").ToArray());
    }

    [TestMethod]
    public void SplitVanityTokens_ToleratesWhitespaceAndSeparators()
        => Assert.AreEqual("555|273|4567", string.Join('|', PhoneKeypadCodec.SplitVanityTokens("(555) 273.4567")));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("0110")]
    public void SplitVanityTokens_NothingToDecode_ReturnsEmpty(string? digits)
        => Assert.AreEqual(0, PhoneKeypadCodec.SplitVanityTokens(digits).Count);

    // ---- Keypad layout (for the on-screen keys / legend) ----

    [TestMethod]
    public void KeypadLetters_MatchStandardItuLayout()
    {
        Assert.AreEqual("ABC", _codec.LettersFor('2'));
        Assert.AreEqual("PQRS", _codec.LettersFor('7'));
        Assert.AreEqual("WXYZ", _codec.LettersFor('9'));
        Assert.AreEqual(string.Empty, _codec.LettersFor('0'));
    }
}
