using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class AdfgvxCipherTests
{
    private readonly AdfgvxCipher _cipher = new();

    // The classic Wikipedia ADFGX worked example, J folded to I (an ADFGX square has no J).
    //   A D F G X
    // A b t a l p
    // D d h o z k
    // F q f v s n
    // G g i c u x
    // X m r e w y
    private const string WikipediaFill = "BTALPDHOZKQFVSNGICUXMREWY";
    private const string WikipediaKeyword = "CARGO";

    // ---- Square construction & alphabets ----

    [TestMethod]
    public void Size_Variants_Are5And6()
    {
        Assert.AreEqual(5, AdfgvxCipher.Size(AdfgvxVariant.Adfgx));
        Assert.AreEqual(6, AdfgvxCipher.Size(AdfgvxVariant.Adfgvx));
    }

    [TestMethod]
    public void Headers_MatchVariantLetters()
    {
        Assert.AreEqual("ADFGX", AdfgvxCipher.Headers(AdfgvxVariant.Adfgx));
        Assert.AreEqual("ADFGVX", AdfgvxCipher.Headers(AdfgvxVariant.Adfgvx));
    }

    [TestMethod]
    public void Alphabet_Adfgx_HasNoJ()
    {
        var alphabet = AdfgvxCipher.Alphabet(AdfgvxVariant.Adfgx);
        Assert.AreEqual(25, alphabet.Length);
        Assert.IsFalse(alphabet.Contains('J'));
    }

    [TestMethod]
    public void Alphabet_Adfgvx_HasLettersAndDigits()
    {
        var alphabet = AdfgvxCipher.Alphabet(AdfgvxVariant.Adfgvx);
        Assert.AreEqual(36, alphabet.Length);
        Assert.IsTrue(alphabet.Contains('J'));
        Assert.IsTrue(alphabet.Contains('0'));
        Assert.IsTrue(alphabet.Contains('9'));
    }

    [TestMethod]
    public void BuildSquare_OrderedAdfgx_ReturnsFlattened25()
    {
        var square = _cipher.BuildSquare(_cipher.OrderedFill(AdfgvxVariant.Adfgx), AdfgvxVariant.Adfgx);
        Assert.AreEqual("ABCDEFGHIKLMNOPQRSTUVWXYZ", square);
    }

    [TestMethod]
    public void BuildSquare_DuplicateCell_Throws()
    {
        var fill = "AABCDEFGHIKLMNOPQRSTUVWXY"; // two A's, missing Z
        var ex = Assert.ThrowsExactly<AdfgvxException>(() => _cipher.BuildSquare(fill, AdfgvxVariant.Adfgx));
        Assert.AreEqual(AdfgvxError.DuplicateCell, ex.Reason);
    }

    [TestMethod]
    public void BuildSquare_TooShort_ThrowsIncomplete()
    {
        var ex = Assert.ThrowsExactly<AdfgvxException>(() => _cipher.BuildSquare("ABCDE", AdfgvxVariant.Adfgx));
        Assert.AreEqual(AdfgvxError.IncompleteSquare, ex.Reason);
    }

    [TestMethod]
    public void BuildSquare_InvalidChar_Throws()
    {
        // J is not allowed in an ADFGX square.
        var fill = "JBCDEFGHIKLMNOPQRSTUVWXYZ";
        var ex = Assert.ThrowsExactly<AdfgvxException>(() => _cipher.BuildSquare(fill, AdfgvxVariant.Adfgx));
        Assert.AreEqual(AdfgvxError.InvalidSquareChar, ex.Reason);
    }

    [TestMethod]
    public void BuildSquare_IgnoresWhitespace()
    {
        var square = _cipher.BuildSquare("ABCDE\nFGHIK LMNOP\tQRSTU VWXYZ", AdfgvxVariant.Adfgx);
        Assert.AreEqual("ABCDEFGHIKLMNOPQRSTUVWXYZ", square);
    }

    // ---- Keyword-seeded square ----

    [TestMethod]
    public void KeywordSeededFill_PrependsDistinctKeyLetters()
    {
        var fill = _cipher.KeywordSeededFill("KEYWORD", AdfgvxVariant.Adfgvx);
        Assert.IsTrue(fill.StartsWith("KEYWORD"));
        Assert.AreEqual(36, fill.Length);
        Assert.AreEqual(36, fill.Distinct().Count());
    }

    [TestMethod]
    public void KeywordSeededFill_FoldsJToIForAdfgx()
    {
        // "JAM" -> I, A, M (J folds to I) then the rest of the alphabet.
        var fill = _cipher.KeywordSeededFill("JAM", AdfgvxVariant.Adfgx);
        Assert.IsTrue(fill.StartsWith("IAM"));
        Assert.AreEqual(25, fill.Length);
        Assert.AreEqual(25, fill.Distinct().Count());
    }

    [TestMethod]
    public void KeywordSeededFill_EmptyKey_ReturnsOrdered()
        => Assert.AreEqual(_cipher.OrderedFill(AdfgvxVariant.Adfgx), _cipher.KeywordSeededFill("", AdfgvxVariant.Adfgx));

    // ---- Stage 1 verified independently: substitution ----

    [TestMethod]
    public void SubstitutionSteps_WikipediaExample_ProducesKnownPairs()
    {
        var steps = _cipher.SubstitutionSteps("attack", WikipediaFill, AdfgvxVariant.Adfgx);
        // a=AF t=AD t=AD a=AF c=GF k=DX
        var pairs = string.Join("", steps.Select(s => s.Pair));
        Assert.AreEqual("AFADADAFGFDX", pairs);
        Assert.AreEqual('A', steps[0].PlainChar);
    }

    // ---- Stage 2 verified via the full known-answer vector ----

    [TestMethod]
    public void Encrypt_WikipediaExample_MatchesKnownCiphertext()
    {
        var result = _cipher.Encrypt("attack at once", WikipediaFill, WikipediaKeyword, AdfgvxVariant.Adfgx);
        Assert.AreEqual("FAXDFADDDGDGFFFAFAXAFAFX", result.Text);
    }

    [TestMethod]
    public void Decrypt_WikipediaCiphertext_RecoversPlaintext()
    {
        var result = _cipher.Decrypt("FAXDFADDDGDGFFFAFAXAFAFX", WikipediaFill, WikipediaKeyword, AdfgvxVariant.Adfgx);
        // Spaces aren't preserved by the cipher; J→I folding doesn't apply here.
        Assert.AreEqual("ATTACKATONCE", result.Text);
    }

    [TestMethod]
    public void Decrypt_AcceptsGroupedCiphertext()
    {
        // Operators traditionally group the ciphertext in fives; spaces must be ignored.
        var result = _cipher.Decrypt("FAXDF ADDDG DGFFF AFAXA FAFX", WikipediaFill, WikipediaKeyword, AdfgvxVariant.Adfgx);
        Assert.AreEqual("ATTACKATONCE", result.Text);
    }

    // ---- Round-trips ----

    [TestMethod]
    [DataRow("ATTACKATDAWN", "BATTLE")]
    [DataRow("THEQUICKBROWNFOXIUMPS", "SECRET")]
    [DataRow("GEOCACHE", "WALDO")]
    public void EncryptThenDecrypt_Adfgx_RoundTrips(string plain, string keyword)
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        var encrypted = _cipher.Encrypt(plain, fill, keyword, AdfgvxVariant.Adfgx);
        var decrypted = _cipher.Decrypt(encrypted.Text, fill, keyword, AdfgvxVariant.Adfgx);
        Assert.AreEqual(plain, decrypted.Text);
    }

    [TestMethod]
    [DataRow("ATTACK1234", "PRIVET")]
    [DataRow("N491357E0083042", "GEOCACHE")]
    [DataRow("THE0QUICK9BROWN8FOX", "ENIGMA")]
    public void EncryptThenDecrypt_Adfgvx_WithDigits_RoundTrips(string plain, string keyword)
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgvx);
        var encrypted = _cipher.Encrypt(plain, fill, keyword, AdfgvxVariant.Adfgvx);
        var decrypted = _cipher.Decrypt(encrypted.Text, fill, keyword, AdfgvxVariant.Adfgvx);
        Assert.AreEqual(plain, decrypted.Text);
    }

    [TestMethod]
    public void EncryptThenDecrypt_KeywordSeededSquare_RoundTrips()
    {
        var fill = _cipher.KeywordSeededFill("GEOCACHING", AdfgvxVariant.Adfgvx);
        var encrypted = _cipher.Encrypt("HIDDENCACHE42", fill, "MYSTERY", AdfgvxVariant.Adfgvx);
        var decrypted = _cipher.Decrypt(encrypted.Text, fill, "MYSTERY", AdfgvxVariant.Adfgvx);
        Assert.AreEqual("HIDDENCACHE42", decrypted.Text);
    }

    [TestMethod]
    public void EncryptThenDecrypt_RepeatedKeywordLetters_StableColumnOrder()
    {
        // A keyword with repeats exercises the stable tie-break in the column sort.
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        var encrypted = _cipher.Encrypt("MEETATDAWN", fill, "HELLO", AdfgvxVariant.Adfgx);
        var decrypted = _cipher.Decrypt(encrypted.Text, fill, "HELLO", AdfgvxVariant.Adfgx);
        Assert.AreEqual("MEETATDAWN", decrypted.Text);
    }

    // ---- Forgiving input ----

    [TestMethod]
    public void Encrypt_Adfgx_FoldsJToIAndFlags()
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        var result = _cipher.Encrypt("JAZZ", fill, "KEY", AdfgvxVariant.Adfgx);
        // J→I means "JAZZ" encrypts identically to "IAZZ".
        var asI = _cipher.Encrypt("IAZZ", fill, "KEY", AdfgvxVariant.Adfgx);
        Assert.AreEqual(asI.Text, result.Text);
        Assert.IsTrue(result.Notes.Contains(AdfgvxNote.FoldedJToI));
    }

    [TestMethod]
    public void Encrypt_DropsUnsupportedCharactersAndFlags()
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        var result = _cipher.Encrypt("HI, THERE!", fill, "KEY", AdfgvxVariant.Adfgx);
        var clean = _cipher.Encrypt("HITHERE", fill, "KEY", AdfgvxVariant.Adfgx);
        Assert.AreEqual(clean.Text, result.Text);
        Assert.IsTrue(result.Notes.Contains(AdfgvxNote.DroppedUnsupportedChars));
    }

    [TestMethod]
    public void Encrypt_Adfgvx_DigitsAreNotDropped()
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgvx);
        var result = _cipher.Encrypt("AB12", fill, "KEY", AdfgvxVariant.Adfgvx);
        Assert.IsFalse(result.Notes.Contains(AdfgvxNote.DroppedUnsupportedChars));
    }

    // ---- Validation / error paths ----

    [TestMethod]
    public void Encrypt_EmptyKeyword_Throws()
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        var ex = Assert.ThrowsExactly<AdfgvxException>(() => _cipher.Encrypt("HI", fill, "   ", AdfgvxVariant.Adfgx));
        Assert.AreEqual(AdfgvxError.EmptyKeyword, ex.Reason);
    }

    [TestMethod]
    public void Decrypt_EmptyCiphertext_Throws()
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        var ex = Assert.ThrowsExactly<AdfgvxException>(() => _cipher.Decrypt("", fill, "KEY", AdfgvxVariant.Adfgx));
        Assert.AreEqual(AdfgvxError.MalformedCiphertext, ex.Reason);
    }

    [TestMethod]
    public void Decrypt_OddLengthCiphertext_Throws()
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        // Five header letters -> odd number of substitution symbols.
        var ex = Assert.ThrowsExactly<AdfgvxException>(() => _cipher.Decrypt("ADFGX", fill, "KEY", AdfgvxVariant.Adfgx));
        Assert.AreEqual(AdfgvxError.MalformedCiphertext, ex.Reason);
    }

    [TestMethod]
    public void Decrypt_NonHeaderLettersIgnored_ThenValid()
    {
        // 'B','C' aren't ADFGX headers; the four headers AD FG round-trip cleanly.
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        var result = _cipher.Decrypt("ADbcFG", fill, "K", AdfgvxVariant.Adfgx);
        Assert.AreEqual(2, result.Text.Length);
    }

    [TestMethod]
    public void Encrypt_EmptyPlaintext_ReturnsEmpty()
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        var result = _cipher.Encrypt("", fill, "KEY", AdfgvxVariant.Adfgx);
        Assert.AreEqual(string.Empty, result.Text);
    }

    [TestMethod]
    public void Encrypt_PlaintextOfOnlyPunctuation_ReturnsEmptyWithDropNote()
    {
        var fill = _cipher.OrderedFill(AdfgvxVariant.Adfgx);
        var result = _cipher.Encrypt("!!! ???", fill, "KEY", AdfgvxVariant.Adfgx);
        Assert.AreEqual(string.Empty, result.Text);
        Assert.IsTrue(result.Notes.Contains(AdfgvxNote.DroppedUnsupportedChars));
    }
}
