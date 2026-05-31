namespace GcToolkit.Core.Discovery;

/// <summary>How recently a tool was introduced or updated, relative to a recency window.</summary>
public enum ToolRecency
{
    None,
    New,
    Updated,
}

/// <summary>
/// Date-driven New/Updated classification, presented WinUI-Gallery-style (research R9). A tool is
/// <see cref="ToolRecency.New"/> when introduced within the window; <see cref="ToolRecency.Updated"/>
/// when introduced earlier but updated within the window; otherwise <see cref="ToolRecency.None"/>.
/// Future dates are not surfaced, and an <c>Updated</c> earlier than <c>Introduced</c> is treated as
/// introduced-only (a tolerated inconsistency).
/// </summary>
public static class ToolRecencyClassifier
{
    public const int DefaultWindowDays = 30;

    public static ToolRecency Classify(DateOnly introduced, DateOnly updated, DateOnly today, int windowDays = DefaultWindowDays)
    {
        var windowStart = today.AddDays(-windowDays);

        // A tool whose introduced date is in the future is not surfaced until that date arrives.
        if (introduced > today)
        {
            return ToolRecency.None;
        }

        if (introduced >= windowStart)
        {
            return ToolRecency.New;
        }

        // Introduced earlier than the window: surfaced as Updated only when the (consistent, non-future)
        // update falls within the window.
        if (updated >= introduced && updated <= today && updated >= windowStart)
        {
            return ToolRecency.Updated;
        }

        return ToolRecency.None;
    }
}
