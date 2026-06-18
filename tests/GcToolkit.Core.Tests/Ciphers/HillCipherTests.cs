using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class HillCipherTests
{
    private readonly HillCipher _cipher = new();

    // Wikipedia 3x3 canonical key.
    private static int[,] WikiKey3 => new[,]
    {
        { 6, 24, 1 },
        { 13, 16, 10 },
        { 20, 17, 15 },
    };

    // A standard invertible 2x2 key (det = 9, gcd(9,26)=1).
    private static int[,] Key2 => new[,]
    {
        { 3, 3 },
        { 2, 5 },
    };

    // ---- Canonical Wikipedia 3x3 vector ----

    [TestMethod]
    public void Encrypt_WikipediaVector_ProducesPOH()
    {
        var result = _cipher.Encrypt("ACT", WikiKey3);
        Assert.AreEqual(HillStatus.Ok, result.Status);
        Assert.AreEqual("POH", result.Text);
    }

    [TestMethod]
    public void Decrypt_WikipediaVector_RecoversACT()
    {
        var result = _cipher.Decrypt("POH", WikiKey3);
        Assert.AreEqual(HillStatus.Ok, result.Status);
        Assert.AreEqual("ACT", result.Text);
    }

    [TestMethod]
    public void Encrypt_WikipediaLongerVector_MatchesKnownCiphertext()
    {
        // Wikipedia worked example: CAT -> FIN.
        Assert.AreEqual("FIN", _cipher.Encrypt("CAT", WikiKey3).Text);
    }

    // ---- 2x2 round-trip ----

    [DataTestMethod]
    [DataRow("HELLO")]
    [DataRow("GEOCACHE")]
    [DataRow("HILLCIPHER")]
    public void Decrypt_Of2x2Encrypt_RoundTripsLetters(string plain)
    {
        var encrypted = _cipher.Encrypt(plain, Key2);
        Assert.AreEqual(HillStatus.Ok, encrypted.Status);

        var decrypted = _cipher.Decrypt(encrypted.Text, Key2);
        Assert.AreEqual(HillStatus.Ok, decrypted.Status);

        // Decrypt restores the original letters (plus any pad letters on an odd-length input).
        StringAssert.StartsWith(decrypted.Text, plain);
    }

    [TestMethod]
    public void Encrypt_OddLength2x2_PadsFinalBlock()
    {
        // "HI" + odd "X" -> three letters padded to four with the default pad 'X'.
        var result = _cipher.Encrypt("HIX", Key2);
        Assert.AreEqual(HillStatus.Ok, result.Status);
        Assert.AreEqual(4, result.Text.Length);
    }

    [TestMethod]
    public void Encrypt_CustomPadCharacter_UsedForFinalBlock()
    {
        var encryptedX = _cipher.Encrypt("ACT", Key2, pad: 'X');
        var encryptedZ = _cipher.Encrypt("ACT", Key2, pad: 'Z');
        // Different pad letters yield different padding, so the final block differs.
        Assert.AreNotEqual(encryptedX.Text, encryptedZ.Text);
    }

    // ---- Non-letters pass through ----

    [TestMethod]
    public void Encrypt_NonLetters_PassThroughInPlace()
    {
        var result = _cipher.Encrypt("AC-T", WikiKey3);
        // The hyphen stays put; ACT still encodes to POH around it.
        Assert.AreEqual("PO-H", result.Text);
    }

    [TestMethod]
    public void Encrypt_LowercaseInput_NormalizedToUpper()
        => Assert.AreEqual("POH", _cipher.Encrypt("act", WikiKey3).Text);

    // ---- Invertibility validation ----

    [TestMethod]
    public void Decrypt_NonInvertibleKey_ReturnsNotInvertible()
    {
        // det = 2, gcd(2,26)=2 -> no inverse mod 26.
        int[,] singular = { { 1, 1 }, { 1, 3 } };
        var result = _cipher.Decrypt("ABCD", singular);
        Assert.AreEqual(HillStatus.NotInvertible, result.Status);
        Assert.AreEqual(string.Empty, result.Text);
    }

    [TestMethod]
    public void Encrypt_NonInvertibleKey_ReturnsNotInvertible()
    {
        int[,] singular = { { 2, 4 }, { 6, 8 } };
        Assert.AreEqual(HillStatus.NotInvertible, _cipher.Encrypt("ABCD", singular).Status);
    }

    [TestMethod]
    public void IsInvertible_Distinguishes_Valid_From_Singular()
    {
        Assert.IsTrue(_cipher.IsInvertible(Key2));
        Assert.IsTrue(_cipher.IsInvertible(WikiKey3));
        Assert.IsFalse(_cipher.IsInvertible(new[,] { { 1, 1 }, { 1, 3 } }));
    }

    [TestMethod]
    public void DeterminantMod_WikiKey_Is_InRange_And_Coprime()
    {
        var det = _cipher.DeterminantMod(WikiKey3);
        Assert.IsTrue(det is >= 0 and < 26);
        Assert.AreEqual(1, System.Numerics.BigInteger.GreatestCommonDivisor(det, 26));
    }

    // ---- Matrix inverse ----

    [TestMethod]
    public void InvertMatrix_WikiKey_ProducesKnownInverse()
    {
        var inverse = _cipher.InvertMatrix(WikiKey3);
        Assert.AreEqual(HillStatus.Ok, inverse.Status);

        int[,] expected =
        {
            { 8, 5, 10 },
            { 21, 8, 21 },
            { 21, 12, 8 },
        };

        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                Assert.AreEqual(expected[r, c], inverse.Inverse[r, c], $"[{r},{c}]");
            }
        }
    }

    [TestMethod]
    public void InvertMatrix_TimesKey_IsIdentity()
    {
        var inverse = _cipher.InvertMatrix(WikiKey3);
        var product = _cipher.Multiply(WikiKey3, inverse.Inverse);

        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                Assert.AreEqual(r == c ? 1 : 0, product[r, c], $"[{r},{c}]");
            }
        }
    }

    [TestMethod]
    public void InvertMatrix_NonInvertible_ReturnsStatus()
        => Assert.AreEqual(HillStatus.NotInvertible, _cipher.InvertMatrix(new[,] { { 1, 1 }, { 1, 3 } }).Status);

    // ---- Keyword-derived keys ----

    [TestMethod]
    public void KeyFromKeyword_FillsRowMajor()
    {
        // "GYBNQKURP" -> the classic Wikipedia keyword matrix.
        var matrix = _cipher.KeyFromKeyword("GYBNQKURP", 3);
        Assert.IsNotNull(matrix);
        int[,] expected =
        {
            { 6, 24, 1 },
            { 13, 16, 10 },
            { 20, 17, 15 },
        };
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                Assert.AreEqual(expected[r, c], matrix![r, c], $"[{r},{c}]");
            }
        }
    }

    [TestMethod]
    public void KeyFromKeyword_IgnoresNonLetters()
    {
        var matrix = _cipher.KeyFromKeyword("dd-cf", 2);
        Assert.IsNotNull(matrix);
        Assert.AreEqual(3, matrix![0, 0]); // d
        Assert.AreEqual(3, matrix[0, 1]);  // d
        Assert.AreEqual(2, matrix[1, 0]);  // c
        Assert.AreEqual(5, matrix[1, 1]);  // f
    }

    [TestMethod]
    public void KeyFromKeyword_TooShort_ReturnsNull()
        => Assert.IsNull(_cipher.KeyFromKeyword("ABC", 3));

    [TestMethod]
    public void KeyFromKeyword_DerivedKey_EncryptsLikeRawMatrix()
    {
        var matrix = _cipher.KeyFromKeyword("GYBNQKURP", 3)!;
        Assert.AreEqual("POH", _cipher.Encrypt("ACT", matrix).Text);
    }

    // ---- Empty / no-letter input ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("12345")]
    [DataRow("---")]
    public void Encrypt_NoLetters_ReturnsNoLetters(string? text)
        => Assert.AreEqual(HillStatus.NoLetters, _cipher.Encrypt(text, Key2).Status);

    // ---- Invalid key shapes ----

    [TestMethod]
    public void Encrypt_NonSquareKey_ReturnsInvalidKey()
        => Assert.AreEqual(HillStatus.InvalidKey, _cipher.Encrypt("AB", new int[2, 3]).Status);

    // ---- Known-plaintext key recovery (beyond parity) ----

    [TestMethod]
    public void RecoverKey_From2x2Crib_RecoversMatrixThatDecrypts()
    {
        const string plain = "HELP"; // two 2-letter blocks, invertible block matrix
        var cipher = _cipher.Encrypt(plain, Key2);

        var recovered = _cipher.RecoverKey(plain, cipher.Text, 2);
        Assert.IsNotNull(recovered);

        // The recovered key reproduces the same ciphertext as the original.
        Assert.AreEqual(cipher.Text, _cipher.Encrypt(plain, recovered!).Text);
    }

    [TestMethod]
    public void RecoverKey_From3x3Crib_RecoversWorkingKey()
    {
        // Three 3-letter blocks whose block matrix is invertible mod 26.
        const string plain = "CATDOGFLY";
        var cipher = _cipher.Encrypt(plain, WikiKey3);

        var recovered = _cipher.RecoverKey(plain, cipher.Text, 3);
        Assert.IsNotNull(recovered);
        Assert.AreEqual(cipher.Text, _cipher.Encrypt(plain, recovered!).Text);
    }

    [TestMethod]
    public void RecoverKey_InsufficientMaterial_ReturnsNull()
        => Assert.IsNull(_cipher.RecoverKey("AC", "PO", 3));
}
