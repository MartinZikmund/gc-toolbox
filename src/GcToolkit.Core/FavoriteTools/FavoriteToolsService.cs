using GcToolkit.Core.Catalog;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.FavoriteTools;

/// <summary>
/// Persists favorite tools as a JSON list under the <c>favoriteTools</c> preferences key. Entries
/// whose tool is no longer in the catalog are ignored when listing (defensive against stale data).
/// </summary>
public sealed class FavoriteToolsService : IFavoriteToolsService
{
    private const string StorageKey = "favoriteTools";

    private readonly IPreferences _preferences;
    private readonly ICatalogService _catalog;
    private readonly List<FavoriteToolEntry> _entries;

    public FavoriteToolsService(IPreferences preferences, ICatalogService catalog)
    {
        _preferences = preferences;
        _catalog = catalog;
        _entries = Load();
    }

    public event EventHandler? FavoriteToolsChanged;

    public bool IsFavorite(string toolId)
        => _entries.Any(e => string.Equals(e.ToolId, toolId, StringComparison.Ordinal));

    public Task ToggleAsync(string toolId)
    {
        var existing = _entries.FirstOrDefault(e => string.Equals(e.ToolId, toolId, StringComparison.Ordinal));
        if (existing is not null)
        {
            _entries.Remove(existing);
        }
        else
        {
            _entries.Add(new FavoriteToolEntry(toolId, DateTimeOffset.UtcNow));
        }

        Save();
        FavoriteToolsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetFavoriteToolIds()
    {
        var known = _catalog.GetTools().Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        return _entries
            .Where(e => known.Contains(e.ToolId))
            .OrderBy(e => e.AddedUtc)
            .Select(e => e.ToolId)
            .ToList();
    }

    private List<FavoriteToolEntry> Load()
    {
        // Defensive: fall back to an empty list if the stored value is corrupt/unreadable.
        try
        {
            return _preferences.GetComplex(StorageKey, new List<FavoriteToolEntry>()) ?? new List<FavoriteToolEntry>();
        }
        catch (Exception)
        {
            return new List<FavoriteToolEntry>();
        }
    }

    private void Save() => _preferences.SetComplex(StorageKey, _entries);
}
