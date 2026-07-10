using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class TrithemiusCipherTests
{
    private readonly TrithemiusCipher _cipher = new();

    // ---- Core parity: keyless +1 progressive shift ----

    [TestMethod]
    public void Transform_Encrypt_AAAA_ProducesABCD()
        => Assert.AreEqual("ABCD", _cipher.Transform("AAAA", decrypt: false));

    [TestMethod]
    public void Transform_Decrypt_ABCD_ProducesAAAA()
        => Assert.AreEqual("AAAA", _cipher.Transform("ABCD", decrypt: true));

    [TestMethod]
    public void Transform_Encrypt_WrapsPastZ()
        => Assert.AreEqual("YYY", _cipher.Transform("YXW", decrypt: false));

    [TestMethod]
    public void Transform_Encrypt_LongText_ShiftsByLetterPosition()
        // H+0 E+1 L+2 L+3 O+4 -> H F N O S
        => Assert.AreEqual("HFNOS", _cipher.Transform("HELLO", decrypt: false));

    [TestMethod]
    public void Transform_RoundTrips_ForLongerText()
    {
        const string plain = "The Quick Brown Fox Jumps Over The Lazy Dog";
        var encrypted = _cipher.Transform(plain, decrypt: false);
        Assert.AreNotEqual(plain, encrypted);
        Assert.AreEqual(plain, _cipher.Transform(encrypted, decrypt: true));
    }

    [TestMethod]
    public void Transform_Encrypt_PreservesCase()
    {
        // lower-case 'a' at positions 0..3 mirrors the AAAA -> ABCD progression in lower case.
        Assert.AreEqual("abcd", _cipher.Transform("aaaa", decrypt: false));
        Assert.AreEqual("AbCd", _cipher.Transform("AaAa", decrypt: false));
    }

    // ---- Non-letters pass through and do NOT advance the shift ----

    [TestMethod]
    public void Transform_Encrypt_NonLettersPassThroughWithoutAdvancing()
        // Spaces/digits keep the running letter index: A A A A still becomes A B C D.
        => Assert.AreEqual("A B1C D", _cipher.Transform("A A1A A", decrypt: false));

    [TestMethod]
    public void Transform_Encrypt_NonLetterDoesNotConsumeAShiftStep()
    {
        // "AA" without separators encrypts to "AB"; inserting non-letters between must not change A/B.
        Assert.AreEqual("A...B", _cipher.Transform("A...A", decrypt: false));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Transform_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Transform(text, decrypt: false));

    // ---- startOffset / step variant (beyond parity) ----

    [TestMethod]
    public void Transform_Encrypt_WithStartOffset_ShiftsAllRows()
        // startOffset 3: A+3 A+4 A+5 A+6 -> D E F G
        => Assert.AreEqual("DEFG", _cipher.Transform("AAAA", decrypt: false, startOffset: 3));

    [TestMethod]
    public void Transform_Encrypt_WithStep2_DoublesProgression()
        // step 2: A+0 A+2 A+4 A+6 -> A C E G
        => Assert.AreEqual("ACEG", _cipher.Transform("AAAA", decrypt: false, step: 2));

    [TestMethod]
    public void Transform_RoundTrips_WithCustomOffsetAndStep()
    {
        const string plain = "GEOCACHE";
        var encrypted = _cipher.Transform(plain, decrypt: false, startOffset: 7, step: 3);
        Assert.AreEqual(plain, _cipher.Transform(encrypted, decrypt: true, startOffset: 7, step: 3));
    }

    // ---- AllOffsets: 26 decode candidates for an unknown start ----

    [TestMethod]
    public void AllOffsets_Produces26Candidates()
        => Assert.AreEqual(26, _cipher.AllOffsets("ABCD").Count);

    [TestMethod]
    public void AllOffsets_FirstCandidate_IsOffsetZeroDecode()
    {
        var candidates = _cipher.AllOffsets("ABCD");
        Assert.AreEqual(0, candidates[0].StartOffset);
        Assert.AreEqual("AAAA", candidates[0].Text);
    }

    [TestMethod]
    public void AllOffsets_ContainsTheTruePlaintext()
    {
        // Encrypt with an unknown start offset, then brute-force every starting row.
        var encrypted = _cipher.Transform("GEOCACHE", decrypt: false, startOffset: 11);
        var candidates = _cipher.AllOffsets(encrypted);
        Assert.IsTrue(candidates.Any(c => c.Text == "GEOCACHE"));
        Assert.AreEqual("GEOCACHE", candidates.Single(c => c.StartOffset == 11).Text);
    }

    // ---- Tabula recta ----

    [TestMethod]
    public void TabulaRecta_Has26RowsOf26Letters()
    {
        var table = _cipher.TabulaRecta();
        Assert.AreEqual(26, table.Count);
        Assert.IsTrue(table.All(row => row.Length == 26));
    }

    [TestMethod]
    public void TabulaRecta_RowsAreSuccessiveRotations()
    {
        var table = _cipher.TabulaRecta();
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", table[0]);
        Assert.AreEqual("BCDEFGHIJKLMNOPQRSTUVWXYZA", table[1]);
        Assert.AreEqual("ZABCDEFGHIJKLMNOPQRSTUVWXY", table[25]);
    }
}
