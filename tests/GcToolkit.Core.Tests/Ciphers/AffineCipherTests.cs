using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class AffineCipherTests
{
    private readonly AffineCipher _cipher = new();

    // ---- Known vector: E(x) = (5x + 8) mod 26 ----

    [TestMethod]
    public void Transform_KnownVector_EncodesAffineExample()
    {
        // Classic textbook example: E(x)=(5x+8) mod 26 on "AFFINECIPHER" → "IHHWVCSWFRCP".
        Assert.AreEqual("IHHWVCSWFRCP", _cipher.Transform("AFFINECIPHER", 5, 8, decrypt: false));
    }

    [TestMethod]
    public void Transform_KnownVector_DecodesBackToPlaintext()
    {
        var encoded = _cipher.Transform("AFFINECIPHER", 5, 8, decrypt: false);
        Assert.AreEqual("AFFINECIPHER", _cipher.Transform(encoded, 5, 8, decrypt: true));
    }

    // ---- Round trip for every valid multiplier and several offsets ----

    [TestMethod]
    public void Transform_RoundTrips_ForEveryValidMultiplierAndOffset()
    {
        const string plain = "The Quick Brown Fox 123!";
        foreach (var a in AffineCipher.ValidMultipliers)
        {
            foreach (var b in new[] { 0, 1, 8, 13, 25, 26 })
            {
                var encoded = _cipher.Transform(plain, a, b, decrypt: false);
                Assert.AreEqual(plain, _cipher.Transform(encoded, a, b, decrypt: true), $"a={a}, b={b}");
            }
        }
    }

    // ---- Modular inverse correctness ----

    [TestMethod]
    public void ModInverse_ForEveryValidMultiplier_SatisfiesProduct()
    {
        foreach (var a in AffineCipher.ValidMultipliers)
        {
            var inverse = _cipher.ModInverse(a);
            Assert.AreEqual(1, a * inverse % 26, $"a={a}, aInv={inverse}");
        }
    }

    [DataTestMethod]
    [DataRow(1, 1)]
    [DataRow(3, 9)]
    [DataRow(5, 21)]
    [DataRow(7, 15)]
    [DataRow(25, 25)]
    public void ModInverse_KnownPairs_ReturnsExpected(int a, int expectedInverse)
        => Assert.AreEqual(expectedInverse, _cipher.ModInverse(a));

    // ---- Coprime detection ----

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(25)]
    public void IsCoprime_CoprimeMultiplier_ReturnsTrue(int a)
        => Assert.IsTrue(_cipher.IsCoprime(a));

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(13)]
    [DataRow(26)]
    [DataRow(4)]
    public void IsCoprime_NonCoprimeMultiplier_ReturnsFalse(int a)
        => Assert.IsFalse(_cipher.IsCoprime(a));

    [TestMethod]
    public void ValidMultipliers_AreTheTwelveCoprimeValues()
        => CollectionAssert.AreEqual(
            new[] { 1, 3, 5, 7, 9, 11, 15, 17, 19, 21, 23, 25 },
            AffineCipher.ValidMultipliers.ToArray());

    // ---- Case preserved / non-letters pass through ----

    [TestMethod]
    public void Transform_PreservesCase()
    {
        var encoded = _cipher.Transform("Hello", 5, 8, decrypt: false);
        // First letter upper, rest lower → cipher keeps that casing.
        Assert.AreEqual(encoded[0], char.ToUpperInvariant(encoded[0]));
        Assert.IsTrue(char.IsLower(encoded[1]));
    }

    [TestMethod]
    public void Transform_PassesNonLettersThrough()
    {
        var result = _cipher.Transform("N 49 13.456!", 5, 8, decrypt: false);
        // Spaces, digits and punctuation untouched; only the letter is transformed.
        StringAssert.Contains(result, " 49 13.456!");
        Assert.AreNotEqual('N', result[0]);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Transform_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Transform(text, 5, 8, decrypt: false));

    // ---- Substitution alphabet ----

    [TestMethod]
    public void SubstitutionAlphabet_Identity_ReturnsPlainAlphabet()
        => Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", _cipher.SubstitutionAlphabet(1, 0));

    [TestMethod]
    public void SubstitutionAlphabet_KnownKey_MapsAlphabet()
    {
        // a=5, b=8 maps A→I, B→N, C→S, ...
        var alphabet = _cipher.SubstitutionAlphabet(5, 8);
        Assert.AreEqual(26, alphabet.Length);
        Assert.AreEqual('I', alphabet[0]);
        Assert.AreEqual('N', alphabet[1]);
    }

    // ---- Brute force ----

    [TestMethod]
    public void BruteForce_Produces324Candidates()
        => Assert.AreEqual(12 * 27, _cipher.BruteForce("ANYTHING").Count);

    [TestMethod]
    public void BruteForce_ContainsTheTruePlaintext()
    {
        var encoded = _cipher.Transform("GEOCACHE", 11, 19, decrypt: false);
        var candidates = _cipher.BruteForce(encoded);
        Assert.IsTrue(candidates.Any(c => c.Text == "GEOCACHE"));
        Assert.IsTrue(candidates.Any(c => c is { A: 11, B: 19 } && c.Text == "GEOCACHE"));
    }
}
