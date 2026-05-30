using GcToolkit.Core.Catalog;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Recents;

/// <summary>
/// Persists recents as a JSON list under the <c>recents</c> preferences key, kept in
/// most-recent-first order. Deduplicated by tool id, capped at <see cref="MaxEntries"/>.
/// Entries whose tool is no longer in the catalog are ignored when listing.
/// </summary>
public sealed class RecentsService : IRecentsService
{
    public const int MaxEntries = 10;
    private const string StorageKey = "recents";

    private readonly IPreferences _preferences;
    private readonly ICatalogService _catalog;
    private List<RecentEntry> _entries;

    public RecentsService(IPreferences preferences, ICatalogService catalog)
    {
        _preferences = preferences;
        _catalog = catalog;
        _entries = Load();
    }

    public event EventHandler? RecentsChanged;

    public Task RecordOpenedAsync(string toolId)
    {
        _entries.RemoveAll(e => string.Equals(e.ToolId, toolId, StringComparison.Ordinal));
        _entries.Insert(0, new RecentEntry(toolId, DateTimeOffset.UtcNow));

        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        }

        Save();
        RecentsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetRecentToolIds()
    {
        var known = _catalog.GetTools().Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        return _entries
            .Where(e => known.Contains(e.ToolId))
            .Take(MaxEntries)
            .Select(e => e.ToolId)
            .ToList();
    }

    public Task ClearAsync()
    {
        _entries.Clear();
        Save();
        RecentsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private List<RecentEntry> Load()
    {
        // Defensive: fall back to an empty list if the stored value is corrupt/unreadable.
        try
        {
            return _preferences.GetComplex(StorageKey, new List<RecentEntry>()) ?? new List<RecentEntry>();
        }
        catch (Exception)
        {
            return new List<RecentEntry>();
        }
    }

    private void Save() => _preferences.SetComplex(StorageKey, _entries);
}
