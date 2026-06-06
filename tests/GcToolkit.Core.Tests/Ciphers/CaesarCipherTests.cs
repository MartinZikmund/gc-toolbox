using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class CaesarCipherTests
{
    private readonly CaesarCipher _cipher = new();

    // ---- Letters: classic Caesar / ROT13 ----

    [DataTestMethod]
    [DataRow("ABC", 1, "BCD")]
    [DataRow("XYZ", 3, "ABC")]          // wraps past Z
    [DataRow("Hello", 13, "Uryyb")]     // ROT13, case preserved
    [DataRow("Uryyb", 13, "Hello")]     // ROT13 is self-inverse
    [DataRow("abc", 0, "abc")]          // identity
    [DataRow("abc", 26, "abc")]         // full wrap is identity
    public void Transform_Letters_ShiftsAndPreservesCase(string text, int shift, string expected)
        => Assert.AreEqual(expected, _cipher.Transform(text, shift, CaesarAlphabet.Letters));

    [TestMethod]
    public void Transform_Letters_PassesNonLettersThrough()
        => Assert.AreEqual("Khoor, Zruog! 123", _cipher.Transform("Hello, World! 123", 3, CaesarAlphabet.Letters));

    [TestMethod]
    public void Transform_Letters_NegativeShiftDecodes()
        => Assert.AreEqual("ABC", _cipher.Transform("DEF", -3, CaesarAlphabet.Letters));

    [TestMethod]
    public void Transform_Letters_NegativeShiftEqualsComplement()
        => Assert.AreEqual(_cipher.Transform("Geocache", 23, CaesarAlphabet.Letters),
                           _cipher.Transform("Geocache", -3, CaesarAlphabet.Letters));

    [TestMethod]
    public void Transform_Letters_RoundTripsForEveryShift()
    {
        const string plain = "The Quick Brown Fox";
        for (var shift = 0; shift < CaesarCipher.LetterAlphabetSize; shift++)
        {
            var encoded = _cipher.Transform(plain, shift, CaesarAlphabet.Letters);
            Assert.AreEqual(plain, _cipher.Transform(encoded, -shift, CaesarAlphabet.Letters), $"shift {shift}");
        }
    }

    // ---- Digits: ROT5 ----

    [DataTestMethod]
    [DataRow("12345", 5, "67890")]
    [DataRow("67890", 5, "12345")]      // ROT5 self-inverse
    [DataRow("N 49 13.456", 5, "N 94 68.901")] // letters/space untouched in digit mode
    public void Transform_Digits_RotatesDigitsOnly(string text, int shift, string expected)
        => Assert.AreEqual(expected, _cipher.Transform(text, shift, CaesarAlphabet.Digits));

    // ---- ASCII: ROT47 ----

    [TestMethod]
    public void Transform_Ascii_Rot47IsSelfInverse()
    {
        const string plain = "Geocache #13! (N49)";
        var encoded = _cipher.Transform(plain, 47, CaesarAlphabet.Ascii);
        Assert.AreNotEqual(plain, encoded);
        Assert.AreEqual(plain, _cipher.Transform(encoded, 47, CaesarAlphabet.Ascii));
    }

    [TestMethod]
    public void Transform_Ascii_LeavesSpaceUntouched()
        => Assert.AreEqual(' ', _cipher.Transform("a b", 47, CaesarAlphabet.Ascii)[1]);

    // ---- Empty / null ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Transform_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Transform(text, 5, CaesarAlphabet.Letters));

    // ---- AllShifts ----

    [TestMethod]
    public void AllShifts_Letters_Produces25CandidatesInOrder()
    {
        var results = _cipher.AllShifts("ABC", CaesarAlphabet.Letters);

        Assert.AreEqual(25, results.Count);
        Assert.AreEqual(1, results[0].Shift);
        Assert.AreEqual("BCD", results[0].Text);
        Assert.AreEqual(25, results[24].Shift);
        Assert.AreEqual("ZAB", results[24].Text);
    }

    [TestMethod]
    public void AllShifts_Digits_Produces9Candidates()
        => Assert.AreEqual(9, _cipher.AllShifts("0", CaesarAlphabet.Digits).Count);

    [TestMethod]
    public void AllShifts_Ascii_Produces93Candidates()
        => Assert.AreEqual(93, _cipher.AllShifts("x", CaesarAlphabet.Ascii).Count);

    [TestMethod]
    public void AllShifts_ContainsTheKnownPlaintext()
    {
        // Brute-forcing an unknown shift: the plaintext appears at the complementary shift.
        var encoded = _cipher.Transform("GEOCACHE", 7, CaesarAlphabet.Letters);
        var candidates = _cipher.AllShifts(encoded, CaesarAlphabet.Letters);
        Assert.IsTrue(candidates.Any(c => c.Text == "GEOCACHE"));
    }

    // ---- Alphabets & defaults ----

    [TestMethod]
    public void PlainAlphabet_ReturnsOrderedSymbols()
    {
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", _cipher.PlainAlphabet(CaesarAlphabet.Letters));
        Assert.AreEqual("0123456789", _cipher.PlainAlphabet(CaesarAlphabet.Digits));
        Assert.AreEqual(CaesarCipher.AsciiAlphabetSize, _cipher.PlainAlphabet(CaesarAlphabet.Ascii).Length);
    }

    [TestMethod]
    public void CipherAlphabet_Rot13_MapsAtoN()
    {
        var key = _cipher.CipherAlphabet(13, CaesarAlphabet.Letters);
        Assert.AreEqual("NOPQRSTUVWXYZABCDEFGHIJKLM", key);
    }

    [DataTestMethod]
    [DataRow(CaesarAlphabet.Letters, 26, 13)]
    [DataRow(CaesarAlphabet.Digits, 10, 5)]
    [DataRow(CaesarAlphabet.Ascii, 94, 47)]
    public void AlphabetSizeAndDefaultShift_MatchConvention(CaesarAlphabet alphabet, int size, int defaultShift)
    {
        Assert.AreEqual(size, CaesarCipher.AlphabetSize(alphabet));
        Assert.AreEqual(defaultShift, CaesarCipher.DefaultShift(alphabet));
    }
}
