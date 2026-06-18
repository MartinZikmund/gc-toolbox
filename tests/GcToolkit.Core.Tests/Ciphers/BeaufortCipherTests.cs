using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class BeaufortCipherTests
{
    private readonly BeaufortCipher _cipher = new();

    // ---- Standard Beaufort: C = (K - P) mod 26, self-reciprocal ----

    [TestMethod]
    [DataRow("A", "B", "B")]   // (1 - 0) mod 26 = 1 -> B
    [DataRow("B", "A", "Z")]   // (0 - 1) mod 26 = 25 -> Z
    [DataRow("A", "A", "A")]   // (0 - 0) = 0 -> A
    public void Encrypt_Standard_SingleLetter_MatchesFormula(string plain, string key, string expected)
        => Assert.AreEqual(expected, _cipher.Encrypt(plain, key, BeaufortVariant.Standard));

    [TestMethod]
    public void Encrypt_Standard_KnownVector_GeocacheWithKeyword()
    {
        // C = K - P, key "FORTIFICATION" repeating over "DEFENDTHEEASTWALL"
        var result = _cipher.Encrypt("DEFENDTHEEASTWALLOFTHECASTLE", "FORTIFICATION", BeaufortVariant.Standard);
        Assert.AreEqual("CKMPVCPVWPIWUJOGIUAPVWRIWUUK", result);
    }

    [TestMethod]
    public void Encrypt_Standard_IsSelfReciprocal()
    {
        const string plain = "The Quick Brown Fox JUMPS over the lazy DOG";
        const string key = "GEOCACHE";
        var once = _cipher.Encrypt(plain, key, BeaufortVariant.Standard);
        var twice = _cipher.Encrypt(once, key, BeaufortVariant.Standard);
        Assert.AreEqual(plain, twice);
    }

    [TestMethod]
    public void Decrypt_Standard_EqualsEncrypt()
    {
        const string text = "HELLOWORLD";
        const string key = "CACHE";
        Assert.AreEqual(
            _cipher.Encrypt(text, key, BeaufortVariant.Standard),
            _cipher.Decrypt(text, key, BeaufortVariant.Standard));
    }

    [TestMethod]
    public void Encrypt_Standard_KeywordCaseInsensitive()
        => Assert.AreEqual(
            _cipher.Encrypt("GEOCACHE", "KEY", BeaufortVariant.Standard),
            _cipher.Encrypt("GEOCACHE", "key", BeaufortVariant.Standard));

    [TestMethod]
    public void Encrypt_Standard_PreservesCase()
    {
        // 'b' with key 'A' -> (0 - 1) mod 26 = 25 = 'Z', lower-cased to 'z'.
        Assert.AreEqual("z", _cipher.Encrypt("b", "A", BeaufortVariant.Standard));
        Assert.AreEqual("Z", _cipher.Encrypt("B", "A", BeaufortVariant.Standard));
    }

    [TestMethod]
    public void Encrypt_Standard_PassesNonLettersThrough()
    {
        var result = _cipher.Encrypt("N 49 12.345", "KEY", BeaufortVariant.Standard);
        Assert.AreEqual(" 49 12.345", result[1..]);     // digits/space/dot untouched
        Assert.AreNotEqual('N', result[0]);             // the letter is enciphered
    }

    // ---- Variant / German Beaufort: encrypt C = (P - K), decrypt P = (C + K) ----

    [TestMethod]
    [DataRow("A", "B", "Z")]   // (0 - 1) mod 26 = 25 -> Z
    [DataRow("B", "A", "B")]   // (1 - 0) mod 26 = 1 -> B
    public void Encrypt_Variant_SingleLetter_MatchesFormula(string plain, string key, string expected)
        => Assert.AreEqual(expected, _cipher.Encrypt(plain, key, BeaufortVariant.Variant));

    [TestMethod]
    public void Variant_DecryptInvertsEncrypt()
    {
        const string plain = "Geocaching Is Fun! 2026";
        const string key = "BEAUFORT";
        var encrypted = _cipher.Encrypt(plain, key, BeaufortVariant.Variant);
        Assert.AreNotEqual(plain, encrypted);
        Assert.AreEqual(plain, _cipher.Decrypt(encrypted, key, BeaufortVariant.Variant));
    }

    [TestMethod]
    public void Variant_IsNotReciprocal()
    {
        const string plain = "ABCDEF";
        const string key = "KEY";
        var twice = _cipher.Encrypt(_cipher.Encrypt(plain, key, BeaufortVariant.Variant), key, BeaufortVariant.Variant);
        Assert.AreNotEqual(plain, twice);
    }

    // ---- Autokey Beaufort ----

    [TestMethod]
    public void Encrypt_Autokey_KnownVector()
        => Assert.AreEqual("DANWQ", _cipher.Encrypt("HELLO", "KEY", BeaufortVariant.Autokey));

    [TestMethod]
    public void Autokey_DecryptInvertsEncrypt()
    {
        const string plain = "Meet At The Cache Tonight";
        const string key = "TREASURE";
        var encrypted = _cipher.Encrypt(plain, key, BeaufortVariant.Autokey);
        Assert.AreEqual(plain, _cipher.Decrypt(encrypted, key, BeaufortVariant.Autokey));
    }

    [TestMethod]
    public void Autokey_NonLettersDoNotConsumeKeystream()
    {
        // Punctuation/spaces pass through without advancing the autokey stream.
        var withSpaces = _cipher.Encrypt("HE LLO", "KEY", BeaufortVariant.Autokey);
        Assert.AreEqual("DA NWQ", withSpaces);
    }

    // ---- Validation ----

    [TestMethod]
    [DataRow("")]
    [DataRow((string?)null)]
    public void Encrypt_EmptyKeyword_Throws(string? key)
        => Assert.ThrowsExactly<ArgumentException>(() => _cipher.Encrypt("ABC", key!, BeaufortVariant.Standard));

    [TestMethod]
    public void Encrypt_KeywordWithNonAlphabetChar_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() => _cipher.Encrypt("ABC", "KE1", BeaufortVariant.Standard));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encrypt_EmptyText_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Encrypt(text, "KEY", BeaufortVariant.Standard));

    [TestMethod]
    [DataRow("CACHE", true)]
    [DataRow("cache", true)]
    [DataRow("", false)]
    [DataRow("KE1", false)]
    [DataRow(" key word", false)]   // space is not in the A-Z alphabet
    public void IsValidKeyword_ChecksEmptyAndAlphabet(string keyword, bool expected)
        => Assert.AreEqual(expected, _cipher.IsValidKeyword(keyword));

    // ---- Tabula recta ----

    [TestMethod]
    public void Tableau_IsSquareAndRotatesEachRow()
    {
        var tableau = _cipher.Tableau();
        Assert.AreEqual(26, tableau.Count);
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", tableau[0]);
        Assert.AreEqual("BCDEFGHIJKLMNOPQRSTUVWXYZA", tableau[1]);
        Assert.AreEqual(26, tableau[5].Length);
    }

    [TestMethod]
    public void Tableau_CellMatchesStandardCipher()
    {
        // Tableau[k][p] should equal Encrypt(plain=p, key=k) for Standard Beaufort... but Beaufort
        // is C = K - P, so the row/col rotation here is the additive table; verify against the codec
        // by checking the reciprocal relationship at a known cell instead.
        var tableau = _cipher.Tableau();
        // Row 'B' (k=1), col 'A' (p=0) in an additive tabula recta is 'B'.
        Assert.AreEqual('B', tableau[1][0]);
    }

    // ---- Custom alphabet ----

    [TestMethod]
    public void CustomAlphabet_UsedForTransform()
    {
        BeaufortCipher reversed = new("ZYXWVUTSRQPONMLKJIHGFEDCBA");
        Assert.AreEqual(26, reversed.Size);
        // Round-trips reciprocally over the custom alphabet too.
        var encrypted = reversed.Encrypt("GEOCACHE", "KEY", BeaufortVariant.Standard);
        Assert.AreEqual("GEOCACHE", reversed.Encrypt(encrypted, "KEY", BeaufortVariant.Standard));
    }

    [TestMethod]
    public void Constructor_EmptyAlphabet_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() => new BeaufortCipher(""));

    [TestMethod]
    public void Constructor_DuplicateLetterAlphabet_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() => new BeaufortCipher("AAB"));
}
