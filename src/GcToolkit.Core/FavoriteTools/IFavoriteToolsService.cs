namespace GcToolkit.Core.FavoriteTools;

/// <summary>
/// Tracks the user's favorited <em>tools</em> (toggle, query, persisted locally). Named
/// tool-specifically to avoid confusion with geocaching "favorites" added in later features.
/// </summary>
public interface IFavoriteToolsService
{
    bool IsFavorite(string toolId);

    /// <summary>Adds the tool if absent, removes it if present.</summary>
    Task ToggleAsync(string toolId);

    /// <summary>Favorited tool ids in stable order (by when they were added), pruned to the current catalog.</summary>
    IReadOnlyList<string> GetFavoriteToolIds();

    event EventHandler? FavoriteToolsChanged;
}
