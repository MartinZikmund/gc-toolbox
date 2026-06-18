using System.Linq;
using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class ColumnarTranspositionTests
{
    private readonly ColumnarTransposition _cipher = new();

    // ---- Column order from a keyword ----

    [TestMethod]
    public void ColumnOrder_Keyword_RanksLettersAlphabetically()
    {
        // ZEBRAS -> A(1) B(2) E(3) R(4) S(5) Z(6) at their positions: Z=5 E=2 B=1 R=3 A=0 S=4
        // Rank (1-based read order) per column position:
        // Z=6, E=3, B=2, R=4, A=1, S=5
        var order = _cipher.ColumnOrder("ZEBRAS");
        CollectionAssert.AreEqual(new[] { 6, 3, 2, 4, 1, 5 }, order.ToArray());
    }

    [TestMethod]
    public void ColumnOrder_DuplicateLetters_ResolvedLeftToRight()
    {
        // TOMATO -> letters A M O O T T ; positions T0 O1 M2 A3 T4 O5
        // sorted: A(3)->1, M(2)->2, O(1)->3, O(5)->4, T(0)->5, T(4)->6
        // so column position ranks: T0=5, O1=3, M2=2, A3=1, T4=6, O5=4
        var order = _cipher.ColumnOrder("TOMATO");
        CollectionAssert.AreEqual(new[] { 5, 3, 2, 1, 6, 4 }, order.ToArray());
    }

    [TestMethod]
    public void ColumnOrder_NumericKey_ParsedDirectly()
    {
        var order = _cipher.ColumnOrder("3 1 4 2");
        CollectionAssert.AreEqual(new[] { 3, 1, 4, 2 }, order.ToArray());
    }

    [TestMethod]
    public void ColumnOrder_NumericKeyCommaSeparated_Parsed()
    {
        var order = _cipher.ColumnOrder("2,4,1,3");
        CollectionAssert.AreEqual(new[] { 2, 4, 1, 3 }, order.ToArray());
    }

    // ---- Canonical Wikipedia vector ----

    [TestMethod]
    public void Encrypt_WikipediaVector_MatchesExactly()
    {
        // keyword ZEBRAS, complete columns, no padding needed (length 25 fits 6 cols irregularly here -> 5 rows)
        var result = _cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", "ZEBRAS");
        Assert.AreEqual("EVLNACDTESEAROFODEECWIREE", result.Text);
    }

    [TestMethod]
    public void Decrypt_WikipediaVector_RecoversPlaintext()
    {
        var result = _cipher.Decrypt("EVLNACDTESEAROFODEECWIREE", "ZEBRAS");
        Assert.AreEqual("WEAREDISCOVEREDFLEEATONCE", result.Text);
    }

    // ---- Round-trips ----

    [TestMethod]
    [DataRow("WEAREDISCOVEREDFLEEATONCE", "ZEBRAS")]
    [DataRow("THEQUICKBROWNFOXJUMPS", "GEOCACHE")]
    [DataRow("ATTACKATDAWN", "SECRET")]
    [DataRow("SHORT", "LONGERKEYWORD")] // key longer than text -> irregular, many empty cells
    public void RoundTrip_Keyword_RecoversInput(string plaintext, string keyword)
    {
        var encrypted = _cipher.Encrypt(plaintext, keyword);
        var decrypted = _cipher.Decrypt(encrypted.Text, keyword);
        Assert.AreEqual(plaintext, decrypted.Text);
    }

    [TestMethod]
    [DataRow("WEAREDISCOVEREDFLEEATONCE", "3 1 4 2")]
    [DataRow("HELLOWORLD", "2 4 1 3 5")]
    [DataRow("GEOCACHING", "4 2 5 1 3")]
    public void RoundTrip_NumericKey_RecoversInput(string plaintext, string numericKey)
    {
        var encrypted = _cipher.Encrypt(plaintext, numericKey);
        var decrypted = _cipher.Decrypt(encrypted.Text, numericKey);
        Assert.AreEqual(plaintext, decrypted.Text);
    }

    [TestMethod]
    public void Encrypt_NumericKeyEqualToKeyword_ProducesSameCipher()
    {
        // ZEBRAS resolves to column order 6 3 2 4 1 5
        var fromKeyword = _cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", "ZEBRAS");
        var fromNumeric = _cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", "6 3 2 4 1 5");
        Assert.AreEqual(fromKeyword.Text, fromNumeric.Text);
    }

    // ---- Irregular (incomplete) grids ----

    [TestMethod]
    public void Decrypt_IrregularGrid_HandlesShortColumnsCorrectly()
    {
        // 7 chars, 3 columns -> rows: 3 (3 full + 1 short row of 1). Columns 0..2 lengths 3,2,2.
        var plaintext = "ABCDEFG";
        var encrypted = _cipher.Encrypt(plaintext, "2 3 1");
        var decrypted = _cipher.Decrypt(encrypted.Text, "2 3 1");
        Assert.AreEqual(plaintext, decrypted.Text);
    }

    [TestMethod]
    public void Encrypt_NoPadding_ProducesSameLengthAsInput()
    {
        var result = _cipher.Encrypt("ABCDEFG", "ZEBRAS");
        Assert.AreEqual(7, result.Text.Length);
    }

    // ---- Padding ----

    [TestMethod]
    public void Encrypt_WithPadding_FillsRectangleWithPadChar()
    {
        // 7 chars, key length 3 -> needs 9 cells -> 2 pad chars
        var options = new ColumnarOptions { Pad = true, PadChar = 'X' };
        var result = _cipher.Encrypt("ABCDEFG", "1 2 3", options);
        Assert.AreEqual(9, result.Text.Length);
        Assert.AreEqual(2, result.Text.Count(c => c == 'X'));
    }

    [TestMethod]
    public void Decrypt_StripPadding_RemovesTrailingPadChars()
    {
        var options = new ColumnarOptions { Pad = true, PadChar = 'X', StripPadding = true };
        var encrypted = _cipher.Encrypt("ABCDEFG", "1 2 3", options);
        var decrypted = _cipher.Decrypt(encrypted.Text, "1 2 3", options);
        Assert.AreEqual("ABCDEFG", decrypted.Text);
    }

    [TestMethod]
    public void Decrypt_KeepPadding_RetainsPadChars()
    {
        var encOptions = new ColumnarOptions { Pad = true, PadChar = 'X' };
        var encrypted = _cipher.Encrypt("ABCDEFG", "1 2 3", encOptions);
        var decOptions = new ColumnarOptions { Pad = true, PadChar = 'X', StripPadding = false };
        var decrypted = _cipher.Decrypt(encrypted.Text, "1 2 3", decOptions);
        Assert.AreEqual("ABCDEFGXX", decrypted.Text);
    }

    // ---- Double / multiple transposition ----

    [TestMethod]
    public void RoundTrip_DoubleTransposition_RecoversInput()
    {
        var options = new ColumnarOptions { SecondKey = "GERMAN" };
        var encrypted = _cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", "ZEBRAS", options);
        var decrypted = _cipher.Decrypt(encrypted.Text, "ZEBRAS", options);
        Assert.AreEqual("WEAREDISCOVEREDFLEEATONCE", decrypted.Text);
    }

    [TestMethod]
    public void Encrypt_DoubleTransposition_DiffersFromSingle()
    {
        var single = _cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", "ZEBRAS");
        var doubleOpts = new ColumnarOptions { SecondKey = "GERMAN" };
        var doubled = _cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", "ZEBRAS", doubleOpts);
        Assert.AreNotEqual(single.Text, doubled.Text);
    }

    // ---- Grid preview ----

    [TestMethod]
    public void BuildGrid_Encrypt_PopulatesRowsLeftToRight()
    {
        var grid = _cipher.BuildGrid("ABCDEF", "ZEBRAS");
        // 6 chars, 6 columns -> single row
        Assert.AreEqual(6, grid.ColumnCount);
        Assert.AreEqual(1, grid.RowCount);
        Assert.AreEqual('A', grid.Cell(0, 0));
        Assert.AreEqual('F', grid.Cell(0, 5));
    }

    [TestMethod]
    public void BuildGrid_ExposesColumnLettersAndOrder()
    {
        var grid = _cipher.BuildGrid("ABCDEF", "ZEBRAS");
        CollectionAssert.AreEqual(new[] { 'Z', 'E', 'B', 'R', 'A', 'S' }, grid.ColumnLetters.ToArray());
        CollectionAssert.AreEqual(new[] { 6, 3, 2, 4, 1, 5 }, grid.ColumnOrder.ToArray());
    }

    [TestMethod]
    public void BuildGrid_IrregularLastRow_LeavesTrailingCellsEmpty()
    {
        var grid = _cipher.BuildGrid("ABCDEFG", "ZEBRAS");
        Assert.AreEqual(2, grid.RowCount);
        Assert.AreEqual('G', grid.Cell(1, 0));
        Assert.IsNull(grid.Cell(1, 1)); // empty trailing cell
    }

    // ---- Validation ----

    [TestMethod]
    public void Encrypt_EmptyKey_Throws()
    {
        Assert.ThrowsExactly<ColumnarException>(() => _cipher.Encrypt("HELLO", ""));
    }

    [TestMethod]
    public void Encrypt_WhitespaceKey_Throws()
    {
        Assert.ThrowsExactly<ColumnarException>(() => _cipher.Encrypt("HELLO", "   "));
    }

    [TestMethod]
    public void Encrypt_NumericKeyNotPermutation_Throws()
    {
        // 1 3 4 is missing 2 -> not a valid 1..n permutation
        Assert.ThrowsExactly<ColumnarException>(() => _cipher.Encrypt("HELLO", "1 3 4"));
    }

    [TestMethod]
    public void Encrypt_EmptyText_ReturnsEmpty()
    {
        var result = _cipher.Encrypt("", "ZEBRAS");
        Assert.AreEqual(string.Empty, result.Text);
    }

    [TestMethod]
    public void TryValidateKey_GoodKeyword_ReturnsTrue()
    {
        Assert.IsTrue(_cipher.TryValidateKey("ZEBRAS", out var length, out _));
        Assert.AreEqual(6, length);
    }

    [TestMethod]
    public void TryValidateKey_BadNumeric_ReturnsFalseWithMessage()
    {
        Assert.IsFalse(_cipher.TryValidateKey("1 3 4", out _, out var error));
        Assert.IsFalse(string.IsNullOrEmpty(error));
    }
}
