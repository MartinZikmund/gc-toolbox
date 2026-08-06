using GcToolkit.Core.Ciphers;

namespace GcToolkit.Core.Tests.Ciphers;

[TestClass]
public class PhoneKeypadDictionaryTests
{
    // Deliberately unsorted, with the *less* common word first, to prove frequency order is preserved.
    private static readonly string[] _words = ["eight", "digit", "fight", "cache", "abc", "hello"];

    private readonly PhoneKeypadDictionary _dictionary = new(_words);

    [TestMethod]
    public void WordsFor_AmbiguousCode_ReturnsEveryWordInFrequencyOrder()
    {
        // 34448 is the site's own example: digit / eight / fight.
        var matches = _dictionary.WordsFor("34448");

        CollectionAssert.AreEqual(new[] { "EIGHT", "DIGIT", "FIGHT" }, matches.ToArray());
    }

    [TestMethod]
    public void WordsFor_UnmatchedCode_ReturnsEmpty()
        => Assert.AreEqual(0, _dictionary.WordsFor("99999").Count);

    [TestMethod]
    public void WordsFor_RespectsMaxCap()
        => Assert.AreEqual(2, _dictionary.WordsFor("34448", max: 2).Count);

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void WordsFor_EmptyOrNull_ReturnsEmpty(string? code)
        => Assert.AreEqual(0, _dictionary.WordsFor(code).Count);

    [TestMethod]
    public void WordsFor_MatchesExactLengthOnly()
    {
        // 22243 is CACHE; the shorter prefix 222 must not drag CACHE in (and vice versa).
        CollectionAssert.AreEqual(new[] { "CACHE" }, _dictionary.WordsFor("22243").ToArray());
        CollectionAssert.AreEqual(new[] { "ABC" }, _dictionary.WordsFor("222").ToArray());
    }

    [TestMethod]
    public void Constructor_DropsWordsWithNoKeypadKey()
    {
        PhoneKeypadDictionary dictionary = new(["ok", "1st", "a-b", "  "]);

        Assert.AreEqual(1, dictionary.Count);
        CollectionAssert.AreEqual(new[] { "OK" }, dictionary.WordsFor("65").ToArray());
    }

    [TestMethod]
    public void English_BundledListDecodesTheSiteExample()
    {
        var matches = PhoneKeypadDictionary.English.WordsFor("34448");

        CollectionAssert.Contains(matches.ToArray(), "DIGIT");
        CollectionAssert.Contains(matches.ToArray(), "EIGHT");
        CollectionAssert.Contains(matches.ToArray(), "FIGHT");
    }

    [TestMethod]
    public void English_RanksCommonWordsFirst()
    {
        // 4663 is GOOD / HOME / HOOD / INNE…; the common words must lead.
        var matches = PhoneKeypadDictionary.English.WordsFor("4663");

        Assert.IsTrue(matches.Count > 0);
        CollectionAssert.Contains(new[] { "GOOD", "HOME" }, matches[0]);
    }
}
