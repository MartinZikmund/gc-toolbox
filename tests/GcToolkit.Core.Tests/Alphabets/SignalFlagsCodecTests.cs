using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class SignalFlagsCodecTests
{
    private readonly SignalFlagsCodec _codec = new();

    [TestMethod]
    public void Encode_SimpleText_ProducesOneFlagPerCharacter()
    {
        var result = _codec.Encode("gc7");

        Assert.AreEqual(3, result.Tokens.Count);
        Assert.AreEqual("G", result.Tokens[0].Flag!.Id);
        Assert.AreEqual("C", result.Tokens[1].Flag!.Id);
        Assert.AreEqual("7", result.Tokens[2].Flag!.Id);
        Assert.IsFalse(result.HasSkipped);
    }

    [TestMethod]
    public void Encode_IsCaseInsensitive_ProducesSameFlags()
    {
        var lower = _codec.Encode("geocache");
        var upper = _codec.Encode("GEOCACHE");

        CollectionAssert.AreEqual(
            lower.Tokens.Select(t => t.Flag!.Id).ToArray(),
            upper.Tokens.Select(t => t.Flag!.Id).ToArray());
    }

    [TestMethod]
    public void Encode_SpaceBetweenWords_ProducesGapToken()
    {
        var result = _codec.Encode("gc 7");

        Assert.AreEqual(4, result.Tokens.Count);
        Assert.IsTrue(result.Tokens[2].IsGap);
        Assert.IsFalse(result.Tokens[0].IsGap);
    }

    [TestMethod]
    public void Encode_RepeatedWhitespace_CollapsesIntoOneGap()
    {
        var result = _codec.Encode("a  \t b");

        Assert.AreEqual(3, result.Tokens.Count);
        Assert.IsTrue(result.Tokens[1].IsGap);
    }

    [TestMethod]
    public void Encode_LeadingAndTrailingWhitespace_ProducesNoGapTokens()
    {
        var result = _codec.Encode("  ab  ");

        Assert.AreEqual(2, result.Tokens.Count);
        Assert.IsFalse(result.Tokens.Any(t => t.IsGap));
    }

    [TestMethod]
    public void Encode_UnknownCharacters_AreSkippedAndReported()
    {
        var result = _codec.Encode("a!b?!");

        Assert.AreEqual(2, result.Tokens.Count);
        Assert.IsTrue(result.HasSkipped);
        CollectionAssert.AreEqual(new[] { '!', '?' }, result.SkippedCharacters.ToArray());
    }

    [TestMethod]
    public void Encode_AccentedLetters_FoldToBaseLetterFlag()
    {
        var result = _codec.Encode("čížek");

        CollectionAssert.AreEqual(
            new[] { "C", "I", "Z", "E", "K" },
            result.Tokens.Select(t => t.Flag!.Id).ToArray());
        Assert.IsFalse(result.HasSkipped);
    }

    [TestMethod]
    public void Encode_EmptyOrWhitespaceOrNull_ReturnsNoTokens()
    {
        Assert.AreEqual(0, _codec.Encode("").Tokens.Count);
        Assert.AreEqual(0, _codec.Encode("   ").Tokens.Count);
        Assert.AreEqual(0, _codec.Encode(null).Tokens.Count);
    }

    [TestMethod]
    public void ToText_FlagAndGapTokens_RebuildsNormalizedText()
    {
        var tokens = _codec.Encode("GC 7").Tokens;

        Assert.AreEqual("GC 7", SignalFlagsCodec.ToText(tokens));
    }

    [TestMethod]
    public void ToText_RoundTripWithSkippedCharacters_KeepsOnlyMappableContent()
    {
        var tokens = _codec.Encode("go! 42").Tokens;

        Assert.AreEqual("GO 42", SignalFlagsCodec.ToText(tokens));
    }

    [TestMethod]
    public void ToText_EmptyTokens_ReturnsEmpty()
        => Assert.AreEqual(string.Empty, SignalFlagsCodec.ToText([]));
}
