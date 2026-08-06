using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class SemaphoreAlphabetTests
{
    [TestMethod]
    public void Letters_ContainsAllTwentySixLetters()
    {
        for (var c = 'A'; c <= 'Z'; c++)
        {
            Assert.IsTrue(SemaphoreAlphabet.Letters.ContainsKey(c), $"Missing letter {c}");
        }

        Assert.AreEqual(26, SemaphoreAlphabet.Letters.Count);
    }

    [TestMethod]
    public void Letters_AllArmPositionPairsAreDistinct()
    {
        var positions = SemaphoreAlphabet.Letters.Values
            .Select(f => (f.LeftArm, f.RightArm))
            .Distinct()
            .Count();

        Assert.AreEqual(26, positions);
    }

    // Distinct arm *positions* aren't enough: two letters must not draw the same picture either
    // (e.g. left-across-low + right-up would render exactly like left-up + right-low).
    [TestMethod]
    public void Letters_AllRenderedFlagDirectionsAreDistinct()
    {
        var collisions = SemaphoreAlphabet.Letters
            .GroupBy(pair => string.Join("/", new[] { pair.Value.LeftFlagAngle, pair.Value.RightFlagAngle }.Order()))
            .Where(group => group.Count() > 1)
            .Select(group => string.Join("=", group.Select(pair => pair.Key)));

        Assert.AreEqual(string.Empty, string.Join(", ", collisions), "Letters that draw the same figure");
    }

    // Canonical positions cross-checked against the standard flag-semaphore chart
    // (left/right are the signaller's own arms).
    [DataTestMethod]
    [DataRow('A', SemaphoreArmPosition.Down, SemaphoreArmPosition.Low)]
    [DataRow('B', SemaphoreArmPosition.Down, SemaphoreArmPosition.Out)]
    [DataRow('C', SemaphoreArmPosition.Down, SemaphoreArmPosition.High)]
    [DataRow('D', SemaphoreArmPosition.Down, SemaphoreArmPosition.Up)]
    [DataRow('E', SemaphoreArmPosition.High, SemaphoreArmPosition.Down)]
    [DataRow('F', SemaphoreArmPosition.Out, SemaphoreArmPosition.Down)]
    [DataRow('G', SemaphoreArmPosition.Low, SemaphoreArmPosition.Down)]
    [DataRow('H', SemaphoreArmPosition.AcrossLow, SemaphoreArmPosition.Out)]
    [DataRow('I', SemaphoreArmPosition.AcrossLow, SemaphoreArmPosition.High)]
    [DataRow('J', SemaphoreArmPosition.Out, SemaphoreArmPosition.Up)]
    [DataRow('K', SemaphoreArmPosition.Up, SemaphoreArmPosition.Low)]
    [DataRow('L', SemaphoreArmPosition.High, SemaphoreArmPosition.Low)]
    [DataRow('M', SemaphoreArmPosition.Out, SemaphoreArmPosition.Low)]
    [DataRow('N', SemaphoreArmPosition.Low, SemaphoreArmPosition.Low)]
    [DataRow('O', SemaphoreArmPosition.AcrossHigh, SemaphoreArmPosition.Out)]
    [DataRow('P', SemaphoreArmPosition.Up, SemaphoreArmPosition.Out)]
    [DataRow('Q', SemaphoreArmPosition.High, SemaphoreArmPosition.Out)]
    [DataRow('R', SemaphoreArmPosition.Out, SemaphoreArmPosition.Out)]
    [DataRow('S', SemaphoreArmPosition.Low, SemaphoreArmPosition.Out)]
    [DataRow('T', SemaphoreArmPosition.Up, SemaphoreArmPosition.High)]
    [DataRow('U', SemaphoreArmPosition.High, SemaphoreArmPosition.High)]
    [DataRow('V', SemaphoreArmPosition.Low, SemaphoreArmPosition.Up)]
    [DataRow('W', SemaphoreArmPosition.Out, SemaphoreArmPosition.AcrossHigh)]
    [DataRow('X', SemaphoreArmPosition.Low, SemaphoreArmPosition.AcrossHigh)]
    [DataRow('Y', SemaphoreArmPosition.Out, SemaphoreArmPosition.High)]
    [DataRow('Z', SemaphoreArmPosition.Out, SemaphoreArmPosition.AcrossLow)]
    public void Letters_CanonicalArmPositions_MatchStandardChart(char letter, SemaphoreArmPosition left, SemaphoreArmPosition right)
    {
        var figure = SemaphoreAlphabet.Letters[letter];

        Assert.AreEqual(left, figure.LeftArm, $"{letter}: left arm");
        Assert.AreEqual(right, figure.RightArm, $"{letter}: right arm");
    }

    [TestMethod]
    public void Space_IsTheRestPosition_BothArmsDown()
    {
        Assert.AreEqual(SemaphoreArmPosition.Down, SemaphoreAlphabet.Space.LeftArm);
        Assert.AreEqual(SemaphoreArmPosition.Down, SemaphoreAlphabet.Space.RightArm);
        Assert.AreEqual(SemaphoreFigureKind.Space, SemaphoreAlphabet.Space.Kind);
    }

    [TestMethod]
    public void NumbersSign_LeftHighRightUp()
    {
        Assert.AreEqual(SemaphoreArmPosition.High, SemaphoreAlphabet.NumbersSign.LeftArm);
        Assert.AreEqual(SemaphoreArmPosition.Up, SemaphoreAlphabet.NumbersSign.RightArm);
        Assert.AreEqual(SemaphoreFigureKind.NumbersSign, SemaphoreAlphabet.NumbersSign.Kind);
    }

    [TestMethod]
    public void LettersSign_SharesArmPositionsWithJ_ButIsDistinct()
    {
        var j = SemaphoreAlphabet.Letters['J'];
        var letters = SemaphoreAlphabet.LettersSign;

        Assert.AreEqual(j.LeftArm, letters.LeftArm);
        Assert.AreEqual(j.RightArm, letters.RightArm);
        Assert.AreEqual(SemaphoreFigureKind.LettersSign, letters.Kind);
        Assert.AreNotEqual(j, letters);
    }

    [DataTestMethod]
    [DataRow('1', 'A')]
    [DataRow('2', 'B')]
    [DataRow('3', 'C')]
    [DataRow('4', 'D')]
    [DataRow('5', 'E')]
    [DataRow('6', 'F')]
    [DataRow('7', 'G')]
    [DataRow('8', 'H')]
    [DataRow('9', 'I')]
    [DataRow('0', 'K')]
    public void DigitToLetter_MapsDigitsToLetterEquivalents(char digit, char letter)
        => Assert.AreEqual(letter, SemaphoreAlphabet.DigitToLetter[digit]);

    [DataTestMethod]
    [DataRow('A', '1')]
    [DataRow('I', '9')]
    [DataRow('K', '0')]
    public void Letters_LetterWithDigitEquivalent_CarriesItsDigit(char letter, char digit)
        => Assert.AreEqual(digit, SemaphoreAlphabet.Letters[letter].Digit);

    [DataTestMethod]
    [DataRow('J')]
    [DataRow('L')]
    [DataRow('Z')]
    public void Letters_LetterWithoutDigitEquivalent_HasNoDigit(char letter)
        => Assert.IsNull(SemaphoreAlphabet.Letters[letter].Digit);

    [TestMethod]
    public void ChartFigures_ListsAllLettersThenSpaceLettersNumbers()
    {
        var chart = SemaphoreAlphabet.ChartFigures;

        Assert.AreEqual(29, chart.Count);
        Assert.AreEqual('A', chart[0].Letter);
        Assert.AreEqual('Z', chart[25].Letter);
        Assert.AreEqual(SemaphoreFigureKind.Space, chart[26].Kind);
        Assert.AreEqual(SemaphoreFigureKind.LettersSign, chart[27].Kind);
        Assert.AreEqual(SemaphoreFigureKind.NumbersSign, chart[28].Kind);
    }

    [TestMethod]
    public void Cancel_IsTheUpperLeftToLowerRightDiagonal()
    {
        var cancel = SemaphoreAlphabet.Cancel;

        Assert.AreEqual(SemaphoreFigureKind.Cancel, cancel.Kind);
        Assert.AreEqual(SemaphoreArmPosition.Low, cancel.LeftArm);
        Assert.AreEqual(SemaphoreArmPosition.High, cancel.RightArm);
        Assert.IsNull(cancel.SecondaryLeftFlagAngle);
        Assert.IsNull(cancel.SecondaryRightFlagAngle);
    }

    [TestMethod]
    public void Error_WavesBothArmsBetweenHighAndLow()
    {
        var error = SemaphoreAlphabet.Error;

        Assert.AreEqual(SemaphoreFigureKind.Error, error.Kind);
        Assert.AreEqual(SemaphoreArmPosition.High, error.LeftArm);
        Assert.AreEqual(SemaphoreArmPosition.High, error.RightArm);
        Assert.AreEqual(225.0, error.LeftFlagAngle);
        Assert.AreEqual(135.0, error.RightFlagAngle);
        // The waving "down" pose, rendered as a faint ghost so Error reads as motion, not as U.
        Assert.AreEqual(315.0, error.SecondaryLeftFlagAngle);
        Assert.AreEqual(45.0, error.SecondaryRightFlagAngle);
    }

    [TestMethod]
    public void Letters_HaveNoSecondaryWaveAngles()
    {
        var a = SemaphoreAlphabet.Letters['A'];

        Assert.IsNull(a.SecondaryLeftFlagAngle);
        Assert.IsNull(a.SecondaryRightFlagAngle);
    }

    [TestMethod]
    public void SpecialFigures_AreCancelThenError_AndExcludedFromTheTappableChart()
    {
        var specials = SemaphoreAlphabet.SpecialFigures;

        Assert.AreEqual(2, specials.Count);
        Assert.AreEqual(SemaphoreFigureKind.Cancel, specials[0].Kind);
        Assert.AreEqual(SemaphoreFigureKind.Error, specials[1].Kind);
        Assert.IsFalse(
            SemaphoreAlphabet.ChartFigures.Any(f => f.Kind is SemaphoreFigureKind.Cancel or SemaphoreFigureKind.Error),
            "Special reference signals must not be part of the tappable chart.");
    }

    // Viewer-facing flag angles: degrees clockwise from straight down, signaller facing the viewer
    // (so the signaller's right arm renders on the viewer's left).
    [DataTestMethod]
    [DataRow('A', 0.0, 45.0)]
    [DataRow('D', 0.0, 180.0)]
    [DataRow('G', 315.0, 0.0)]
    [DataRow('J', 270.0, 180.0)]
    [DataRow('N', 315.0, 45.0)]
    [DataRow('R', 270.0, 90.0)]
    [DataRow('W', 270.0, 225.0)]
    [DataRow('Z', 270.0, 315.0)]
    public void FlagAngles_ViewerFacing_MatchExpectedDegrees(char letter, double leftAngle, double rightAngle)
    {
        var figure = SemaphoreAlphabet.Letters[letter];

        Assert.AreEqual(leftAngle, figure.LeftFlagAngle, $"{letter}: left flag angle");
        Assert.AreEqual(rightAngle, figure.RightFlagAngle, $"{letter}: right flag angle");
    }
}
