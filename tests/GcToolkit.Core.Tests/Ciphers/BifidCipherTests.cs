using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class BifidCipherTests
{
    private readonly BifidCipher _cipher = new();

    // The canonical Wikipedia square: B G W K Z / Q P N D S / I O A X E / F C L U M / T H Y V R.
    private static BifidSquare WikipediaSquare()
        => BifidSquare.FromText("BGWKZQPNDSIOAXEFCLUMTHYVR");

    // ---- Square construction ----

    [TestMethod]
    public void FromKeyword_MergeJIntoI_FillsRemainingAlphabetInOrder()
    {
        // "KEYWORD" deduped = K E Y W O R D, then the rest of A–Z (J folded into I) in order.
        var square = BifidSquare.FromKeyword("KEYWORD", BifidLetterFit.MergeJIntoI);

        Assert.AreEqual("KEYWORDABCFGHILMNPQSTUVXZ", square.Letters);
        Assert.AreEqual(25, square.Letters.Length);
    }

    [TestMethod]
    public void FromKeyword_NoKeyword_IsTheMergedAlphabetInOrder()
    {
        var square = BifidSquare.FromKeyword("", BifidLetterFit.MergeJIntoI);
        Assert.AreEqual("ABCDEFGHIKLMNOPQRSTUVWXYZ", square.Letters); // J dropped
    }

    [TestMethod]
    public void FromKeyword_MergeIntoArbitraryLetter_DropsThatLetter()
    {
        // Merge Q into K (a non-default merge): Q never appears, K does.
        var square = BifidSquare.FromKeyword("", new BifidLetterFit(BifidFitKind.Merge, 'Q', 'K'));
        Assert.IsFalse(square.Letters.Contains('Q'));
        Assert.IsTrue(square.Letters.Contains('K'));
        Assert.AreEqual(25, square.Letters.Length);
    }

    [TestMethod]
    public void FromKeyword_SkipLetter_OmitsTheChosenLetter()
    {
        // Skip Q entirely (a common alternative reduction).
        var square = BifidSquare.FromKeyword("", BifidLetterFit.Skip('Q'));
        Assert.AreEqual("ABCDEFGHIJKLMNOPRSTUVWXYZ", square.Letters); // no Q
        Assert.AreEqual(25, square.Letters.Length);
    }

    [TestMethod]
    public void FromKeyword_KeywordFoldsCaseAndDuplicates()
    {
        var square = BifidSquare.FromKeyword("Geocache", BifidLetterFit.MergeJIntoI);
        Assert.AreEqual("GEOCAHBDFIKLMNPQRSTUVWXYZ", square.Letters);
    }

    [TestMethod]
    public void FromText_TooShort_Throws()
        => Assert.Throws<ArgumentException>(() => BifidSquare.FromText("ABC"));

    [TestMethod]
    public void FromText_DuplicateLetter_Throws()
        => Assert.Throws<ArgumentException>(() => BifidSquare.FromText("AABCDEFGHIKLMNOPQRSTUVWXY"));

    // ---- Coordinate mapping ----

    [TestMethod]
    public void Square_RowAndColumn_MapBackToTheSameLetter()
    {
        var square = WikipediaSquare();
        for (var i = 0; i < 25; i++)
        {
            var letter = square.Letters[i];
            var (row, col) = square.Find(letter);
            Assert.AreEqual(letter, square.LetterAt(row, col), $"index {i}");
        }
    }

    // ---- Canonical Wikipedia vector ----

    [TestMethod]
    public void Encrypt_WikipediaVector_ProducesUAEOLWRINS()
    {
        var result = _cipher.Encrypt("FLEEATONCE", WikipediaSquare());
        Assert.AreEqual("UAEOLWRINS", result.Text);
    }

    [TestMethod]
    public void Decrypt_WikipediaVector_RecoversPlaintext()
    {
        var result = _cipher.Decrypt("UAEOLWRINS", WikipediaSquare());
        Assert.AreEqual("FLEEATONCE", result.Text);
    }

    // ---- Round-trips ----

    [TestMethod]
    [DataRow("FLEEATONCE", 0)]
    [DataRow("GEOCACHING", 0)]
    [DataRow("THEQUICKBROWNFOXIUMPS", 0)]
    [DataRow("FLEEATONCE", 5)]
    [DataRow("ATTACKATDAWN", 4)]
    [DataRow("HELLOWORLD", 3)]
    public void EncryptThenDecrypt_RoundTrips(string plain, int period)
    {
        var square = WikipediaSquare();
        var encrypted = _cipher.Encrypt(plain, square, period).Text;
        var decrypted = _cipher.Decrypt(encrypted, square, period).Text;
        Assert.AreEqual(plain, decrypted);
    }

    [TestMethod]
    public void EncryptThenDecrypt_KeywordSquare_RoundTrips()
    {
        var square = BifidSquare.FromKeyword("GEOCACHE", BifidLetterFit.MergeJIntoI);
        var encrypted = _cipher.Encrypt("FINDTHECACHE", square).Text;
        Assert.AreEqual("FINDTHECACHE", _cipher.Decrypt(encrypted, square).Text);
    }

    // ---- Period / block length ----

    [TestMethod]
    public void Encrypt_PeriodOne_IsAnIdentityTransform()
    {
        // With block length 1, each letter fractionates within itself: row,col re-pairs to the same cell.
        var square = WikipediaSquare();
        Assert.AreEqual("FLEEATONCE", _cipher.Encrypt("FLEEATONCE", square, period: 1).Text);
    }

    [TestMethod]
    public void Encrypt_PeriodChangesOutput()
    {
        var square = WikipediaSquare();
        var whole = _cipher.Encrypt("FLEEATONCE", square, period: 0).Text;
        var blocks = _cipher.Encrypt("FLEEATONCE", square, period: 5).Text;
        Assert.AreNotEqual(whole, blocks);
    }

    // ---- J→I merge folding ----

    [TestMethod]
    public void Encrypt_FoldsJIntoIByDefault()
    {
        var square = WikipediaSquare();
        // J is not on the square; the default merge folds it to I, so "J" encrypts like "I".
        var withJ = _cipher.Encrypt("JOIN", square).Text;
        var withI = _cipher.Encrypt("IOIN", square).Text;
        Assert.AreEqual(withI, withJ);
    }

    [TestMethod]
    public void Encrypt_SkipFit_DropsTheSkippedLetterEntirely()
    {
        // Square that skips Q: a Q in the input is not on the square and is dropped.
        var square = BifidSquare.FromKeyword("", BifidLetterFit.Skip('Q'));
        var withQ = _cipher.Encrypt("QQABCQ", square).Text;
        var without = _cipher.Encrypt("ABC", square).Text;
        Assert.AreEqual(without, withQ);
    }

    // ---- Charset handling ----

    [TestMethod]
    public void Encrypt_StripsNonLettersAndIsCaseInsensitive()
    {
        var square = WikipediaSquare();
        var clean = _cipher.Encrypt("FLEEATONCE", square).Text;
        var noisy = _cipher.Encrypt("flee, at once!", square).Text;
        Assert.AreEqual(clean, noisy);
    }

    [TestMethod]
    public void Encrypt_ReportsSkippedNonAlphabetCharacters()
    {
        var square = WikipediaSquare();
        var result = _cipher.Encrypt("FLEE 123", square);
        Assert.IsTrue(result.HasIgnored);
    }

    // ---- Empty / null ----

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("12345")]
    public void Encrypt_NoUsableLetters_ReturnsEmpty(string? text)
    {
        var result = _cipher.Encrypt(text, WikipediaSquare());
        Assert.AreEqual(string.Empty, result.Text);
    }

    // ---- Validation ----

    [TestMethod]
    public void FromKeyword_KeywordWithNoAlphabetLetters_StillBuildsFullSquare()
    {
        // A keyword of only digits/punctuation contributes nothing; the square is the plain alphabet.
        var square = BifidSquare.FromKeyword("123!!", BifidLetterFit.MergeJIntoI);
        Assert.AreEqual(25, square.Letters.Length);
    }
}
