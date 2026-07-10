using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class ScytaleCipherTests
{
    private readonly ScytaleCipher _cipher = new();

    // ---- Worked example: write across columns, read down them ----

    [TestMethod]
    public void Encrypt_KnownExample_ReadsDownColumns()
    {
        // "HELPME" laid out across 3 columns row-by-row:
        //   H E L
        //   P M E
        // read down columns -> HP EM LE
        Assert.AreEqual("HPEMLE", _cipher.Encrypt("HELPME", 3, ignoreSpaces: false));
    }

    [TestMethod]
    public void Encrypt_LongerExample_ReadsDownColumns()
    {
        // "WEAREDISCOVEREDFLEEATONCE" (25 chars) across 5 columns:
        //   W E A R E
        //   D I S C O
        //   V E R E D
        //   F L E E A
        //   T O N C E
        // down columns -> WDVFT EIELO ASREN RCEEC EODAE
        Assert.AreEqual("WDVFTEIELOASRENRCEECEODAE", _cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", 5, ignoreSpaces: false));
    }

    [TestMethod]
    public void Decrypt_IsInverseOfEncrypt_KnownExample()
        => Assert.AreEqual("HELPME", _cipher.Decrypt("HPEMLE", 3, ignoreSpaces: false));

    // ---- Round trips across several column counts ----

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(7)]
    [DataRow(11)]
    public void Decrypt_OfEncrypt_RoundTrips(int columns)
    {
        const string plain = "THEQUICKBROWNFOXJUMPS";
        var encoded = _cipher.Encrypt(plain, columns, ignoreSpaces: false);
        Assert.AreEqual(plain, _cipher.Decrypt(encoded, columns, ignoreSpaces: false));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(6)]
    public void Decrypt_OfEncrypt_RoundTripsWithSpaces(int columns)
    {
        const string plain = "ATTACK AT DAWN";
        var encoded = _cipher.Encrypt(plain, columns, ignoreSpaces: false);
        Assert.AreEqual(plain, _cipher.Decrypt(encoded, columns, ignoreSpaces: false));
    }

    // ---- Ignore spaces ----

    [TestMethod]
    public void Encrypt_IgnoreSpaces_ExcludesSpacesFromTransposition()
    {
        // "AB CD" with spaces ignored transposes only "ABCD" across 2 columns:
        //   A B
        //   C D
        // down columns -> AC BD
        Assert.AreEqual("ACBD", _cipher.Encrypt("AB CD", 2, ignoreSpaces: true));
    }

    [TestMethod]
    public void Decrypt_IgnoreSpaces_RoundTripsWithoutSpaces()
    {
        const string plain = "ATTACK AT DAWN";
        var encoded = _cipher.Encrypt(plain, 4, ignoreSpaces: true);
        // Spaces are dropped, so the recovered text is the spaceless plaintext.
        Assert.AreEqual("ATTACKATDAWN", _cipher.Decrypt(encoded, 4, ignoreSpaces: true));
    }

    // ---- Pad / extra character ----

    [TestMethod]
    public void Encrypt_WithPadChar_FillsLastIncompleteRow()
    {
        // "HELLO" (5) across 3 columns needs one pad to complete the 2x3 grid:
        //   H E L
        //   L O X
        // down columns -> HL EO LX
        Assert.AreEqual("HLEOLX", _cipher.Encrypt("HELLO", 3, ignoreSpaces: false, padChar: 'X'));
    }

    [TestMethod]
    public void Encrypt_WithoutPadChar_LeavesRaggedGrid()
    {
        // No padding: the last row is short, columns are uneven.
        //   H E L
        //   L O
        // down columns -> HL EO L
        Assert.AreEqual("HLEOL", _cipher.Encrypt("HELLO", 3, ignoreSpaces: false));
    }

    // ---- AutoSolve (beyond parity) ----

    [TestMethod]
    public void AutoSolve_ContainsTheTruePlaintext()
    {
        const string plain = "MEETMEATTHEPARK";
        const int columns = 4;
        var cipher = _cipher.Encrypt(plain, columns, ignoreSpaces: false);

        var candidates = _cipher.AutoSolve(cipher);

        Assert.IsTrue(candidates.Any(c => c.Text == plain), "true plaintext missing from candidates");
        Assert.IsTrue(candidates.Any(c => c.Columns == columns && c.Text == plain), "true column count not labelled");
    }

    [TestMethod]
    public void AutoSolve_ProducesOneCandidatePerPlausibleColumnCount()
    {
        var candidates = _cipher.AutoSolve("ABCDEFGH"); // length 8 -> columns 2..8 = 7 candidates
        Assert.AreEqual(7, candidates.Count);
        Assert.AreEqual(2, candidates[0].Columns);
        Assert.AreEqual(8, candidates[^1].Columns);
    }

    // ---- Validation ----

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    [DataRow(-3)]
    public void Encrypt_ColumnsLessThanTwo_Throws(int columns)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _cipher.Encrypt("ABC", columns, ignoreSpaces: false));

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    public void Decrypt_ColumnsLessThanTwo_Throws(int columns)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _cipher.Decrypt("ABC", columns, ignoreSpaces: false));

    // ---- Empty / null ----

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encrypt_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Encrypt(text, 3, ignoreSpaces: false));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Decrypt_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Decrypt(text, 3, ignoreSpaces: false));
}
