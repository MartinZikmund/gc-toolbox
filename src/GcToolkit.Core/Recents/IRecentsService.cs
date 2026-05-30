namespace GcToolkit.Core.Recents;

/// <summary>Tracks recently opened tools: deduplicated, capped, most-recent-first, clearable.</summary>
public interface IRecentsService
{
    /// <summary>Records an open: dedupes by tool id and bumps it to the top.</summary>
    Task RecordOpenedAsync(string toolId);

    /// <summary>Recent tool ids, most-recent-first, capped at 10, pruned to the current catalog.</summary>
    IReadOnlyList<string> GetRecentToolIds();

    /// <summary>Empties the recents list.</summary>
    Task ClearAsync();

    event EventHandler? RecentsChanged;
}
