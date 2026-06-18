using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class PlayfairCipherTests
{
    private readonly PlayfairCipher _cipher = new();

    private static PlayfairOptions Standard(string keyword = "") => new() { Keyword = keyword };

    // ---- Key square construction ----

    [TestMethod]
    public void BuildSquare_NoKeyword_IsAlphabetWithoutJ()
    {
        var square = _cipher.BuildSquare(Standard());

        Assert.AreEqual("ABCDEFGHIKLMNOPQRSTUVWXYZ", Flatten(square));
    }

    [TestMethod]
    public void BuildSquare_Keyword_PlacesKeyLettersFirstThenRemainingAlphabet()
    {
        // "playfair example" → distinct letters P L A Y F I R E X M, then the rest (J merged into I).
        var square = _cipher.BuildSquare(Standard("playfair example"));

        Assert.AreEqual("PLAYFIREXMBCDGHKNOQSTUVWZ", Flatten(square));
    }

    [TestMethod]
    public void BuildSquare_Is5By5Rows()
    {
        var square = _cipher.BuildSquare(Standard("playfair"));

        Assert.AreEqual(5, square.Count);
        foreach (var row in square)
        {
            Assert.AreEqual(5, row.Length);
        }
    }

    [TestMethod]
    public void BuildSquare_MergeDropsTheMergedLetter_DefaultJIntoI()
    {
        var square = Flatten(_cipher.BuildSquare(Standard("jazz")));

        Assert.IsFalse(square.Contains('J'));
        Assert.IsTrue(square.Contains('I'));
        Assert.AreEqual(25, square.Length);
    }

    [TestMethod]
    public void BuildSquare_SkipFit_OmitsTheChosenLetterAndKeepsBoth_I_And_J()
    {
        var options = new PlayfairOptions { Keyword = "quack", Fit = PlayfairFit.Skip, SkipLetter = 'Q' };
        var square = Flatten(_cipher.BuildSquare(options));

        Assert.IsFalse(square.Contains('Q'));
        Assert.IsTrue(square.Contains('I'));
        Assert.IsTrue(square.Contains('J'));
        Assert.AreEqual(25, square.Length);
    }

    [TestMethod]
    public void BuildSquare_ColumnMajorFill_TransposesTheRowMajorSquare()
    {
        var rowMajor = Flatten(_cipher.BuildSquare(new PlayfairOptions { Keyword = "playfair example" }));
        var columnMajor = Flatten(_cipher.BuildSquare(new PlayfairOptions { Keyword = "playfair example", FillOrder = PlayfairFillOrder.ColumnMajor }));

        // Column-major fill reads down columns; transposing the row-major layout must reproduce it.
        var transposed = new char[25];
        for (var i = 0; i < 25; i++)
        {
            var r = i / 5;
            var c = i % 5;
            transposed[c * 5 + r] = rowMajor[i];
        }

        Assert.AreEqual(new string(transposed), columnMajor);
    }

    [TestMethod]
    public void BuildSquare_ManualSquare_UsesTheProvidedCellsVerbatim()
    {
        const string manual = "ZYXWVUTSRQPONMLKIHGFEDCBA";
        var square = Flatten(_cipher.BuildSquare(new PlayfairOptions { ManualSquare = manual }));

        Assert.AreEqual(manual, square);
    }

    [TestMethod]
    public void BuildSquare_EmptyKeyword_FallsBackToPlainAlphabet()
    {
        var square = Flatten(_cipher.BuildSquare(new PlayfairOptions { Keyword = "   " }));

        Assert.AreEqual("ABCDEFGHIKLMNOPQRSTUVWXYZ", square);
    }

    // ---- Canonical Wikipedia vector ----

    [TestMethod]
    public void Encrypt_WikipediaVector_ProducesKnownCiphertext()
    {
        var result = _cipher.Encrypt("hide the gold in the tree stump", Standard("playfair example"));

        Assert.AreEqual("BMODZBXDNABEKUDMUIXMMOUVIF", result.Text);
    }

    [TestMethod]
    public void Decrypt_WikipediaVector_RecoversThePaddedPlaintext()
    {
        var result = _cipher.Decrypt("BMODZBXDNABEKUDMUIXMMOUVIF", Standard("playfair example"));

        // Decryption yields the normalized, filler-padded plaintext (X separates the double E).
        Assert.AreEqual("HIDETHEGOLDINTHETREXESTUMP", result.Text);
    }

    // ---- Digraph rules ----

    [TestMethod]
    public void Encrypt_SameRow_ShiftsRight_WithWrap()
    {
        // Default square row 0 = A B C D E. AB→BC (right); DE→E then A wraps → "EA".
        var result = _cipher.Encrypt("abde", Standard());

        Assert.AreEqual("BCEA", result.Text);
    }

    [TestMethod]
    public void Encrypt_SameColumn_ShiftsDown_WithWrap()
    {
        // Default square column 0 = A F L Q V. AF→FL (down), and VA wraps: V→A, A→F → "FA"? check:
        // pair "AV": A(row0,col0) V(row4,col0) same column → A↓=F, V↓ wraps to A → "FA".
        var result = _cipher.Encrypt("av", Standard());

        Assert.AreEqual("FA", result.Text);
    }

    [TestMethod]
    public void Encrypt_Rectangle_SwapsToSameRowOppositeCorner()
    {
        // Default square: A(r0,c0) G(r1,c1) → rectangle → A takes its row at G's column = B,
        // G takes its row at A's column = F. "AG" → "BF".
        var result = _cipher.Encrypt("ag", Standard());

        Assert.AreEqual("BF", result.Text);
    }

    // ---- Double letters / fillers / padding ----

    [TestMethod]
    public void Encrypt_RepeatedLetterPair_InsertsFillerBetweenThem()
    {
        // "BALLOON": B A L L O O N → with X splits: BA LX LO ON XN? trace via normalization.
        var result = _cipher.Encrypt("balloon", Standard());

        // Decrypting must round-trip back to the inserted-filler form.
        var back = _cipher.Decrypt(result.Text, Standard());
        Assert.IsTrue(back.Text.StartsWith("BALXLO", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Encrypt_OddLengthPlaintext_PadsFinalDigraphWithFiller()
    {
        var result = _cipher.Encrypt("hi", Standard("playfair example"));

        // "HI" is one digraph (even) → no padding, length 2.
        Assert.AreEqual(2, result.Text.Length);

        var odd = _cipher.Encrypt("bug", Standard());
        Assert.AreEqual(4, odd.Text.Length); // BU GX → 4 chars
    }

    [TestMethod]
    public void Encrypt_SelectableFiller_UsesTheConfiguredLetter()
    {
        var options = new PlayfairOptions { Filler = 'Q', Fit = PlayfairFit.Merge };
        var result = _cipher.Encrypt("aa", options);

        // Repeated A split by Q → "AQ" then padded "A" → "AQAQ" digraphs; decrypt recovers AQAQ.
        var back = _cipher.Decrypt(result.Text, options);
        Assert.IsTrue(back.Text.Contains('Q'));
    }

    [TestMethod]
    public void Encrypt_DoubleLetterAtEndUsesAlternateFiller_WhenFillerEqualsTheLetter()
    {
        // If the repeated letter IS the filler (e.g. "XX" with filler X), a different filler must be used.
        var result = _cipher.Encrypt("xx", new PlayfairOptions { Filler = 'X' });
        var back = _cipher.Decrypt(result.Text, new PlayfairOptions { Filler = 'X' });

        // The split character must differ from X so the pair is no longer a double.
        Assert.AreEqual('X', back.Text[0]);
        Assert.AreNotEqual('X', back.Text[1]);
    }

    [TestMethod]
    public void Encrypt_NoSplit_EncodesDoubleLetterByMovingDownAndRight()
    {
        // With SplitDoubles=false a same-letter pair is encoded directly (down+right of each).
        var options = new PlayfairOptions { Keyword = "playfair example", SplitDoubles = false };
        var result = _cipher.Encrypt("ee", options);

        // No filler is inserted, so the digraph count matches the (even) input length.
        Assert.AreEqual(2, result.Text.Length);
    }

    // ---- Normalization / stripping ----

    [TestMethod]
    public void Encrypt_StripsNonLettersAndReportsThem()
    {
        var result = _cipher.Encrypt("Hi, there! 123", Standard("playfair example"));

        Assert.IsTrue(result.StrippedCount > 0);
        Assert.IsTrue(result.NormalizedInput.All(char.IsLetter));
    }

    [TestMethod]
    public void Encrypt_MergeRule_FoldsJOntoIInTheInput()
    {
        var merged = _cipher.Encrypt("jump", Standard("playfair example"));
        var asI = _cipher.Encrypt("ium p", Standard("playfair example"));

        Assert.AreEqual(asI.Text, merged.Text);
    }

    [TestMethod]
    public void Encrypt_SkipRule_RemovesTheSkippedLetterFromInput()
    {
        var options = new PlayfairOptions { Keyword = "keyword", Fit = PlayfairFit.Skip, SkipLetter = 'Q' };
        var result = _cipher.Encrypt("aqua", options);

        // Q is removed during normalization, so it never appears in the normalized input.
        Assert.IsFalse(result.NormalizedInput.Contains('Q'));
    }

    // ---- Round trips ----

    [TestMethod]
    [DataRow("playfair example", "hide the gold in the tree stump")]
    [DataRow("monarchy", "instruments")]
    [DataRow("secret", "attackatdawn")]
    [DataRow("", "the quick brown fox")]
    public void RoundTrip_DecryptOfEncrypt_RecoversNormalizedPlaintext(string keyword, string plaintext)
    {
        var options = Standard(keyword);
        var encrypted = _cipher.Encrypt(plaintext, options);
        var decrypted = _cipher.Decrypt(encrypted.Text, options);

        // The decrypted text is the encryptor's normalized (filler-inserted) plaintext.
        Assert.AreEqual(encrypted.NormalizedInput, decrypted.Text);
    }

    // ---- Validation ----

    [TestMethod]
    public void Encrypt_EmptyInput_ProducesEmptyResultWithoutError()
    {
        var result = _cipher.Encrypt("   ", Standard("playfair example"));

        Assert.AreEqual(string.Empty, result.Text);
        Assert.IsFalse(result.HasError);
    }

    [TestMethod]
    public void Encrypt_InputWithNoLetters_ProducesEmptyResult()
    {
        var result = _cipher.Encrypt("12345 !!!", Standard());

        Assert.AreEqual(string.Empty, result.Text);
    }

    [TestMethod]
    public void BuildSquare_ManualSquareNotDistinct_Throws()
    {
        // 25 chars but with a duplicate is invalid.
        Assert.ThrowsExactly<ArgumentException>(
            () => _cipher.BuildSquare(new PlayfairOptions { ManualSquare = "AABCDEFGHIKLMNOPQRSTUVWXY" }));
    }

    [TestMethod]
    public void BuildSquare_ManualSquareWrongLength_Throws()
        => Assert.ThrowsExactly<ArgumentException>(
            () => _cipher.BuildSquare(new PlayfairOptions { ManualSquare = "ABCDE" }));

    [TestMethod]
    public void Decrypt_OddLengthCiphertext_ReportsError()
    {
        var result = _cipher.Decrypt("ABC", Standard());

        Assert.IsTrue(result.HasError);
        Assert.AreEqual(string.Empty, result.Text);
    }

    private static string Flatten(IReadOnlyList<string> square) => string.Concat(square);
}
