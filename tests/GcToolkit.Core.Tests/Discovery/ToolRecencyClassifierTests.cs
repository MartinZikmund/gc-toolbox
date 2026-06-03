using System;
using GcToolkit.Core.Discovery;

namespace GcToolkit.Core.Tests.Discovery;

[TestClass]
public class ToolRecencyClassifierTests
{
    private static readonly DateOnly Today = new(2026, 5, 31);
    private const int WindowDays = 30;

    [TestMethod]
    public void Classify_IntroducedWithinWindow_ReturnsNew()
    {
        DateOnly introduced = new(2026, 5, 10);
        DateOnly updated = new(2026, 5, 10);

        ToolRecency result = ToolRecencyClassifier.Classify(introduced, updated, Today, WindowDays);

        Assert.AreEqual(ToolRecency.New, result);
    }

    [TestMethod]
    public void Classify_IntroducedOldButUpdatedWithinWindow_ReturnsUpdated()
    {
        DateOnly introduced = new(2026, 1, 1);
        DateOnly updated = new(2026, 5, 20);

        ToolRecency result = ToolRecencyClassifier.Classify(introduced, updated, Today, WindowDays);

        Assert.AreEqual(ToolRecency.Updated, result);
    }

    [TestMethod]
    public void Classify_IntroducedAndUpdatedBothOld_ReturnsNone()
    {
        DateOnly introduced = new(2026, 1, 1);
        DateOnly updated = new(2026, 2, 1);

        ToolRecency result = ToolRecencyClassifier.Classify(introduced, updated, Today, WindowDays);

        Assert.AreEqual(ToolRecency.None, result);
    }

    [TestMethod]
    public void Classify_IntroducedInFuture_ReturnsNone()
    {
        DateOnly introduced = new(2026, 6, 15);
        DateOnly updated = new(2026, 6, 15);

        ToolRecency result = ToolRecencyClassifier.Classify(introduced, updated, Today, WindowDays);

        Assert.AreEqual(ToolRecency.None, result);
    }

    [TestMethod]
    public void Classify_UpdatedInFuture_ReturnsNone()
    {
        DateOnly introduced = new(2026, 1, 1);
        DateOnly updated = new(2026, 6, 15);

        ToolRecency result = ToolRecencyClassifier.Classify(introduced, updated, Today, WindowDays);

        Assert.AreEqual(ToolRecency.None, result);
    }

    [TestMethod]
    public void Classify_UpdatedBeforeIntroducedWithOldIntroduced_ReturnsNone()
    {
        DateOnly introduced = new(2026, 1, 1);
        DateOnly updated = new(2025, 12, 1);

        ToolRecency result = ToolRecencyClassifier.Classify(introduced, updated, Today, WindowDays);

        Assert.AreEqual(ToolRecency.None, result);
    }
}
