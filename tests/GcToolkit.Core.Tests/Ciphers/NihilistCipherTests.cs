using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class NihilistCipherTests
{
    private readonly NihilistCipher _cipher = new();

    // ---- Polybius square construction ----

    [TestMethod]
    public void BuildSquare_ZebrasKeyword_DedupesThenAppendsRemainingAlphabet()
        => Assert.AreEqual("ZEBRASCDFGHIKLMNOPQTUVWXY", _cipher.BuildSquare("ZEBRAS"));

    [TestMethod]
    public void BuildSquare_NoKeyword_IsTheStraightAlphabet()
        => Assert.AreEqual(NihilistCipher.Default5x5Alphabet, _cipher.BuildSquare(""));

    [TestMethod]
    public void BuildSquare_JulesVerne_FoldsJToIAndDedupesExpectedSquare()
        => Assert.AreEqual("IULESVRNABCDFGHKMOPQTWXYZ", _cipher.BuildSquare("JULESVERNE"));

    [TestMethod]
    public void BuildSquare_6x6_IncludesDigitsAndKeepsJ()
        => Assert.AreEqual("ZEBRAS" + "CDFGHIJKLMNOPQTUVWXY" + "0123456789",
                           _cipher.BuildSquare("ZEBRAS", new NihilistOptions(Size: 6)));

    [TestMethod]
    public void BuildSquare_KeywordCharNotInAlphabet_Throws()
        => Assert.ThrowsExactly<NihilistCipherException>(() => _cipher.BuildSquare("ZEBR4S"));

    [TestMethod]
    public void SquareRows_5x5_ReturnsFiveRowsOfFive()
    {
        var rows = _cipher.SquareRows("ZEBRAS");
        Assert.AreEqual(5, rows.Count);
        Assert.AreEqual("ZEBRA", rows[0]);
        Assert.AreEqual("SCDFG", rows[1]);
        Assert.AreEqual("UVWXY", rows[4]);
    }

    [TestMethod]
    public void AxisLabels_Base1_AreOneThroughSize()
        => CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, _cipher.AxisLabels().ToArray());

    [TestMethod]
    public void AxisLabels_Base0_AreZeroThroughSizeMinusOne()
        => CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, _cipher.AxisLabels(new NihilistOptions(ZeroBased: true)).ToArray());

    // ---- Canonical Wikipedia vector ----

    [TestMethod]
    public void Encrypt_WikipediaVector_ProducesExactCipherStream()
    {
        var result = _cipher.Encrypt("DYNAMITEWINTERPALACE", "ZEBRAS", "RUSSIAN");

        Assert.AreEqual(
            "37 106 62 36 67 47 86 26 104 53 62 77 27 55 57 66 55 36 54 27",
            result.Text);
    }

    [TestMethod]
    public void Decrypt_WikipediaVector_RecoversPlaintext()
    {
        var result = _cipher.Decrypt(
            "37 106 62 36 67 47 86 26 104 53 62 77 27 55 57 66 55 36 54 27",
            "ZEBRAS",
            "RUSSIAN");

        Assert.AreEqual("DYNAMITEWINTERPALACE", result.Text);
    }

    [TestMethod]
    public void Encrypt_WikipediaVector_BreakdownStartsWithCorrectStep()
    {
        var result = _cipher.Encrypt("DYNAMITEWINTERPALACE", "ZEBRAS", "RUSSIAN");

        var first = result.Steps[0];
        Assert.AreEqual('D', first.Symbol);
        Assert.AreEqual(23, first.PlainValue);  // D at row2 col3
        Assert.AreEqual(14, first.KeyValue);    // R at row1 col4
        Assert.AreEqual(37, first.CipherValue);
        Assert.AreEqual(20, result.Steps.Count);
    }

    // ---- Round trips ----

    [DataTestMethod]
    [DataRow("HELLOWORLD", "SECRET", "KEY")]
    [DataRow("ATTACKATDAWN", "POLYBIUS", "NIHILIST")]
    [DataRow("THEQUICKBROWNFOX", "", "ALPHA")]
    [DataRow("X", "Z", "Z")]
    public void RoundTrip_EncryptThenDecrypt_RecoversPlaintext(string plain, string poly, string add)
    {
        var cipher = _cipher.Encrypt(plain, poly, add).Text;
        var recovered = _cipher.Decrypt(cipher, poly, add).Text;
        Assert.AreEqual(plain, recovered);
    }

    [TestMethod]
    public void RoundTrip_JFoldsToI_SoDecryptYieldsI()
    {
        var cipher = _cipher.Encrypt("JINX", "ZEBRAS", "KEY").Text;
        Assert.AreEqual("IINX", _cipher.Decrypt(cipher, "ZEBRAS", "KEY").Text);
    }

    // ---- 6×6 with digits ----

    [TestMethod]
    public void Encrypt_6x6_EncodesDigitsAndRoundTrips()
    {
        NihilistOptions options = new(Size: 6);
        const string plain = "N49E8M2";
        var cipher = _cipher.Encrypt(plain, "GEOCACHE", "WAYPOINT", options).Text;
        var recovered = _cipher.Decrypt(cipher, "GEOCACHE", "WAYPOINT", options).Text;
        Assert.AreEqual(plain, recovered);
    }

    [TestMethod]
    public void Encrypt_6x6_KnownDigitCoordinate()
    {
        // 6×6 square "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", '0' is index 26 -> row4 col2 -> 1-based 53.
        // Additive 'A' (index 0) -> 11. So "0" with key "A" => 53 + 11 = 64.
        var result = _cipher.Encrypt("0", "", "A", new NihilistOptions(Size: 6));
        Assert.AreEqual("64", result.Text);
    }

    // ---- Orientation toggle ----

    [TestMethod]
    public void Encrypt_ColumnRowOrientation_SwapsCoordinateOrder()
    {
        // D in ZEBRAS square sits at row2 col3. Row-col => 23, col-row => 32.
        var rowCol = _cipher.Encrypt("D", "ZEBRAS", "A").Steps[0].PlainValue;
        var colRow = _cipher.Encrypt("D", "ZEBRAS", "A", new NihilistOptions(Orientation: NihilistOrientation.ColumnRow)).Steps[0].PlainValue;
        Assert.AreEqual(23, rowCol);
        Assert.AreEqual(32, colRow);
    }

    [TestMethod]
    public void RoundTrip_ColumnRowOrientation_Recovers()
    {
        NihilistOptions options = new(Orientation: NihilistOrientation.ColumnRow);
        var cipher = _cipher.Encrypt("GEOCACHE", "PUZZLE", "FIELD", options).Text;
        Assert.AreEqual("GEOCACHE", _cipher.Decrypt(cipher, "PUZZLE", "FIELD", options).Text);
    }

    // ---- Base toggle ----

    [TestMethod]
    public void Encrypt_Base0_StartsCoordinatesAtZero()
    {
        // Z is index 0 in the ZEBRAS square; base-0 => row0 col0 => coordinate 0.
        var result = _cipher.Encrypt("Z", "ZEBRAS", "Z", new NihilistOptions(ZeroBased: true));
        Assert.AreEqual(0, result.Steps[0].PlainValue);
        Assert.AreEqual("0", result.Text);
    }

    [TestMethod]
    public void RoundTrip_Base0_Recovers()
    {
        NihilistOptions options = new(ZeroBased: true);
        var cipher = _cipher.Encrypt("CACHE", "KEYWORD", "ADD", options).Text;
        Assert.AreEqual("CACHE", _cipher.Decrypt(cipher, "KEYWORD", "ADD", options).Text);
    }

    // ---- Custom alphabet ----

    [TestMethod]
    public void Encrypt_CustomAlphabet_UsesProvidedOrdering()
    {
        // Reverse alphabet (still 25 distinct, I/J-style omission of J not needed since we supply 25).
        NihilistOptions options = new(Alphabet: "ZYXWVUTSRQPONMLKIHGFEDCBA");
        var cipher = _cipher.Encrypt("ABC", "", "A", options).Text;
        Assert.AreEqual("ABC", _cipher.Decrypt(cipher, "", "A", options).Text);
    }

    [TestMethod]
    public void BuildSquare_CustomAlphabetWrongLength_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() => _cipher.BuildSquare("", new NihilistOptions(Alphabet: "ABC")));

    // ---- Whitespace / separators ----

    [TestMethod]
    public void Encrypt_SkipsSpacesAndPunctuation()
    {
        var spaced = _cipher.Encrypt("DYN AMI TE", "ZEBRAS", "RUSSIAN").Text;
        var solid = _cipher.Encrypt("DYNAMITE", "ZEBRAS", "RUSSIAN").Text;
        Assert.AreEqual(solid, spaced);
    }

    [TestMethod]
    public void Decrypt_AcceptsCommaAndNewlineSeparators()
    {
        var result = _cipher.Decrypt("37,106\n62", "ZEBRAS", "RUSSIAN");
        Assert.AreEqual("DYN", result.Text);
    }

    [TestMethod]
    public void KeyValues_RepeatsAcrossLongerPlaintext()
    {
        // RUSSIAN has 7 letters; the 8th plaintext symbol reuses the 1st key value.
        var steps = _cipher.Encrypt("AAAAAAAA", "ZEBRAS", "RUSSIAN").Steps;
        Assert.AreEqual(steps[0].KeyValue, steps[7].KeyValue);
    }

    // ---- Validation / error paths ----

    [TestMethod]
    public void Encrypt_EmptyAdditiveKeyword_Throws()
        => Assert.ThrowsExactly<NihilistCipherException>(() => _cipher.Encrypt("ABC", "ZEBRAS", ""));

    [TestMethod]
    public void Encrypt_WhitespaceAdditiveKeyword_Throws()
        => Assert.ThrowsExactly<NihilistCipherException>(() => _cipher.Encrypt("ABC", "ZEBRAS", "   "));

    [TestMethod]
    public void Encrypt_PlaintextCharNotInSquare_Throws()
        => Assert.ThrowsExactly<NihilistCipherException>(() => _cipher.Encrypt("ABC1", "ZEBRAS", "KEY"));

    [TestMethod]
    public void Decrypt_NonNumericToken_Throws()
        => Assert.ThrowsExactly<NihilistCipherException>(() => _cipher.Decrypt("37 XX 62", "ZEBRAS", "RUSSIAN"));

    [TestMethod]
    public void Decrypt_OutOfRangeCipherValue_Throws()
    {
        // A wildly large value cannot land inside a 5×5 square after key subtraction.
        Assert.ThrowsExactly<NihilistCipherException>(() => _cipher.Decrypt("999", "ZEBRAS", "RUSSIAN"));
    }

    [TestMethod]
    public void Decrypt_KeySubtractionUnderflow_Throws()
        => Assert.ThrowsExactly<NihilistCipherException>(() => _cipher.Decrypt("1", "ZEBRAS", "RUSSIAN"));

    [TestMethod]
    public void Encrypt_EmptyPlaintext_ProducesEmptyOutputButValidatesKey()
    {
        var result = _cipher.Encrypt("", "ZEBRAS", "KEY");
        Assert.AreEqual(string.Empty, result.Text);
        Assert.AreEqual(0, result.Steps.Count);
    }

    [TestMethod]
    public void KeyValues_PublicHelper_ReturnsRepeatingSequence()
    {
        var values = _cipher.KeyValues("RUSSIAN", "ZEBRAS");
        Assert.AreEqual(7, values.Count);
        Assert.AreEqual(14, values[0]); // R -> row1 col4
    }
}
