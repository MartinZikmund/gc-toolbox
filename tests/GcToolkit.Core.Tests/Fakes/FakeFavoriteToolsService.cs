using GcToolkit.Core.FavoriteTools;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>In-memory <see cref="IFavoriteToolsService"/> for ViewModel tests.</summary>
public sealed class FakeFavoriteToolsService : IFavoriteToolsService
{
    private readonly HashSet<string> _favorites = [];

    public bool IsFavorite(string toolId) => _favorites.Contains(toolId);

    public Task ToggleAsync(string toolId)
    {
        if (!_favorites.Add(toolId))
        {
            _favorites.Remove(toolId);
        }

        FavoriteToolsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetFavoriteToolIds() => [.. _favorites];

    public event EventHandler? FavoriteToolsChanged;
}
