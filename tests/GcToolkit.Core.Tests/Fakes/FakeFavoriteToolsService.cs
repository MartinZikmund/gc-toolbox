using GcToolkit.Core.FavoriteTools;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>In-memory favorite-tools toggle store for tool VM tests.</summary>
public sealed class FakeFavoriteToolsService : IFavoriteToolsService
{
    private readonly List<string> _favorites = [];

    public bool IsFavorite(string toolId) => _favorites.Contains(toolId);

    public Task ToggleAsync(string toolId)
    {
        if (!_favorites.Remove(toolId))
        {
            _favorites.Add(toolId);
        }

        FavoriteToolsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetFavoriteToolIds() => _favorites;

    public event EventHandler? FavoriteToolsChanged;
}
