using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class GronsfeldCipherTests
{
    private readonly GronsfeldCipher _cipher = new();

    // ---- Known vector (computed by hand) ----
    // key "1234" on "GRONSFELD" (key cycles 1,2,3,4,1,2,3,4,1 across the 9 letters):
    // G+1=H, R+2=T, O+3=R, N+4=R, S+1=T, F+2=H, E+3=H, L+4=P, D+1=E => "HTRRTHHPE"

    [TestMethod]
    public void Transform_KnownVector_EncryptsCorrectly()
        => Assert.AreEqual("HTRRTHHPE", _cipher.Transform("GRONSFELD", "1234", decrypt: false));

    [TestMethod]
    public void Transform_KnownVector_DecryptsBackToPlaintext()
        => Assert.AreEqual("GRONSFELD", _cipher.Transform("HTRRTHHPE", "1234", decrypt: true));

    // ---- Single-digit key behaves like a constant Caesar shift ----

    [TestMethod]
    [DataRow("ABC", "1", "BCD")]
    [DataRow("XYZ", "3", "ABC")]   // wraps past Z
    [DataRow("abc", "0", "abc")]   // 0 is the identity shift
    [DataRow("AJ", "9", "JS")]     // 0=>a..9=>j reference: A+9=J, J+9=S
    public void Transform_SingleDigitKey_ShiftsLikeCaesar(string text, string key, string expected)
        => Assert.AreEqual(expected, _cipher.Transform(text, key, decrypt: false));

    // ---- Round trip ----

    [TestMethod]
    public void Transform_RoundTrip_RecoversOriginal()
    {
        const string plain = "The Quick Brown Fox Jumps Over The Lazy Dog";
        const string key = "31415926";
        var encoded = _cipher.Transform(plain, key, decrypt: false);
        Assert.AreEqual(plain, _cipher.Transform(encoded, key, decrypt: true));
    }

    // ---- Key cycling across long text ----

    [TestMethod]
    public void Transform_KeyCycles_RepeatsAcrossLetters()
    {
        // key "12" repeats 1,2,1,2,1,2 over six letters: A+1,A+2,A+1,A+2,A+1,A+2
        Assert.AreEqual("BCBCBC", _cipher.Transform("AAAAAA", "12", decrypt: false));
    }

    // ---- Non-letters pass through WITHOUT advancing the key ----

    [TestMethod]
    public void Transform_NonLetters_PassThroughWithoutConsumingKey()
    {
        // key "12" — only the four letters consume key positions (1,2,1,2); spaces/digits/punctuation
        // are emitted verbatim and do not advance the key.
        // A+1=B, B+2=D, (space) (1) (space) C+1=D, D+2=F => "BD 1 DF"
        Assert.AreEqual("BD 1 DF", _cipher.Transform("AB 1 CD", "12", decrypt: false));
    }

    [TestMethod]
    public void Transform_NonLettersOnly_PassThroughUnchanged()
        => Assert.AreEqual("49 13.456!", _cipher.Transform("49 13.456!", "7", decrypt: false));

    // ---- Case preserved ----

    [TestMethod]
    public void Transform_MixedCase_PreservesCase()
        => Assert.AreEqual("Ifmmp", _cipher.Transform("Hello", "1", decrypt: false));

    [TestMethod]
    public void Transform_LowerCaseWraps_PreservesCase()
        => Assert.AreEqual("abc", _cipher.Transform("xyz", "3", decrypt: false));

    // ---- Empty / null input ----

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Transform_EmptyOrNullInput_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Transform(text, "123", decrypt: false));

    // ---- Key validation ----

    [TestMethod]
    [DataRow("0")]
    [DataRow("9")]
    [DataRow("31415926")]
    public void IsValidKey_DigitsOnly_ReturnsTrue(string key)
        => Assert.IsTrue(GronsfeldCipher.IsValidKey(key));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("12a")]
    [DataRow("1 2")]
    [DataRow("-1")]
    [DataRow("１２")] // full-width digits are not ASCII 0-9
    public void IsValidKey_EmptyOrNonDigit_ReturnsFalse(string? key)
        => Assert.IsFalse(GronsfeldCipher.IsValidKey(key));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("12a")]
    public void Transform_InvalidKey_Throws(string? key)
        => Assert.ThrowsExactly<ArgumentException>(() => _cipher.Transform("ABC", key, decrypt: false));

    // ---- Digit -> letter reference (0=>a .. 9=>j) ----

    [TestMethod]
    public void DigitReference_MapsDigitsToLetters()
    {
        var reference = GronsfeldCipher.DigitReference;
        Assert.AreEqual(10, reference.Count);
        Assert.AreEqual((0, 'A'), (reference[0].Digit, reference[0].Letter));
        Assert.AreEqual((9, 'J'), (reference[9].Digit, reference[9].Letter));
    }
}
