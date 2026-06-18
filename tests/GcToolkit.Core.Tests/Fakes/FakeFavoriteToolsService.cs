using System.Threading.Tasks;
using GcToolkit.Core.FavoriteTools;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>In-memory <see cref="IFavoriteToolsService"/> for ViewModel tests.</summary>
public sealed class FakeFavoriteToolsService : IFavoriteToolsService
{
    private readonly HashSet<string> _favorites = new(StringComparer.Ordinal);

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

    public IReadOnlyList<string> GetFavoriteToolIds() => [.. _favorites];

    public event EventHandler? FavoriteToolsChanged;
}
