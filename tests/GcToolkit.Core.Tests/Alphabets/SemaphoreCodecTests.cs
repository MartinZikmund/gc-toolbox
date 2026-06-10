using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class SemaphoreCodecTests
{
    private readonly SemaphoreCodec _codec = new();

    private static string KindsOf(SemaphoreEncodeResult result)
        => string.Join(",", result.Tokens.Select(t => t.Figure.Kind switch
        {
            SemaphoreFigureKind.Letter => t.Display,
            SemaphoreFigureKind.Space => "<sp>",
            SemaphoreFigureKind.LettersSign => "<let>",
            SemaphoreFigureKind.NumbersSign => "<num>",
            _ => "?",
        }));

    // The canonical parity vector verified live on geocachingtoolbox.com: typing "gc7 a" renders
    // g, c, Numbers sign, g (=7), Space sign, Letters sign, a.
    [TestMethod]
    public void Encode_CanonicalGc7aVector_MatchesReferenceSemantics()
    {
        var result = _codec.Encode("gc7 a");

        Assert.AreEqual("G,C,<num>,7,<sp>,<let>,A", KindsOf(result));
    }

    [TestMethod]
    public void Encode_PlainLetters_ProducesOneFigurePerLetter()
    {
        var result = _codec.Encode("ab");

        Assert.AreEqual("A,B", KindsOf(result));
        Assert.AreEqual(SemaphoreAlphabet.Letters['A'], result.Tokens[0].Figure);
    }

    [TestMethod]
    public void Encode_IsCaseInsensitive_ProducesSameFigures()
    {
        Assert.AreEqual(KindsOf(_codec.Encode("GC")), KindsOf(_codec.Encode("gc")));
    }

    [TestMethod]
    public void Encode_DigitRun_EmitsNumbersSignOnce()
    {
        Assert.AreEqual("<num>,1,2,3", KindsOf(_codec.Encode("123")));
    }

    [TestMethod]
    public void Encode_DigitUsesLetterEquivalentArms()
    {
        var result = _codec.Encode("7");

        Assert.AreEqual(SemaphoreAlphabet.Letters['G'].LeftArm, result.Tokens[1].Figure.LeftArm);
        Assert.AreEqual(SemaphoreAlphabet.Letters['G'].RightArm, result.Tokens[1].Figure.RightArm);
        Assert.AreEqual("7", result.Tokens[1].Display);
    }

    [TestMethod]
    public void Encode_ZeroDigit_UsesKArms()
    {
        var result = _codec.Encode("0");

        Assert.AreEqual(SemaphoreAlphabet.Letters['K'].LeftArm, result.Tokens[1].Figure.LeftArm);
        Assert.AreEqual(SemaphoreAlphabet.Letters['K'].RightArm, result.Tokens[1].Figure.RightArm);
    }

    [TestMethod]
    public void Encode_LetterAfterDigits_EmitsLettersSign()
    {
        Assert.AreEqual("<num>,1,<let>,A", KindsOf(_codec.Encode("1a")));
    }

    [TestMethod]
    public void Encode_SpaceDoesNotResetNumberMode()
    {
        // After a space inside a digit run the Numbers sign is NOT repeated…
        Assert.AreEqual("<num>,1,<sp>,2", KindsOf(_codec.Encode("1 2")));
    }

    [TestMethod]
    public void Encode_LetterAfterSpaceInNumberMode_StillNeedsLettersSign()
    {
        // …but a letter after that space still needs the Letters sign (verified live).
        Assert.AreEqual("<num>,1,<sp>,<let>,A", KindsOf(_codec.Encode("1 a")));
    }

    [TestMethod]
    public void Encode_SpaceBetweenLetters_IsSingleSpaceSign()
    {
        Assert.AreEqual("A,<sp>,B", KindsOf(_codec.Encode("a b")));
    }

    [TestMethod]
    public void Encode_AccentedLetters_FoldToBaseLetter()
    {
        Assert.AreEqual("C,R", KindsOf(_codec.Encode("čř")));
    }

    [TestMethod]
    public void Encode_UnknownCharacters_AreSkippedAndReported()
    {
        var result = _codec.Encode("a?b!");

        Assert.AreEqual("A,B", KindsOf(result));
        Assert.AreEqual("?!", result.UnknownCharacters);
        Assert.IsTrue(result.HasUnknown);
    }

    [TestMethod]
    public void Encode_UnknownCharacters_AreReportedDistinct()
    {
        Assert.AreEqual("?", _codec.Encode("a?b?").UnknownCharacters);
    }

    [TestMethod]
    public void Encode_EmptyOrNull_ReturnsNoTokens()
    {
        Assert.AreEqual(0, _codec.Encode(string.Empty).Tokens.Count);
        Assert.AreEqual(0, _codec.Encode(null).Tokens.Count);
        Assert.IsFalse(_codec.Encode(null).HasUnknown);
    }

    [TestMethod]
    public void Decode_TracksLettersAndNumbersModeStatefully()
    {
        SemaphoreFigure[] figures =
        [
            SemaphoreAlphabet.Letters['G'],
            SemaphoreAlphabet.NumbersSign,
            SemaphoreAlphabet.Letters['G'],
            SemaphoreAlphabet.Space,
            SemaphoreAlphabet.LettersSign,
            SemaphoreAlphabet.Letters['A'],
        ];

        Assert.AreEqual("g7 a", _codec.Decode(figures));
    }

    [TestMethod]
    public void Decode_KInNumberMode_IsZero()
    {
        SemaphoreFigure[] figures = [SemaphoreAlphabet.NumbersSign, SemaphoreAlphabet.Letters['K']];

        Assert.AreEqual("0", _codec.Decode(figures));
    }

    [TestMethod]
    public void Decode_SpaceKeepsNumberMode()
    {
        SemaphoreFigure[] figures =
        [
            SemaphoreAlphabet.NumbersSign,
            SemaphoreAlphabet.Letters['A'],
            SemaphoreAlphabet.Space,
            SemaphoreAlphabet.Letters['B'],
        ];

        Assert.AreEqual("1 2", _codec.Decode(figures));
    }

    [TestMethod]
    public void Decode_LetterWithoutDigitInNumberMode_FallsBackToTheLetter()
    {
        SemaphoreFigure[] figures = [SemaphoreAlphabet.NumbersSign, SemaphoreAlphabet.Letters['M']];

        Assert.AreEqual("m", _codec.Decode(figures));
    }

    [TestMethod]
    public void RoundTrip_CanonicalVector_PreservesText()
    {
        var figures = _codec.Encode("gc7 a").Tokens.Select(t => t.Figure);

        Assert.AreEqual("gc7 a", _codec.Decode(figures));
    }

    [TestMethod]
    public void RoundTrip_MixedDigitsAndWords_PreservesText()
    {
        const string text = "n50 12345 e014 0";
        var figures = _codec.Encode(text).Tokens.Select(t => t.Figure);

        Assert.AreEqual(text, _codec.Decode(figures));
    }

    [DataTestMethod]
    [DataRow("", SemaphoreMode.Letters)]
    [DataRow(null, SemaphoreMode.Letters)]
    [DataRow("abc", SemaphoreMode.Letters)]
    [DataRow("a1", SemaphoreMode.Numbers)]
    [DataRow("a1 ", SemaphoreMode.Numbers)]
    [DataRow("a1b", SemaphoreMode.Letters)]
    [DataRow("1?", SemaphoreMode.Numbers)]
    public void GetFinalMode_FollowsEncodeModeRules(string? text, SemaphoreMode expected)
        => Assert.AreEqual(expected, _codec.GetFinalMode(text));
}
