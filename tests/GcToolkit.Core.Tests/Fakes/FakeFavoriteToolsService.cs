using GcToolkit.Core.FavoriteTools;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>In-memory <see cref="IFavoriteToolsService"/> for tool VM tests.</summary>
public sealed class FakeFavoriteToolsService : IFavoriteToolsService
{
    private readonly List<string> _ids = [];

    public bool IsFavorite(string toolId) => _ids.Contains(toolId);

    public Task ToggleAsync(string toolId)
    {
        if (!_ids.Remove(toolId))
        {
            _ids.Add(toolId);
        }

        FavoriteToolsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetFavoriteToolIds() => _ids;

    public event EventHandler? FavoriteToolsChanged;
}
