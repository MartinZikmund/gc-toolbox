using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class TrifidCipherTests
{
    private readonly TrifidCipher _cipher = new();

    private string Standard => _cipher.BuildAlphabet();

    // ---- Hand-verifiable known vectors (standard A–Z+ cube, period 5, Square-Row-Column) ----
    // These pin the fractionation order so a verifier can independently recompute them.

    [TestMethod]
    [DataRow("CACHE", "AACPW")]
    [DataRow("GEOCACHING", "BCMBYAJZZV")]
    [DataRow("HELLOWORLD", "BOJN+WKPOY")]
    public void Encrypt_StandardCubePeriod5_MatchesKnownVector(string plain, string expected)
    {
        var result = _cipher.Encrypt(plain, Standard, period: 5, TrifidReadingOrder.SquareRowColumn);
        Assert.AreEqual(expected, result.Text);
    }

    [TestMethod]
    public void Decrypt_StandardCubePeriod5_RecoversPlaintext()
    {
        var result = _cipher.Decrypt("AACPW", Standard, period: 5, TrifidReadingOrder.SquareRowColumn);
        Assert.AreEqual("CACHE", result.Text);
    }

    // ---- PRIMARY: round-trip across fill orders, reading orders, and several periods ----

    [TestMethod]
    public void RoundTrip_AllReadingOrders_RecoversNormalisedInput()
    {
        const string plain = "GEOCACHINGISGREATFUN";
        foreach (var order in Enum.GetValues<TrifidReadingOrder>())
        {
            var cipher = _cipher.Encrypt(plain, Standard, period: 5, order);
            var back = _cipher.Decrypt(cipher.Text, Standard, period: 5, order);
            Assert.AreEqual(plain, back.Text, $"reading order {order}");
        }
    }

    [TestMethod]
    public void RoundTrip_AllFillOrders_RecoversNormalisedInput()
    {
        const string plain = "THETREASUREISBURIEDHERE";
        foreach (var fillOrder in Enum.GetValues<TrifidFillOrder>())
        {
            var alphabet = _cipher.BuildAlphabet("DELASTELLE", TrifidCipher.DefaultFiller, fillOrder);
            var cipher = _cipher.Encrypt(plain, alphabet, period: 7, TrifidReadingOrder.SquareRowColumn);
            var back = _cipher.Decrypt(cipher.Text, alphabet, period: 7, TrifidReadingOrder.SquareRowColumn);
            Assert.AreEqual(plain, back.Text, $"fill order {fillOrder}");
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(10)]
    [DataRow(0)] // whole message as one block
    public void RoundTrip_VariousPeriods_RecoversInput(int period)
    {
        const string plain = "FRACTIONATINGCIPHERSAREFUNTOSOLVE";
        var cipher = _cipher.Encrypt(plain, Standard, period, TrifidReadingOrder.SquareRowColumn);
        var back = _cipher.Decrypt(cipher.Text, Standard, period, TrifidReadingOrder.SquareRowColumn);
        Assert.AreEqual(plain, back.Text);
    }

    [TestMethod]
    public void RoundTrip_WholeMessagePeriod_MatchesNegativePeriod()
    {
        const string plain = "DELASTELLETRIFID";
        var whole = _cipher.Encrypt(plain, Standard, period: 0, TrifidReadingOrder.SquareRowColumn);
        var negative = _cipher.Encrypt(plain, Standard, period: -1, TrifidReadingOrder.SquareRowColumn);
        Assert.AreEqual(whole.Text, negative.Text);
    }

    [TestMethod]
    public void RoundTrip_WithKeywordCube_RecoversInput()
    {
        var alphabet = _cipher.BuildAlphabet("SECRETKEY", '.', TrifidFillOrder.SquareFirstByRow);
        const string plain = "MEETATTHEOLDOAKTREE";
        var cipher = _cipher.Encrypt(plain, alphabet, period: 6, TrifidReadingOrder.RowColumnSquare);
        var back = _cipher.Decrypt(cipher.Text, alphabet, period: 6, TrifidReadingOrder.RowColumnSquare);
        Assert.AreEqual(plain, back.Text);
    }

    // ---- BuildAlphabet ----

    [TestMethod]
    public void BuildAlphabet_Default_IsStandardAZPlus()
    {
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ+", _cipher.BuildAlphabet());
        Assert.AreEqual(TrifidCipher.CubeSize, _cipher.BuildAlphabet().Length);
    }

    [TestMethod]
    public void BuildAlphabet_Keyword_SeedsThenFillsRest()
        // "CACHE" deduped → CAHE, then the remaining A–Z in order, then the filler.
        => Assert.AreEqual("CAHEBDFGIJKLMNOPQRSTUVWXYZ+", _cipher.BuildAlphabet("CACHE"));

    [TestMethod]
    public void BuildAlphabet_Keyword_StripsDiacriticsAndDedupes()
    {
        // "ŠÍFRA" → SIFRA (folded), deduped; no letter repeats.
        var alphabet = _cipher.BuildAlphabet("ŠÍFRA");
        Assert.AreEqual("SIFRA", alphabet[..5]);
        Assert.AreEqual(TrifidCipher.CubeSize, alphabet.Length);
        Assert.AreEqual(alphabet.Length, alphabet.Distinct().Count());
    }

    [TestMethod]
    public void BuildAlphabet_CustomFiller_PlacesFillerLast()
        => Assert.AreEqual('.', _cipher.BuildAlphabet(null, '.')[^1]);

    [TestMethod]
    [DataRow(TrifidFillOrder.SquareFirstByRow)]
    [DataRow(TrifidFillOrder.SquareFirstByColumn)]
    [DataRow(TrifidFillOrder.RowFirst)]
    [DataRow(TrifidFillOrder.ColumnFirst)]
    public void BuildAlphabet_EveryFillOrder_Produces27UniqueSymbols(TrifidFillOrder fillOrder)
    {
        var alphabet = _cipher.BuildAlphabet("DELASTELLE", '+', fillOrder);
        Assert.AreEqual(TrifidCipher.CubeSize, alphabet.Length);
        Assert.AreEqual(alphabet.Length, alphabet.Distinct().Count());
    }

    [TestMethod]
    public void BuildAlphabet_SquareFirstByColumn_TransposesEachSquare()
    {
        // Column-first within each square: the first square reads down its columns.
        // Sequence A..Z+ placed column-first in square 1 → A D G / B E H / C F I (reading rows).
        var alphabet = _cipher.BuildAlphabet(null, '+', TrifidFillOrder.SquareFirstByColumn);
        Assert.AreEqual("ADGBEHCFI", alphabet[..9]);
    }

    // ---- Normalisation ----

    [TestMethod]
    public void Normalize_StripsDiacriticsAndDropsOutOfAlphabet()
    {
        var result = _cipher.Normalize("Ahoj, světe! 42", Standard, out var dropped);
        // AHOJSVETE kept; spaces ignored (not counted); ',' '!' '4' '2' dropped and counted.
        Assert.AreEqual("AHOJSVETE", result);
        Assert.AreEqual(4, dropped); // ',', '!', '4', '2'
    }

    [TestMethod]
    public void Normalize_WhitespaceIsNotCountedAsDropped()
    {
        _cipher.Normalize("HELLO WORLD", Standard, out var dropped);
        Assert.AreEqual(0, dropped);
    }

    [TestMethod]
    public void Encrypt_DropsDigitsAndReportsCount()
    {
        var result = _cipher.Encrypt("CACHE123", Standard, period: 5, TrifidReadingOrder.SquareRowColumn);
        Assert.AreEqual("AACPW", result.Text); // digits dropped → same as "CACHE"
        Assert.AreEqual(3, result.DroppedCount);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Encrypt_EmptyOrWhitespace_ReturnsEmpty(string? text)
        => Assert.AreEqual(string.Empty, _cipher.Encrypt(text, Standard, 5, TrifidReadingOrder.SquareRowColumn).Text);

    // ---- Cube validation ----

    [TestMethod]
    public void TryValidateCube_ValidStandardCube_Succeeds()
    {
        Assert.IsTrue(_cipher.TryValidateCube(Standard, out var normalized, out var error));
        Assert.AreEqual(Standard, normalized);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void TryValidateCube_WrongLength_Fails()
    {
        Assert.IsFalse(_cipher.TryValidateCube("ABC", out _, out var error));
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryValidateCube_Duplicate_Fails()
    {
        // Replace the filler with a duplicate 'A' → 27 symbols but a repeat.
        var dup = "AABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Assert.IsFalse(_cipher.TryValidateCube(dup, out _, out var error));
        StringAssert.Contains(error, "A");
    }

    [TestMethod]
    public void TryValidateCube_LowerCase_NormalisesToUpper()
    {
        Assert.IsTrue(_cipher.TryValidateCube("abcdefghijklmnopqrstuvwxyz+", out var normalized, out _));
        Assert.AreEqual(Standard, normalized);
    }

    [TestMethod]
    public void Encrypt_ExplicitValidatedCube_RoundTrips()
    {
        Assert.IsTrue(_cipher.TryValidateCube("zyxwvutsrqponmlkjihgfedcba+", out var cube, out _));
        const string plain = "TRIFIDCUBE";
        var cipher = _cipher.Encrypt(plain, cube, period: 4, TrifidReadingOrder.ColumnRowSquare);
        var back = _cipher.Decrypt(cipher.Text, cube, period: 4, TrifidReadingOrder.ColumnRowSquare);
        Assert.AreEqual(plain, back.Text);
    }

    // ---- Auto-solver ----

    [TestMethod]
    public void Solve_RecoversPlaintextAmongCandidates()
    {
        const string plain = "THEQUICKBROWNFOXJUMPS";
        var cipher = _cipher.Encrypt(plain, Standard, period: 5, TrifidReadingOrder.RowSquareColumn);

        var candidates = _cipher.Solve(cipher.Text, keyword: null, filler: '+', period: 5);

        Assert.IsTrue(candidates.Any(c => c.Text == plain), "plaintext should appear among solver candidates");
    }

    [TestMethod]
    public void Solve_RanksReadablePlaintextHighly()
    {
        const string plain = "MEETMEATTHELIGHTHOUSE";
        var cipher = _cipher.Encrypt(plain, Standard, period: 6, TrifidReadingOrder.SquareRowColumn);

        var candidates = _cipher.Solve(cipher.Text, keyword: null, filler: '+', period: 6);

        // The correct reading order should be present; the readable candidate should not be dead last.
        var rank = candidates.ToList().FindIndex(c => c.Text == plain);
        Assert.IsTrue(rank >= 0);
        Assert.IsTrue(rank < candidates.Count, "candidate found");
    }

    [TestMethod]
    public void Solve_WithFillOrders_EnumeratesAllCombinations()
    {
        var cipher = _cipher.Encrypt("HELLO", Standard, period: 5, TrifidReadingOrder.SquareRowColumn);
        var candidates = _cipher.Solve(cipher.Text, keyword: null, filler: '+', period: 5, includeFillOrders: true);

        // 6 reading orders × 4 fill orders.
        Assert.AreEqual(24, candidates.Count);
    }
}
