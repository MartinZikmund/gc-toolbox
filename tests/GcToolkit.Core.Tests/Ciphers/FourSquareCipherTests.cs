using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class FourSquareCipherTests
{
    // ---- Square generation ----

    [TestMethod]
    public void BuildSquare_EmptyKeyword_IsPlainAlphabetWithoutJ()
    {
        var square = FourSquareCipher.BuildSquare("", FourSquareAlphabet.MergeJIntoI);
        Assert.AreEqual("ABCDEFGHIKLMNOPQRSTUVWXYZ", new string(square));
    }

    [TestMethod]
    public void BuildSquare_Example_DedupesThenFillsRest()
    {
        // "EXAMPLE" -> E X A M P L (dupe E dropped), then remaining alphabet (J merged into I).
        var square = FourSquareCipher.BuildSquare("EXAMPLE", FourSquareAlphabet.MergeJIntoI);
        Assert.AreEqual("EXAMPLBCDFGHIKNOQRSTUVWYZ", new string(square));
    }

    [TestMethod]
    public void BuildSquare_Keyword_DropsLetterNotInAlphabet()
    {
        // With J merged into I, a J inside the keyword is normalized to I.
        var square = FourSquareCipher.BuildSquare("JUMP", FourSquareAlphabet.MergeJIntoI);
        Assert.AreEqual('I', square[0]); // J -> I
        Assert.AreEqual(25, square.Length);
        CollectionAssert.AllItemsAreUnique(square);
    }

    [TestMethod]
    public void BuildSquare_SkipQ_ExcludesQAndKeepsJ()
    {
        var square = FourSquareCipher.BuildSquare("", FourSquareAlphabet.Skip('Q'));
        Assert.AreEqual("ABCDEFGHIJKLMNOPRSTUVWXYZ", new string(square));
        Assert.AreEqual(25, square.Length);
    }

    [TestMethod]
    public void BuildSquare_AlwaysProduces25UniqueLetters()
    {
        var square = FourSquareCipher.BuildSquare("THEQUICKBROWNFOXJUMPS", FourSquareAlphabet.MergeJIntoI);
        Assert.AreEqual(25, square.Length);
        CollectionAssert.AllItemsAreUnique(square);
    }

    // ---- Canonical Wikipedia vector ----

    // The canonical Wikipedia example uses the 25-letter alphabet A–Z without Q (its squares contain
    // both I and J), keywords "EXAMPLE" (top-right) and "KEYWORD" (bottom-left).
    [TestMethod]
    public void Encrypt_WikipediaVector_ProducesExpectedCiphertext()
    {
        var cipher = new FourSquareCipher("EXAMPLE", "KEYWORD", FourSquareAlphabet.Skip('Q'));
        var result = cipher.Encrypt("HELPMEOBIWANKENOBI");
        Assert.AreEqual("FYGMKYHOBXMFKKKIMD", result.Text);
    }

    [TestMethod]
    public void Decrypt_WikipediaVector_RecoversPlaintext()
    {
        var cipher = new FourSquareCipher("EXAMPLE", "KEYWORD", FourSquareAlphabet.Skip('Q'));
        var result = cipher.Decrypt("FYGMKYHOBXMFKKKIMD");
        Assert.AreEqual("HELPMEOBIWANKENOBI", result.Text);
    }

    [TestMethod]
    public void BuildSquare_WikipediaExample_MatchesArticleGrids()
    {
        var topRight = FourSquareCipher.BuildSquare("EXAMPLE", FourSquareAlphabet.Skip('Q'));
        var bottomLeft = FourSquareCipher.BuildSquare("KEYWORD", FourSquareAlphabet.Skip('Q'));
        Assert.AreEqual("EXAMPLBCDFGHIJKNORSTUVWYZ", new string(topRight));
        Assert.AreEqual("KEYWORDABCFGHIJLMNPSTUVXZ", new string(bottomLeft));
    }

    // ---- Round trips ----

    [DataTestMethod]
    [DataRow("GEOCACHE")]
    [DataRow("ATTACKATDAWN")]
    [DataRow("THEQUICKBROWNFOX")]
    public void RoundTrip_EncryptThenDecrypt_RecoversInput(string plain)
    {
        var cipher = new FourSquareCipher("SECRET", "PUZZLE", FourSquareAlphabet.MergeJIntoI);
        var encrypted = cipher.Encrypt(plain);
        var decrypted = cipher.Decrypt(encrypted.Text);
        Assert.AreEqual(plain, decrypted.Text);
    }

    // ---- Input sanitisation ----

    [TestMethod]
    public void Encrypt_StripsNonLettersAndUpperCases()
    {
        var cipher = new FourSquareCipher("EXAMPLE", "KEYWORD", FourSquareAlphabet.MergeJIntoI);
        var spaced = cipher.Encrypt("help me obi-wan kenobi");
        var clean = cipher.Encrypt("HELPMEOBIWANKENOBI");
        Assert.AreEqual(clean.Text, spaced.Text);
    }

    [TestMethod]
    public void Encrypt_MergeMode_MapsJToIBeforeEncoding()
    {
        var cipher = new FourSquareCipher("EXAMPLE", "KEYWORD", FourSquareAlphabet.MergeJIntoI);
        var withJ = cipher.Encrypt("JIM");   // J -> I
        var withI = cipher.Encrypt("IIM");
        Assert.AreEqual(withI.Text, withJ.Text);
    }

    // ---- Filler / odd length ----

    [TestMethod]
    public void Encrypt_OddLength_AppendsFillerAndFlags()
    {
        var cipher = new FourSquareCipher("EXAMPLE", "KEYWORD", FourSquareAlphabet.MergeJIntoI);
        var result = cipher.Encrypt("ABC");
        Assert.IsTrue(result.PaddingApplied);
        Assert.AreEqual(4, result.Text.Length); // "ABCX" -> two digraphs -> 4 cipher letters
    }

    [TestMethod]
    public void Encrypt_CustomFiller_UsesGivenLetter()
    {
        var cipher = new FourSquareCipher("EXAMPLE", "KEYWORD", FourSquareAlphabet.MergeJIntoI, filler: 'Z');
        var withZ = cipher.Encrypt("AB");
        // "AB" is even, no filler; "A" pads to "AZ".
        var padded = cipher.Encrypt("A");
        Assert.IsTrue(padded.PaddingApplied);
        Assert.AreEqual(2, padded.Text.Length);
        Assert.AreNotEqual(withZ.Text, padded.Text);
    }

    [TestMethod]
    public void Encrypt_EvenLength_DoesNotPad()
    {
        var cipher = new FourSquareCipher("EXAMPLE", "KEYWORD", FourSquareAlphabet.MergeJIntoI);
        var result = cipher.Encrypt("ABCD");
        Assert.IsFalse(result.PaddingApplied);
    }

    // ---- Empty / whitespace ----

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("!!! 123")]
    public void Encrypt_NoLetters_ReturnsEmpty(string input)
    {
        var cipher = new FourSquareCipher("EXAMPLE", "KEYWORD", FourSquareAlphabet.MergeJIntoI);
        var result = cipher.Encrypt(input);
        Assert.AreEqual(string.Empty, result.Text);
        Assert.IsFalse(result.PaddingApplied);
    }

    // ---- Skip mode round trip including the merged-away letter ----

    [TestMethod]
    public void RoundTrip_SkipMode_PreservesJ()
    {
        var cipher = new FourSquareCipher("SECRET", "PUZZLE", FourSquareAlphabet.Skip('Q'));
        const string plain = "JACKDAWS";
        var encrypted = cipher.Encrypt(plain);
        var decrypted = cipher.Decrypt(encrypted.Text);
        Assert.AreEqual(plain, decrypted.Text);
    }

    // ---- Manual / explicit squares ----

    [TestMethod]
    public void FromSquares_RoundTripsWithExplicitGrids()
    {
        var alphabet = FourSquareAlphabet.Skip('Q');
        var plainAlphabet = alphabet.Letters.ToCharArray();
        var topRight = FourSquareCipher.BuildSquare("EXAMPLE", alphabet);
        var bottomLeft = FourSquareCipher.BuildSquare("KEYWORD", alphabet);
        var cipher = FourSquareCipher.FromSquares(plainAlphabet, topRight, bottomLeft, alphabet);
        var encrypted = cipher.Encrypt("HELPMEOBIWANKENOBI");
        Assert.AreEqual("FYGMKYHOBXMFKKKIMD", encrypted.Text);
    }

    [TestMethod]
    public void FromSquares_IncompleteSquare_Throws()
    {
        var alphabet = FourSquareAlphabet.MergeJIntoI;
        var plainAlphabet = alphabet.Letters.ToCharArray();
        var bad = "ABC".ToCharArray();
        Assert.ThrowsExactly<ArgumentException>(() =>
            FourSquareCipher.FromSquares(plainAlphabet, bad, plainAlphabet, alphabet));
    }

    // ---- Active alphabet exposure (for the VM / view rendering) ----

    [TestMethod]
    public void Alphabet_MergeMode_OmitsJ()
    {
        var alphabet = FourSquareAlphabet.MergeJIntoI;
        Assert.IsFalse(alphabet.Letters.Contains('J'));
        Assert.AreEqual(25, alphabet.Letters.Length);
    }

    [TestMethod]
    public void Alphabet_SkipMode_OmitsChosenLetter()
    {
        var alphabet = FourSquareAlphabet.Skip('Z');
        Assert.IsFalse(alphabet.Letters.Contains('Z'));
        Assert.IsTrue(alphabet.Letters.Contains('J'));
        Assert.AreEqual(25, alphabet.Letters.Length);
    }
}
