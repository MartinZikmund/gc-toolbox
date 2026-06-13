using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public sealed class TapCodeTests
{
    private readonly TapCode _codec = new();

    [TestMethod]
    public void EncodeNumbers_Geo_ReturnsRowColumnPairs()
    {
        // Verified on cachesleuth: G=22, E=15, O=34.
        Assert.AreEqual("22 15 34", _codec.EncodeNumbers("GEO", TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void DecodeNumbers_Geo_ReturnsLetters()
    {
        Assert.AreEqual("GEO", _codec.DecodeNumbers("22 15 34", TapCodeGrid.FiveByFive));
    }

    [DataTestMethod]
    [DataRow('A', "11")]
    [DataRow('B', "12")]
    [DataRow('C', "13")]
    [DataRow('D', "14")]
    [DataRow('E', "15")]
    [DataRow('F', "21")]
    [DataRow('G', "22")]
    [DataRow('H', "23")]
    [DataRow('I', "24")]
    [DataRow('J', "25")]
    [DataRow('K', "13")] // K is merged into the C cell.
    [DataRow('L', "31")]
    [DataRow('M', "32")]
    [DataRow('N', "33")]
    [DataRow('O', "34")]
    [DataRow('P', "35")]
    [DataRow('Q', "41")]
    [DataRow('R', "42")]
    [DataRow('S', "43")]
    [DataRow('T', "44")]
    [DataRow('U', "45")]
    [DataRow('V', "51")]
    [DataRow('W', "52")]
    [DataRow('X', "53")]
    [DataRow('Y', "54")]
    [DataRow('Z', "55")]
    public void EncodeNumbers_EveryLetter_MapsToExpectedCellFiveByFive(char letter, string expected)
    {
        Assert.AreEqual(expected, _codec.EncodeNumbers(letter.ToString(), TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void DecodeNumbers_CCellFiveByFive_ReturnsC()
    {
        // The (1,3) cell decodes to the canonical C (never K).
        Assert.AreEqual("C", _codec.DecodeNumbers("13", TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void EncodeDots_O_RendersRowThenColumnTaps()
    {
        // O = (3,4): three row taps, a single space, four column taps.
        Assert.AreEqual("... ....", _codec.EncodeDots("O", TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void EncodeDots_Geo_SeparatesLettersWithTwoSpaces()
    {
        // G=22, E=15, O=34 -> two spaces between letters, one space between row and column taps.
        Assert.AreEqual(".. ..  . .....  ... ....", _codec.EncodeDots("GEO", TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void DecodeDots_Geo_ReturnsLetters()
    {
        Assert.AreEqual("GEO", _codec.DecodeDots(".. ..  . .....  ... ....", TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void RoundTrip_LettersToNumbersToLetters_IsLossless()
    {
        const string text = "HELLOWORLD";
        var numbers = _codec.EncodeNumbers(text, TapCodeGrid.FiveByFive);
        Assert.AreEqual(text, _codec.DecodeNumbers(numbers, TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void RoundTrip_LettersToDotsToLetters_IsLossless()
    {
        const string text = "TAPCODE";
        var dots = _codec.EncodeDots(text, TapCodeGrid.FiveByFive);
        Assert.AreEqual(text, _codec.DecodeDots(dots, TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void RoundTrip_NumbersToDotsToNumbers_IsLossless()
    {
        // Numbers -> letters -> dots -> letters -> numbers stays stable.
        const string numbers = "22 15 34";
        var letters = _codec.DecodeNumbers(numbers, TapCodeGrid.FiveByFive);
        var dots = _codec.EncodeDots(letters, TapCodeGrid.FiveByFive);
        var back = _codec.DecodeDots(dots, TapCodeGrid.FiveByFive);
        Assert.AreEqual(numbers, _codec.EncodeNumbers(back, TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void EncodeNumbers_Digit_UsesSixBySixGrid()
    {
        // 6x6 grid: A-Z then 0-9, no merge. '0' is the 27th cell -> row 5, col 3.
        Assert.AreEqual("53", _codec.EncodeNumbers("0", TapCodeGrid.SixBySix));
    }

    [TestMethod]
    public void EncodeNumbers_LetterAndDigit_SixBySix()
    {
        // A=(1,1), 9=last cell=(6,6).
        Assert.AreEqual("11 66", _codec.EncodeNumbers("A9", TapCodeGrid.SixBySix));
    }

    [TestMethod]
    public void DecodeNumbers_SixBySix_RoundTripsDigits()
    {
        const string text = "GC2026";
        var numbers = _codec.EncodeNumbers(text, TapCodeGrid.SixBySix);
        Assert.AreEqual(text, _codec.DecodeNumbers(numbers, TapCodeGrid.SixBySix));
    }

    [TestMethod]
    public void DecodeNumbers_MessySeparators_IsLenient()
    {
        // Tolerate slashes, bullets, dashes and arbitrary spacing between pairs.
        Assert.AreEqual("GEO", _codec.DecodeNumbers("2-2 / 1-5 • 3,4", TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void DecodeDots_MessySeparators_IsLenient()
    {
        // Bullets and slashes stand in for taps; spacing is irregular.
        Assert.AreEqual("O", _codec.DecodeDots("•••/••••", TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void EncodeNumbers_LowercaseAndAccent_NormalizesBeforeEncoding()
    {
        // Lowercase folds to upper; the Czech Č folds to its base C cell (1,3).
        Assert.AreEqual("13", _codec.EncodeNumbers("č", TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void EncodeNumbers_UnmappedCharacter_IsSkipped()
    {
        // Spaces and punctuation are not part of the square; only mappable letters encode.
        Assert.AreEqual("22 15", _codec.EncodeNumbers("G E!", TapCodeGrid.FiveByFive));
    }

    [TestMethod]
    public void BuildGrid_FiveByFive_HasMergedCK()
    {
        var grid = TapCode.BuildGrid(TapCodeGrid.FiveByFive);
        Assert.AreEqual(5, grid.Count);
        Assert.AreEqual(5, grid[0].Count);
        Assert.AreEqual("C/K", grid[0][2].Label); // row 1, col 3
        Assert.AreEqual("A", grid[0][0].Label);
        Assert.AreEqual("Z", grid[4][4].Label);
    }

    [TestMethod]
    public void BuildGrid_SixBySix_Has36CellsIncludingDigits()
    {
        var grid = TapCode.BuildGrid(TapCodeGrid.SixBySix);
        Assert.AreEqual(6, grid.Count);
        Assert.AreEqual(6, grid[0].Count);
        Assert.AreEqual("A", grid[0][0].Label);
        Assert.AreEqual("9", grid[5][5].Label);
    }
}
