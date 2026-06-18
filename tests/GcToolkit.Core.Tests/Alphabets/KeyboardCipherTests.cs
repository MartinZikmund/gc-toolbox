using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class KeyboardCipherTests
{
    private readonly KeyboardCipher _cipher = new();

    // ---- Known vectors (QWERTY) ----

    [TestMethod]
    public void Encrypt_QwertyAbc_MapsToQwe()
        => Assert.AreEqual("QWE", _cipher.Encrypt("ABC", KeyboardLayout.Qwerty));

    [TestMethod]
    public void Decrypt_QwertyQwe_MapsToAbc()
        => Assert.AreEqual("ABC", _cipher.Decrypt("QWE", KeyboardLayout.Qwerty));

    [TestMethod]
    public void Encrypt_QwertyHello_MapsToItssg()
        => Assert.AreEqual("ITSSG", _cipher.Encrypt("HELLO", KeyboardLayout.Qwerty));

    [TestMethod]
    public void Decrypt_QwertyItssg_MapsToHello()
        => Assert.AreEqual("HELLO", _cipher.Decrypt("ITSSG", KeyboardLayout.Qwerty));

    // ---- Case preservation & pass-through ----

    [TestMethod]
    public void Encrypt_PreservesLowercase()
        => Assert.AreEqual("qwe", _cipher.Encrypt("abc", KeyboardLayout.Qwerty));

    [TestMethod]
    public void Encrypt_MixedCase_PreservesEachLetterCase()
        => Assert.AreEqual("Itssg", _cipher.Encrypt("Hello", KeyboardLayout.Qwerty));

    [TestMethod]
    public void Encrypt_NonLetters_PassThroughUnchanged()
        => Assert.AreEqual("Qwe, 123! G.", _cipher.Encrypt("Abc, 123! O.", KeyboardLayout.Qwerty));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encrypt_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Encrypt(text, KeyboardLayout.Qwerty));

    // ---- Round trips for every built-in layout ----

    [DataTestMethod]
    [DataRow(KeyboardLayout.Qwerty)]
    [DataRow(KeyboardLayout.Azerty)]
    [DataRow(KeyboardLayout.Qwertz)]
    [DataRow(KeyboardLayout.Dvorak)]
    [DataRow(KeyboardLayout.Colemak)]
    public void Decrypt_OfEncrypt_RoundTrips(KeyboardLayout layout)
    {
        const string plain = "The Quick Brown Fox jumps over N 49 13.456!";
        var encrypted = _cipher.Encrypt(plain, layout);
        Assert.AreEqual(plain, _cipher.Decrypt(encrypted, layout));
    }

    [DataTestMethod]
    [DataRow(KeyboardLayout.Qwerty)]
    [DataRow(KeyboardLayout.Azerty)]
    [DataRow(KeyboardLayout.Qwertz)]
    [DataRow(KeyboardLayout.Dvorak)]
    [DataRow(KeyboardLayout.Colemak)]
    public void Encrypt_FullAlphabet_IsAPermutation(KeyboardLayout layout)
    {
        var encrypted = _cipher.Encrypt(KeyboardCipher.Alphabet, layout);
        CollectionAssert.AreEquivalent(KeyboardCipher.Alphabet.ToCharArray(), encrypted.ToCharArray());
        Assert.AreEqual(KeyboardCipher.GetLayoutOrder(layout), encrypted);
    }

    // ---- Layout orders are valid permutations ----

    [DataTestMethod]
    [DataRow(KeyboardLayout.Qwerty, "QWERTYUIOPASDFGHJKLZXCVBNM")]
    [DataRow(KeyboardLayout.Azerty, "AZERTYUIOPQSDFGHJKLMWXCVBN")]
    [DataRow(KeyboardLayout.Qwertz, "QWERTZUIOPASDFGHJKLYXCVBNM")]
    [DataRow(KeyboardLayout.Dvorak, "PYFGCRLAOEUIDHTNSQJKXBMWVZ")]
    [DataRow(KeyboardLayout.Colemak, "QWFPGJLUYARSTDHNEIOZXCVBKM")]
    public void GetLayoutOrder_ReturnsExpectedKeyOrder(KeyboardLayout layout, string expected)
    {
        Assert.AreEqual(expected, KeyboardCipher.GetLayoutOrder(layout));
        Assert.IsTrue(KeyboardCipher.IsValidLayout(expected));
    }

    // ---- Custom layout validation ----

    [TestMethod]
    public void IsValidLayout_FullPermutation_ReturnsTrue()
        => Assert.IsTrue(KeyboardCipher.IsValidLayout("ZYXWVUTSRQPONMLKJIHGFEDCBA"));

    [DataTestMethod]
    [DataRow("")]                            // empty
    [DataRow("ABC")]                         // too short
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYZA")] // too long
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYA")]  // missing Z, duplicate A
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXY1")]  // contains a digit
    public void IsValidLayout_NonPermutation_ReturnsFalse(string layout)
        => Assert.IsFalse(KeyboardCipher.IsValidLayout(layout));

    [TestMethod]
    public void IsValidLayout_AcceptsLowercaseAndIsCaseInsensitive()
        => Assert.IsTrue(KeyboardCipher.IsValidLayout("qwertyuiopasdfghjklzxcvbnm"));

    [TestMethod]
    public void Transform_CustomLayout_RoundTrips()
    {
        const string custom = "MNBVCXZLKJHGFDSAPOIUYTREWQ";
        var encrypted = _cipher.Transform("GEOCACHE", custom, KeyboardCipherDirection.Encrypt);
        Assert.AreEqual("GEOCACHE", _cipher.Transform(encrypted, custom, KeyboardCipherDirection.Decrypt));
    }

    [TestMethod]
    public void Transform_CustomLayout_MatchesQwertyOrder()
        => Assert.AreEqual("QWE", _cipher.Transform("ABC", "QWERTYUIOPASDFGHJKLZXCVBNM", KeyboardCipherDirection.Encrypt));

    [TestMethod]
    public void Transform_InvalidCustomLayout_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() => _cipher.Transform("ABC", "NOTAPERMUTATION", KeyboardCipherDirection.Encrypt));

    // ---- Mapping table ----

    [TestMethod]
    public void GetMapping_Qwerty_PairsAlphabetWithLayout()
    {
        var mapping = _cipher.GetMapping(KeyboardLayout.Qwerty);

        Assert.AreEqual(26, mapping.Count);
        Assert.AreEqual(new KeyboardMappingEntry('A', 'Q'), mapping[0]);
        Assert.AreEqual(new KeyboardMappingEntry('B', 'W'), mapping[1]);
        Assert.AreEqual(new KeyboardMappingEntry('C', 'E'), mapping[2]);
        Assert.AreEqual(new KeyboardMappingEntry('Z', 'M'), mapping[25]);
    }

    // ---- Auto-detect ----

    [TestMethod]
    public void AutoDetect_ProducesEveryLayoutAndDirection()
    {
        var candidates = _cipher.AutoDetect("QWE");

        Assert.AreEqual(KeyboardCipher.BuiltInLayouts.Count * 2, candidates.Count);
        Assert.IsTrue(candidates.Any(c =>
            c is { Layout: KeyboardLayout.Qwerty, Direction: KeyboardCipherDirection.Decrypt, Text: "ABC" }));
    }

    [TestMethod]
    public void AutoDetect_RecoversPlaintextForUnknownLayout()
    {
        var encrypted = _cipher.Encrypt("GEOCACHE", KeyboardLayout.Dvorak);
        var candidates = _cipher.AutoDetect(encrypted);
        Assert.IsTrue(candidates.Any(c => c.Text == "GEOCACHE"));
    }
}
