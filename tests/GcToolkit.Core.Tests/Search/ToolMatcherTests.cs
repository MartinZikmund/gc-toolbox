using GcToolkit.Core.Search;

namespace GcToolkit.Core.Tests.Search;

[TestClass]
public class ToolMatcherTests
{
    private readonly IToolMatcher _matcher = new ToolMatcher();

    [TestMethod]
    public void Matches_CaseInsensitiveQuery_ReturnsTrue()
        => Assert.IsTrue(_matcher.Matches("CAESAR", ["Caesar cipher"]));

    [TestMethod]
    public void Matches_QueryWithoutDiacritics_MatchesAccentedValue()
        => Assert.IsTrue(_matcher.Matches("reseni", ["řešení"]));

    [TestMethod]
    public void Matches_QueryWithDiacritics_MatchesUnaccentedValue()
        => Assert.IsTrue(_matcher.Matches("řešení", ["reseni"]));

    [TestMethod]
    public void Matches_SubstringOfValue_ReturnsTrue()
        => Assert.IsTrue(_matcher.Matches("ord", ["coordinates"]));

    [TestMethod]
    public void Matches_KeywordAmongValues_ReturnsTrue()
        => Assert.IsTrue(_matcher.Matches("gps", ["Coordinate conversion", "wgs84", "gps"]));

    [TestMethod]
    public void Matches_NoValueContainsQuery_ReturnsFalse()
        => Assert.IsFalse(_matcher.Matches("morse", ["Coordinate conversion", "wgs84", "gps"]));

    [TestMethod]
    public void Matches_EmptyQuery_ReturnsTrue()
        => Assert.IsTrue(_matcher.Matches(string.Empty, ["anything"]));

    [TestMethod]
    public void Matches_WhitespaceQuery_ReturnsTrue()
        => Assert.IsTrue(_matcher.Matches("   ", ["anything"]));

    [TestMethod]
    public void Matches_QueryWithSurroundingWhitespace_IsTrimmed()
        => Assert.IsTrue(_matcher.Matches("  caesar  ", ["Caesar cipher"]));

    [TestMethod]
    public void Matches_EmptyValuesAndNonEmptyQuery_ReturnsFalse()
        => Assert.IsFalse(_matcher.Matches("caesar", []));
}
