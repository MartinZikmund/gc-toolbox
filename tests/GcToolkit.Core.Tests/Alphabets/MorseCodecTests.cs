using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class MorseCodecTests
{
    private readonly MorseCodec _codec = new();

    [DataTestMethod]
    [DataRow("E", ".")]
    [DataRow("T", "-")]
    [DataRow("A", ".-")]
    [DataRow("S", "...")]
    [DataRow("SOS", "... --- ...")]
    [DataRow("1", ".----")]
    [DataRow("0", "-----")]
    [DataRow("!", "-.-.--")]
    [DataRow(".", ".-.-.-")]
    public void Encode_KnownInput_ProducesExpectedMorse(string input, string expected)
        => Assert.AreEqual(expected, _codec.Encode(input));

    [TestMethod]
    public void Encode_IsCaseInsensitive_ProducesSameOutput()
        => Assert.AreEqual(_codec.Encode("SOS"), _codec.Encode("sos"));

    [TestMethod]
    public void Encode_MultipleWords_SeparatedBySlash()
        => Assert.AreEqual(".... .. / -... -.-- .", _codec.Encode("HI BYE"));

    [TestMethod]
    public void Encode_CollapsesRepeatedWhitespace_IntoOneWordGap()
        => Assert.AreEqual(". / -", _codec.Encode("E    T"));

    [TestMethod]
    public void Encode_AccentedLetters_AreSupported()
    {
        Assert.AreEqual("..-..", _codec.Encode("É"));
        Assert.AreEqual(".-.-", _codec.Encode("Ä"));
    }

    [TestMethod]
    public void Encode_UnknownCharacter_ProducesHash()
        => Assert.AreEqual(".- #", _codec.Encode("A~"));

    [TestMethod]
    public void Encode_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, _codec.Encode(""));
        Assert.AreEqual(string.Empty, _codec.Encode("   "));
        Assert.AreEqual(string.Empty, _codec.Encode(null));
    }

    [DataTestMethod]
    [DataRow("...", "S")]
    [DataRow(".... .. / -.-- --- ..-", "HI YOU")]
    [DataRow("... --- ...", "SOS")]
    public void Decode_KnownMorse_ProducesExpectedText(string morse, string expected)
        => Assert.AreEqual(expected, _codec.Decode(morse));

    [TestMethod]
    public void Decode_TwoOrMoreSpaces_AreTreatedAsWordGap()
        => Assert.AreEqual("HI YOU", _codec.Decode(".... ..   -.-- --- ..-"));

    [TestMethod]
    public void Decode_UnknownToken_ProducesHash()
        => Assert.AreEqual("#", _codec.Decode("......."));

    [TestMethod]
    public void Decode_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, _codec.Decode(""));
        Assert.AreEqual(string.Empty, _codec.Decode("   "));
        Assert.AreEqual(string.Empty, _codec.Decode(null));
    }

    [TestMethod]
    public void RoundTrip_TextWithPunctuationAndWords_PreservesContent()
        => Assert.AreEqual("HELLO WORLD!", _codec.Decode(_codec.Encode("HELLO WORLD!")));

    [TestMethod]
    public void RoundTrip_SlashCharacter_IsNotConfusedWithWordSeparator()
    {
        // '/' as a character encodes to -..-.; the word separator is the literal " / ".
        Assert.AreEqual(".- -..-. -...", _codec.Encode("A/B"));
        Assert.AreEqual("A/B", _codec.Decode(".- -..-. -..."));
    }

    [TestMethod]
    public void Decode_AccentedCode_ReturnsAccentedLetter()
        => Assert.AreEqual("É", _codec.Decode("..-.."));

    [TestMethod]
    public void RoundTrip_AccentedText_IsPreserved()
        => Assert.AreEqual("CAFÉ", _codec.Decode(_codec.Encode("CAFÉ")));

    [TestMethod]
    public void RoundTrip_PunctuationAcrossWords_IsPreserved()
        => Assert.AreEqual("HI, BOB.", _codec.Decode(_codec.Encode("HI, BOB.")));

    [TestMethod]
    public void Decode_TrimsSurroundingWhitespace()
        => Assert.AreEqual("S", _codec.Decode("   ...   "));

    [DataTestMethod]
    [DataRow('Á', "A")]
    [DataRow('Č', "C")]
    [DataRow('Ď', "D")]
    [DataRow('Ě', "E")]
    [DataRow('Í', "I")]
    [DataRow('Ň', "N")]
    [DataRow('Ř', "R")]
    [DataRow('Š', "S")]
    [DataRow('Ť', "T")]
    [DataRow('Ý', "Y")]
    [DataRow('Ž', "Z")]
    public void Encode_CzechDiacriticWithoutOwnCode_FoldsToBaseLetter(char accented, string baseLetter)
    {
        // A Czech letter without its own ITU code encodes to its base letter's Morse, not '#'.
        Assert.AreEqual(_codec.Encode(baseLetter), _codec.Encode(accented.ToString()));
    }

    [TestMethod]
    public void Encode_CzechWord_ProducesRealMorseNotHashes()
    {
        // "ŽLUŤ" folds to "ZLUT" — every letter is real Morse, no unknown placeholder.
        var encoded = _codec.Encode("ŽLUŤ");

        Assert.AreEqual(_codec.Encode("ZLUT"), encoded);
        Assert.IsFalse(encoded.Contains('#'), "Czech diacritics should fold to base letters, not '#'.");
    }

    [TestMethod]
    public void Encode_AccentedLetterWithOwnCode_UsesItsCodeNotTheFold()
    {
        // Ä has its own ITU code (.-.-) and must NOT fold to A (.-).
        Assert.AreEqual(".-.-", _codec.Encode("Ä"));
        Assert.AreNotEqual(_codec.Encode("A"), _codec.Encode("Ä"));
    }

    [TestMethod]
    public void SharedCode_DecodesToCanonicalLetter()
    {
        // À and Å legitimately share the ITU code .--.-; both encode to it and it decodes to À.
        Assert.AreEqual(".--.-", _codec.Encode("À"));
        Assert.AreEqual(".--.-", _codec.Encode("Å"));
        Assert.AreEqual("À", _codec.Decode(".--.-"));
    }
}
