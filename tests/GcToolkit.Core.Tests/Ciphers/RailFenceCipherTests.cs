using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class RailFenceCipherTests
{
    private readonly RailFenceCipher _cipher = new();

    // ---- Canonical vector ----

    [TestMethod]
    public void Encrypt_CanonicalVector_MatchesWikipedia()
        => Assert.AreEqual(
            "WECRLTEERDSOEEFEAOCAIVDEN",
            _cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", 3, 0));

    [TestMethod]
    public void Decrypt_CanonicalVector_RecoversPlaintext()
        => Assert.AreEqual(
            "WEAREDISCOVEREDFLEEATONCE",
            _cipher.Decrypt("WECRLTEERDSOEEFEAOCAIVDEN", 3, 0));

    [TestMethod]
    public void Encrypt_TwoRails_AlternatesRows()
        // Even-index chars on rail 0, odd-index chars on rail 1.
        => Assert.AreEqual(
            "WAEICVRDLETNEERDSOEEFEAOC",
            _cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", 2, 0));

    // ---- Round-trip across rails and offsets ----

    [DataTestMethod]
    [DataRow(2, 0)]
    [DataRow(3, 0)]
    [DataRow(4, 0)]
    [DataRow(5, 0)]
    [DataRow(2, 1)]
    [DataRow(3, 2)]
    [DataRow(4, 3)]
    [DataRow(5, 5)]
    [DataRow(7, 9)]
    public void RoundTrip_VariousRailsAndOffsets_RecoversPlaintext(int rails, int offset)
    {
        const string plain = "WEAREDISCOVEREDFLEEATONCE";
        var encrypted = _cipher.Encrypt(plain, rails, offset);
        Assert.AreEqual(plain, _cipher.Decrypt(encrypted, rails, offset), $"rails={rails}, offset={offset}");
    }

    // ---- Full charset preserved (pure transposition) ----

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void RoundTrip_Coordinate_PreservesCaseSpacesAndPunctuation(int rails)
    {
        const string coord = "N50 12.345 E014 23.456";
        var encrypted = _cipher.Encrypt(coord, rails, 0);
        Assert.AreEqual(coord, _cipher.Decrypt(encrypted, rails, 0));
    }

    [TestMethod]
    public void Encrypt_PreservesEveryCharacter_NothingDropped()
    {
        const string text = "Mix3d C@se! (test)";
        var encrypted = _cipher.Encrypt(text, 4, 0);
        Assert.AreEqual(text.Length, encrypted.Length);
        // A transposition is a permutation: sorted characters are unchanged.
        CollectionAssert.AreEquivalent(text.ToCharArray(), encrypted.ToCharArray());
    }

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(5)]
    public void RoundTrip_LowercaseAndMixedCase_CasePreserved(int rails)
    {
        const string text = "Hello World";
        Assert.AreEqual(text, _cipher.Decrypt(_cipher.Encrypt(text, rails, 0), rails, 0));
    }

    // ---- Edge cases ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Encrypt_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Encrypt(text, 3, 0));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Decrypt_EmptyOrNull_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Decrypt(text, 3, 0));

    [TestMethod]
    public void Encrypt_SingleCharacter_ReturnsItself()
        => Assert.AreEqual("A", _cipher.Encrypt("A", 3, 0));

    [TestMethod]
    public void RoundTrip_RailsEqualLength_RecoversPlaintext()
    {
        const string text = "GEOCACHE";
        Assert.AreEqual(text, _cipher.Decrypt(_cipher.Encrypt(text, text.Length, 0), text.Length, 0));
    }

    [TestMethod]
    public void RoundTrip_RailsGreaterThanLength_RecoversPlaintext()
    {
        const string text = "GEOCACHE";
        Assert.AreEqual(text, _cipher.Decrypt(_cipher.Encrypt(text, text.Length + 5, 0), text.Length + 5, 0));
    }

    [TestMethod]
    public void Encrypt_RailsLessThanTwo_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _cipher.Encrypt("ABC", 1, 0));

    [TestMethod]
    public void Encrypt_NegativeOffset_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _cipher.Encrypt("ABC", 3, -1));

    // ---- Offset behavior is self-consistent ----

    [TestMethod]
    public void Encrypt_OffsetByFullCycle_EqualsOffsetZero()
    {
        const string text = "WEAREDISCOVEREDFLEEATONCE";
        const int rails = 4;
        var cycle = 2 * (rails - 1); // 6
        Assert.AreEqual(_cipher.Encrypt(text, rails, 0), _cipher.Encrypt(text, rails, cycle));
    }

    // ---- BuildFence (visualization) ----

    [TestMethod]
    public void BuildFence_ThreeRails_LaysOutZigZag()
    {
        var fence = _cipher.BuildFence("WEAREDISCOVEREDFLEEATONCE", 3, 0, '.');

        Assert.AreEqual(3, fence.Count);
        // Top rail of the canonical example: W . . . E . . . C . . . R . . . L . . . T . . . E
        Assert.AreEqual("W...E...C...R...L...T...E", fence[0]);
    }

    [TestMethod]
    public void BuildFence_RowCount_EqualsRails()
    {
        var fence = _cipher.BuildFence("HELLOWORLD", 4, 0, '.');
        Assert.AreEqual(4, fence.Count);
        // Every row has the same width = text length.
        foreach (var row in fence)
        {
            Assert.AreEqual(10, row.Length);
        }
    }

    // ---- AutoSolve ----

    [TestMethod]
    public void AutoSolve_ContainsTheOriginalPlaintext()
    {
        const string plain = "WEAREDISCOVEREDFLEEATONCE";
        var encrypted = _cipher.Encrypt(plain, 4, 0);
        var candidates = _cipher.AutoSolve(encrypted);
        Assert.IsTrue(candidates.Any(c => c.Text == plain && c.Rails == 4 && c.Offset == 0));
    }

    [TestMethod]
    public void AutoSolve_DefaultOffsetZero_OneCandidatePerRailCount()
    {
        const string text = "WEAREDISCOVEREDFLEEATONCE"; // length 25 -> rails 2..25 = 24 candidates
        var candidates = _cipher.AutoSolve(text);
        Assert.AreEqual(24, candidates.Count);
        Assert.IsTrue(candidates.All(c => c.Offset == 0));
    }

    [TestMethod]
    public void AutoSolve_AllOffsets_FindsPlaintextForNonZeroOffsetKey()
    {
        const string plain = "WEAREDISCOVEREDFLEEATONCE";
        var encrypted = _cipher.Encrypt(plain, 5, 3);
        var candidates = _cipher.AutoSolve(encrypted, allOffsets: true);
        Assert.IsTrue(candidates.Any(c => c.Text == plain && c.Rails == 5 && c.Offset == 3));
    }

    [TestMethod]
    public void AutoSolve_EmptyInput_ReturnsEmpty()
        => Assert.AreEqual(0, _cipher.AutoSolve("").Count);
}
