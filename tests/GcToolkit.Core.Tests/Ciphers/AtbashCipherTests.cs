using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class AtbashCipherTests
{
    private readonly AtbashCipher _cipher = new();

    // ---- Core mapping: A<->Z, B<->Y, … (0-based i -> 25-i) ----

    [DataTestMethod]
    [DataRow("ABC", "ZYX")]
    [DataRow("XYZ", "CBA")]
    [DataRow("A", "Z")]
    [DataRow("Z", "A")]
    [DataRow("M", "N")]   // the middle pair
    [DataRow("N", "M")]
    [DataRow("THE QUICK BROWN FOX", "GSV JFRXP YILDM ULC")]
    public void Transform_UpperCase_MirrorsAlphabet(string text, string expected)
        => Assert.AreEqual(expected, _cipher.Transform(text));

    [TestMethod]
    public void Transform_PreservesLetterCase()
        => Assert.AreEqual("Svool, Dliow!", _cipher.Transform("Hello, World!"));

    [TestMethod]
    public void Transform_LowerCase_MirrorsAlphabet()
        => Assert.AreEqual("zyx", _cipher.Transform("abc"));

    // ---- Non-letters pass through unchanged ----

    [DataTestMethod]
    [DataRow("123", "123")]
    [DataRow("N 49 13.456", "M 49 13.456")] // digits, dot, space untouched; letters mirrored
    [DataRow("!@#$%^&*()", "!@#$%^&*()")]
    [DataRow("a-b_c", "z-y_x")]
    public void Transform_PassesNonLettersThrough(string text, string expected)
        => Assert.AreEqual(expected, _cipher.Transform(text));

    [TestMethod]
    public void Transform_PassesUnicodeThrough()
    {
        // Non-Latin letters and emoji are outside A–Z/a–z, so they pass through unchanged while the
        // ASCII letters around them are still mirrored. Build the expectation from the rule itself
        // so the assertion can't drift on a hand-typed literal.
        const string text = "Příliš žluťoučký 🦊 café";
        var expected = string.Concat(text.Select(static c => c switch
        {
            >= 'A' and <= 'Z' => (char)('Z' - (c - 'A')),
            >= 'a' and <= 'z' => (char)('z' - (c - 'a')),
            _ => c,
        }));

        Assert.AreEqual(expected, _cipher.Transform(text));
        // Accented/emoji code points survive verbatim (🦊 is a surrogate pair → use the string overload).
        Assert.IsTrue(_cipher.Transform(text).Contains("🦊", StringComparison.Ordinal));
        Assert.IsTrue(_cipher.Transform(text).Contains('ř'));
    }

    // ---- Empty / null ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Transform_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Transform(text));

    // ---- Self-inverse: Transform(Transform(x)) == x ----

    [DataTestMethod]
    [DataRow("Hello, World!")]
    [DataRow("The quick brown fox jumps over the lazy dog 1234567890")]
    [DataRow("N 49° 13.456 E 008° 24.123")]
    [DataRow("Příliš žluťoučký kůň")]
    public void Transform_AppliedTwice_RoundTrips(string text)
        => Assert.AreEqual(text, _cipher.Transform(_cipher.Transform(text)));

    // ---- Substitution key for the "key" display ----

    [TestMethod]
    public void PlainAlphabet_ReturnsAtoZ()
        => Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", AtbashCipher.PlainAlphabet);

    [TestMethod]
    public void CipherAlphabet_ReturnsZtoA()
        => Assert.AreEqual("ZYXWVUTSRQPONMLKJIHGFEDCBA", AtbashCipher.CipherAlphabet);
}
