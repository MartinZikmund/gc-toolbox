using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class OneTimePadTests
{
    private readonly OneTimePad _cipher = new();

    // ---- Parity: classic letters-only Vernam (A=0..Z=25), (P+K) mod 26 ----

    [TestMethod]
    public void Transform_KnownVector_EncryptsHelloToEqnvz()
    {
        var result = _cipher.Transform("HELLO", "XMCKL", decrypt: false);
        Assert.AreEqual("EQNVZ", result.Text);
    }

    [TestMethod]
    public void Transform_KnownVector_DecryptsBackToHello()
    {
        var result = _cipher.Transform("EQNVZ", "XMCKL", decrypt: true);
        Assert.AreEqual("HELLO", result.Text);
    }

    [TestMethod]
    public void Transform_Encrypt_RoundTripsToOriginal()
    {
        const string plain = "ATTACKATDAWN";
        const string key = "LEMONLEMONLE";
        var encrypted = _cipher.Transform(plain, key, decrypt: false);
        var decrypted = _cipher.Transform(encrypted.Text, key, decrypt: true);
        Assert.AreEqual(plain, decrypted.Text);
    }

    // ---- Parity: only letters are processed; the site ignores non-letters ----

    [TestMethod]
    public void Transform_LettersOnly_IgnoresNonLetters()
    {
        // Default parity mode strips non-letters and upper-cases the output.
        var result = _cipher.Transform("hello, world!", "xmckl", decrypt: false, preserveNonLetters: false);
        // "helloworld" with the key cycled to "xmcklxmckl"
        var expected = _cipher.Transform("HELLOWORLD", "XMCKLXMCKL", decrypt: false, preserveNonLetters: false).Text;
        Assert.AreEqual(expected, result.Text);
        Assert.IsFalse(result.Text.Contains(','));
        Assert.IsFalse(result.Text.Contains(' '));
    }

    [TestMethod]
    public void Transform_LettersOnly_KeyAdvancesOnLettersOnlyEvenWhenInputHasPunctuation()
    {
        // The key letter must align with processed (letter) positions only.
        var withPunct = _cipher.Transform("HE.LLO", "XMCKL", decrypt: false, preserveNonLetters: false);
        Assert.AreEqual("EQNVZ", withPunct.Text);
    }

    // ---- Beyond parity: preserve case + pass non-letters through ----

    [TestMethod]
    public void Transform_PreserveNonLetters_PassesPunctuationAndKeepsCase()
    {
        var result = _cipher.Transform("He.llo", "XMCKL", decrypt: false, preserveNonLetters: true);
        // '.' passes through; key only advances on the letters H,e,l,l,o
        Assert.AreEqual("Eq.nvz", result.Text);
    }

    [TestMethod]
    public void Transform_PreserveNonLetters_KeyDoesNotAdvanceOnNonLetters()
    {
        var dotted = _cipher.Transform("H...E", "XM", decrypt: false, preserveNonLetters: true);
        var plain = _cipher.Transform("HE", "XM", decrypt: false, preserveNonLetters: true);
        // Letters between dots use key X then M, identical to the un-dotted "HE".
        Assert.AreEqual(plain.Text[0], dotted.Text[0]);
        Assert.AreEqual(plain.Text[1], dotted.Text[^1]);
    }

    // ---- Key shorter than message: cycle + warn ----

    [TestMethod]
    public void Transform_KeyShorterThanMessage_CyclesKey()
    {
        var result = _cipher.Transform("HELLOHELLO", "XMCKL", decrypt: false);
        Assert.AreEqual("EQNVZEQNVZ", result.Text);
    }

    [TestMethod]
    public void Transform_KeyShorterThanMessage_SetsTooShortWarning()
    {
        var result = _cipher.Transform("HELLO", "XM", decrypt: false);
        Assert.IsTrue(result.KeyTooShort);
    }

    [TestMethod]
    public void Transform_KeyAtLeastMessageLength_DoesNotWarn()
    {
        var result = _cipher.Transform("HELLO", "XMCKL", decrypt: false);
        Assert.IsFalse(result.KeyTooShort);
    }

    [TestMethod]
    public void Transform_KeyLongerThanMessage_DoesNotWarn()
    {
        var result = _cipher.Transform("HI", "XMCKL", decrypt: false);
        Assert.IsFalse(result.KeyTooShort);
    }

    // ---- Variants ----

    [TestMethod]
    public void Transform_Beaufort_IsItsOwnInverse()
    {
        const string plain = "GEOCACHE";
        const string key = "SECRETKE";
        var encrypted = _cipher.Transform(plain, key, decrypt: false, variant: OneTimePadVariant.Beaufort);
        // Beaufort (K - P) is an involution: applying it again recovers the plaintext.
        var roundTrip = _cipher.Transform(encrypted.Text, key, decrypt: false, variant: OneTimePadVariant.Beaufort);
        Assert.AreEqual(plain, roundTrip.Text);
    }

    [TestMethod]
    public void Transform_Beaufort_ComputesKMinusP()
    {
        // 'A' (P=0) with key 'C' (K=2) => K-P = 2 => 'C'
        var result = _cipher.Transform("A", "C", decrypt: false, variant: OneTimePadVariant.Beaufort);
        Assert.AreEqual("C", result.Text);
    }

    [TestMethod]
    public void Transform_VariantBeaufort_RoundTripsAgainstVigenere()
    {
        const string plain = "MESSAGE";
        const string key = "KEYKEYK";
        // Variant Beaufort encrypt is (P - K); decrypting is (C + K) = Vigenere encrypt.
        var encrypted = _cipher.Transform(plain, key, decrypt: false, variant: OneTimePadVariant.VariantBeaufort);
        var decrypted = _cipher.Transform(encrypted.Text, key, decrypt: true, variant: OneTimePadVariant.VariantBeaufort);
        Assert.AreEqual(plain, decrypted.Text);
    }

    [TestMethod]
    public void Transform_VariantBeaufort_ComputesPMinusK()
    {
        // 'C' (P=2) with key 'A' (K=0) => P-K = 2 => 'C'; and 'A'-'C' wraps to 'Y' (-2 mod 26 = 24)
        Assert.AreEqual("C", _cipher.Transform("C", "A", decrypt: false, variant: OneTimePadVariant.VariantBeaufort).Text);
        Assert.AreEqual("Y", _cipher.Transform("A", "C", decrypt: false, variant: OneTimePadVariant.VariantBeaufort).Text);
    }

    // ---- Charsets ----

    [TestMethod]
    public void Transform_DigitsCharset_WrapsModulo36()
    {
        // Charset order is A–Z (0..25) then 0–9 (26..35).
        // 'Z' (25) + key '1' (27) => 52 mod 36 = 16 => 'Q'
        var result = _cipher.Transform("Z", "1", decrypt: false, charset: OneTimePadCharset.LettersAndDigits);
        Assert.AreEqual("Q", result.Text);
    }

    [TestMethod]
    public void Transform_DigitsCharset_RoundTrips()
    {
        const string plain = "GC1A2B";
        const string key = "KEY007";
        var enc = _cipher.Transform(plain, key, decrypt: false, charset: OneTimePadCharset.LettersAndDigits);
        var dec = _cipher.Transform(enc.Text, key, decrypt: true, charset: OneTimePadCharset.LettersAndDigits);
        Assert.AreEqual(plain, dec.Text);
    }

    [TestMethod]
    public void Transform_AsciiCharset_WrapsModulo95AndRoundTrips()
    {
        const string plain = "Geocache #13! (N49 E007)";
        const string key = "SuperSecretPadKey1234567";
        var enc = _cipher.Transform(plain, key, decrypt: false, charset: OneTimePadCharset.PrintableAscii);
        Assert.AreNotEqual(plain, enc.Text);
        var dec = _cipher.Transform(enc.Text, key, decrypt: true, charset: OneTimePadCharset.PrintableAscii);
        Assert.AreEqual(plain, dec.Text);
    }

    // ---- Empty / no-key edge cases ----

    [TestMethod]
    public void Transform_EmptyText_ReturnsEmpty()
    {
        var result = _cipher.Transform("", "KEY", decrypt: false);
        Assert.AreEqual(string.Empty, result.Text);
    }

    [TestMethod]
    public void Transform_NoUsableKey_ReturnsTextUnchangedAndWarns()
    {
        // A key with no usable symbols can't shift anything; surface the warning, leave text intact.
        var result = _cipher.Transform("HELLO", "!!!", decrypt: false);
        Assert.AreEqual("HELLO", result.Text);
        Assert.IsTrue(result.KeyTooShort);
    }

    // ---- Random pad generation ----

    [TestMethod]
    public void GeneratePad_Letters_ProducesRequestedLengthInAlphabet()
    {
        var pad = _cipher.GeneratePad(20, OneTimePadCharset.Letters);
        Assert.AreEqual(20, pad.Length);
        Assert.IsTrue(pad.All(c => c is >= 'A' and <= 'Z'));
    }

    [TestMethod]
    public void GeneratePad_AsciiLengthOfMessage_ServesAsAPerfectPad()
    {
        const string plain = "ATTACKATDAWN";
        var pad = _cipher.GeneratePad(plain.Length, OneTimePadCharset.PrintableAscii);
        var enc = _cipher.Transform(plain, pad, decrypt: false, charset: OneTimePadCharset.PrintableAscii);
        Assert.IsFalse(enc.KeyTooShort);
        var dec = _cipher.Transform(enc.Text, pad, decrypt: true, charset: OneTimePadCharset.PrintableAscii);
        Assert.AreEqual(plain, dec.Text);
    }
}
