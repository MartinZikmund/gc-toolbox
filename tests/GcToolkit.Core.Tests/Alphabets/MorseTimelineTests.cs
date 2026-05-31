using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class MorseTimelineTests
{
    [TestMethod]
    public void Build_EmptyOrNull_ReturnsNoSignals()
    {
        Assert.AreEqual(0, MorseTimeline.Build(null).Count);
        Assert.AreEqual(0, MorseTimeline.Build("").Count);
        Assert.AreEqual(0, MorseTimeline.Build("   ").Count);
    }

    [TestMethod]
    public void Build_Dot_IsOneOnUnit()
    {
        var timeline = MorseTimeline.Build(".");

        Assert.AreEqual(1, timeline.Count);
        Assert.IsTrue(timeline[0].On);
        Assert.AreEqual(1, timeline[0].Units);
    }

    [TestMethod]
    public void Build_Dash_IsThreeOnUnits()
    {
        var timeline = MorseTimeline.Build("-");

        Assert.AreEqual(1, timeline.Count);
        Assert.IsTrue(timeline[0].On);
        Assert.AreEqual(3, timeline[0].Units);
    }

    [TestMethod]
    public void Build_IntraCharacterGap_IsOneUnit()
    {
        // "I" = two dots → on, gap(1), on.
        var timeline = MorseTimeline.Build("..");

        Assert.AreEqual(3, timeline.Count);
        Assert.IsFalse(timeline[1].On);
        Assert.AreEqual(1, timeline[1].Units);
    }

    [TestMethod]
    public void Build_LetterGap_IsThreeUnits()
    {
        // Two letters separated by a single space.
        var timeline = MorseTimeline.Build(". .");

        Assert.AreEqual(3, timeline.Count);
        Assert.IsFalse(timeline[1].On);
        Assert.AreEqual(3, timeline[1].Units);
    }

    [TestMethod]
    public void Build_WordGap_IsSevenUnits()
    {
        var timeline = MorseTimeline.Build(". / .");

        Assert.AreEqual(3, timeline.Count);
        Assert.IsFalse(timeline[1].On);
        Assert.AreEqual(7, timeline[1].Units);
    }

    [TestMethod]
    public void Build_EndsOnAToneNotAGap()
    {
        var timeline = MorseTimeline.Build("... --- ...");

        Assert.IsTrue(timeline[^1].On, "Timeline must not end with a trailing gap.");
    }

    [TestMethod]
    public void Build_SkipsUnknownPlaceholderTokens()
    {
        // The '#' token contributes no signal; only the dot remains.
        var timeline = MorseTimeline.Build("# .");

        Assert.AreEqual(1, timeline.Count);
        Assert.IsTrue(timeline[0].On);
        Assert.AreEqual(1, timeline[0].Units);
    }
}
