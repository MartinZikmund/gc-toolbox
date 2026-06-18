using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class PolybiusSquareTests
{
    private static PolybiusSquare Classic() => new(new PolybiusOptions());

    // ---- Classic 5×5, I/J merged, digit labels ----

    [TestMethod]
    [DataRow('A', "11")]
    [DataRow('B', "12")]
    [DataRow('C', "13")]
    [DataRow('E', "15")]
    [DataRow('F', "21")]
    [DataRow('H', "23")]
    [DataRow('Z', "55")]
    public void Encrypt_ClassicSquare_MapsLetterToExpectedPair(char letter, string expected)
        => Assert.AreEqual(expected, Classic().Encrypt(letter.ToString()));

    [TestMethod]
    public void Encrypt_Hello_ProducesClassicVector()
        => Assert.AreEqual("23 15 31 31 34", Classic().Encrypt("HELLO"));

    [TestMethod]
    public void Encrypt_MergedJ_MapsToSameCellAsI()
    {
        var square = Classic();
        // I and J share a cell, so both encode to the same pair.
        Assert.AreEqual(square.Encrypt("I"), square.Encrypt("J"));
        Assert.AreEqual("24", square.Encrypt("I"));
    }

    [TestMethod]
    public void Encrypt_SkipsCharactersNotInTheSquare()
        => Assert.AreEqual("23 15", Classic().Encrypt("H, E!"));

    [TestMethod]
    public void Encrypt_IsCaseInsensitive()
        => Assert.AreEqual(Classic().Encrypt("HELLO"), Classic().Encrypt("hello"));

    [TestMethod]
    public void Encrypt_StripsDiacritics()
        => Assert.AreEqual(Classic().Encrypt("E"), Classic().Encrypt("É"));

    [TestMethod]
    public void Encrypt_EmptyInput_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, Classic().Encrypt(""));

    // ---- Decrypt ----

    [TestMethod]
    public void Decrypt_ClassicVector_RecoversPlaintext()
    {
        var result = Classic().Decrypt("23 15 31 31 34");
        Assert.AreEqual("HELLO", result.Text);
        Assert.IsTrue(result.IsClean);
    }

    [TestMethod]
    public void Decrypt_ToleratesAnySeparatorAndWhitespace()
    {
        var result = Classic().Decrypt("23,15;31-31|34");
        Assert.AreEqual("HELLO", result.Text);
        Assert.IsTrue(result.IsClean);
    }

    [TestMethod]
    public void Decrypt_ContinuousDigitStream_IsAccepted()
    {
        var result = Classic().Decrypt("2315313134");
        Assert.AreEqual("HELLO", result.Text);
        Assert.IsTrue(result.IsClean);
    }

    [TestMethod]
    public void Decrypt_OddDigitCount_FlagsLeftover()
    {
        var result = Classic().Decrypt("231");
        Assert.IsTrue(result.HasLeftover);
        Assert.IsFalse(result.IsClean);
    }

    [TestMethod]
    public void Decrypt_NoiseCharacters_AreFlaggedNotDropped()
    {
        var result = Classic().Decrypt("23 ?? 15");
        Assert.AreEqual("HE", result.Text);
        CollectionAssert.Contains(result.InvalidPairs.ToArray(), "??");
        Assert.IsFalse(result.IsClean);
    }

    [TestMethod]
    public void Decrypt_EmptyInput_ReturnsCleanEmpty()
    {
        var result = Classic().Decrypt("");
        Assert.AreEqual(string.Empty, result.Text);
        Assert.IsTrue(result.IsClean);
    }

    // ---- Round trip ----

    [TestMethod]
    [DataRow("THEQUICKBROWNFOXIUMPSOVERALAZYDOG")]
    [DataRow("GEOCACHING")]
    [DataRow("POLYBIUS")]
    public void RoundTrip_EncryptThenDecrypt_PreservesText(string text)
    {
        var square = Classic();
        var decoded = square.Decrypt(square.Encrypt(text)).Text;
        Assert.AreEqual(text, decoded);
    }

    // ---- Keyed alphabet ----

    [TestMethod]
    public void KeyedSquare_PlacesKeywordLettersFirst()
    {
        var square = new PolybiusSquare(new PolybiusOptions(Keyword: "KEYWORD"));
        var firstRow = square.GetCells()[0];

        // KEYWORD deduped = K E Y W O R D, which fills the first row of a 5×5.
        var letters = string.Concat(firstRow.Select(c => c.Letter));
        Assert.AreEqual("KEYWORD"[..5], letters);
    }

    [TestMethod]
    public void KeyedSquare_DedupesKeywordLetters()
    {
        // "LETTER" -> L E T R, the duplicate T/E collapse.
        var square = new PolybiusSquare(new PolybiusOptions(Keyword: "LETTER"));
        Assert.AreEqual("11", square.Encrypt("L"));
        Assert.AreEqual("12", square.Encrypt("E"));
        Assert.AreEqual("13", square.Encrypt("T"));
        Assert.AreEqual("14", square.Encrypt("R"));
    }

    [TestMethod]
    public void KeyedSquare_ReverseKeyword_SeedsReversedLetters()
    {
        var square = new PolybiusSquare(new PolybiusOptions(Keyword: "KEYWORD", ReverseKeyword: true));
        // Reversed "DROWYEK" deduped = D R O W Y E K, first five = D R O W Y.
        var letters = string.Concat(square.GetCells()[0].Select(c => c.Letter));
        Assert.AreEqual("DROWY", letters);
    }

    [TestMethod]
    public void KeyedSquare_LastInstance_KeepsTheLaterOccurrence()
    {
        // "ALPHA" keep-first = A L P H; keep-last moves A after its repeat = L P H A.
        var first = new PolybiusSquare(new PolybiusOptions(Keyword: "ALPHA"));
        var last = new PolybiusSquare(new PolybiusOptions(Keyword: "ALPHA",
            KeywordPlacement: PolybiusKeywordPlacement.LastInstance));

        Assert.AreEqual("ALPH", string.Concat(first.GetCells()[0].Take(4).Select(c => c.Letter)));
        Assert.AreEqual("LPHA", string.Concat(last.GetCells()[0].Take(4).Select(c => c.Letter)));
    }

    [TestMethod]
    public void KeyedSquare_RoundTrips()
    {
        var square = new PolybiusSquare(new PolybiusOptions(Keyword: "GEOCACHE"));
        var decoded = square.Decrypt(square.Encrypt("FINDTHECACHE")).Text;
        Assert.AreEqual("FINDTHECACHE", decoded);
    }

    // ---- Configurable merge pair ----

    [TestMethod]
    public void MergePair_CK_FoldsCOntoK_AndDropsC()
    {
        var square = new PolybiusSquare(new PolybiusOptions(MergeFrom: 'C', MergeInto: 'K'));
        // C and K now share a cell; encoding either yields the same pair.
        Assert.AreEqual(square.Encrypt("K"), square.Encrypt("C"));
        // With C removed, the square is A B D E F / G H I J K / ... so I and J are now distinct.
        Assert.AreNotEqual(square.Encrypt("I"), square.Encrypt("J"));
    }

    // ---- 6×6 grid ----

    [TestMethod]
    public void SixBySix_IncludesDigits()
    {
        var square = new PolybiusSquare(new PolybiusOptions(Size: PolybiusGridSize.SixBySix));
        Assert.AreEqual("11", square.Encrypt("A"));
        Assert.AreEqual("66", square.Encrypt("9"));
        Assert.AreEqual(6, square.Side);
    }

    [TestMethod]
    public void SixBySix_DoesNotMergeIAndJ()
    {
        var square = new PolybiusSquare(new PolybiusOptions(Size: PolybiusGridSize.SixBySix));
        Assert.AreNotEqual(square.Encrypt("I"), square.Encrypt("J"));
    }

    [TestMethod]
    public void SixBySix_RoundTripsLettersAndDigits()
    {
        var square = new PolybiusSquare(new PolybiusOptions(Size: PolybiusGridSize.SixBySix));
        var decoded = square.Decrypt(square.Encrypt("CACHE2025")).Text;
        Assert.AreEqual("CACHE2025", decoded);
    }

    // ---- Label schemes ----

    [TestMethod]
    public void AdfgxLabels_EncodeHelloWithLetters()
    {
        var square = new PolybiusSquare(new PolybiusOptions(LabelScheme: PolybiusLabelScheme.Adfgx));
        // H = row 2 col 3 -> labels A D F G X -> row2=D, col3=F -> "DF".
        Assert.AreEqual("DF", square.Encrypt("H"));
    }

    [TestMethod]
    public void AdfgxLabels_RoundTrip()
    {
        var square = new PolybiusSquare(new PolybiusOptions(LabelScheme: PolybiusLabelScheme.Adfgx));
        var decoded = square.Decrypt(square.Encrypt("ATTACK")).Text;
        Assert.AreEqual("ATTACK", decoded);
    }

    [TestMethod]
    public void Adfgvx_For6x6_UsesSixLabels()
    {
        var square = new PolybiusSquare(new PolybiusOptions(
            Size: PolybiusGridSize.SixBySix, LabelScheme: PolybiusLabelScheme.Adfgx));
        Assert.AreEqual(PolybiusSquare.AdfgvxLabels, square.RowLabels);
    }

    [TestMethod]
    public void CustomLabels_AreUsedForPairs()
    {
        var square = new PolybiusSquare(new PolybiusOptions(
            LabelScheme: PolybiusLabelScheme.Custom, CustomLabels: "VWXYZ"));
        // A = row0 col0 -> "VV".
        Assert.AreEqual("VV", square.Encrypt("A"));
    }

    // ---- Read order ----

    [TestMethod]
    public void ColumnThenRow_SwapsCoordinateOrder()
    {
        var rowFirst = Classic();
        var colFirst = new PolybiusSquare(new PolybiusOptions(ColumnThenRow: true));
        // H is row2 col3 -> row-first "23", column-first "32".
        Assert.AreEqual("23", rowFirst.Encrypt("H"));
        Assert.AreEqual("32", colFirst.Encrypt("H"));
    }

    [TestMethod]
    public void ColumnThenRow_RoundTrips()
    {
        var square = new PolybiusSquare(new PolybiusOptions(ColumnThenRow: true));
        var decoded = square.Decrypt(square.Encrypt("PUZZLE")).Text;
        Assert.AreEqual("PUZZLE", decoded);
    }

    // ---- Separator ----

    [TestMethod]
    public void Separator_None_ConcatenatesPairs()
    {
        var square = new PolybiusSquare(new PolybiusOptions(Separator: ""));
        Assert.AreEqual("2315313134", square.Encrypt("HELLO"));
    }

    [TestMethod]
    public void Separator_Comma_JoinsWithComma()
    {
        var square = new PolybiusSquare(new PolybiusOptions(Separator: ", "));
        Assert.AreEqual("23, 15", square.Encrypt("HE"));
    }

    // ---- Rendered cells ----

    [TestMethod]
    public void GetCells_ReturnsFullGridWithCoordinates()
    {
        var cells = Classic().GetCells();
        Assert.AreEqual(5, cells.Count);
        Assert.AreEqual(5, cells[0].Count);
        Assert.AreEqual("11", cells[0][0].Coordinate);
        Assert.AreEqual("A", cells[0][0].Letter);
    }

    [TestMethod]
    public void GetCells_MergedCell_ShowsBothLetters()
    {
        var merged = Classic().GetCells().SelectMany(r => r).First(c => c.Letter.Contains('/'));
        Assert.AreEqual("I/J", merged.Letter);
    }

    [TestMethod]
    public void CoordinateFor_ReturnsPairForLetter()
        => Assert.AreEqual("23", Classic().CoordinateFor('H'));

    [TestMethod]
    public void CoordinateFor_UnknownChar_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, new PolybiusSquare(new PolybiusOptions()).CoordinateFor(' '));

    // ---- Validation ----

    [TestMethod]
    public void Constructor_CustomLabelsWrongLength_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() =>
            new PolybiusSquare(new PolybiusOptions(LabelScheme: PolybiusLabelScheme.Custom, CustomLabels: "AB")));

    [TestMethod]
    public void Constructor_CustomLabelsNotDistinct_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() =>
            new PolybiusSquare(new PolybiusOptions(LabelScheme: PolybiusLabelScheme.Custom, CustomLabels: "AABCD")));

    [TestMethod]
    public void Constructor_InvalidMergePair_Throws()
        => Assert.ThrowsExactly<ArgumentException>(() =>
            new PolybiusSquare(new PolybiusOptions(MergeFrom: '5', MergeInto: 'I')));
}
