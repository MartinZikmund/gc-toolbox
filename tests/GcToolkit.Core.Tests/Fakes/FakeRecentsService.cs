using GcToolkit.Core.Recents;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>In-memory <see cref="IRecentsService"/> for tool VM tests.</summary>
public sealed class FakeRecentsService : IRecentsService
{
    private readonly List<string> _ids = [];

    public IReadOnlyList<string> Recorded => _ids;

    public Task RecordOpenedAsync(string toolId)
    {
        _ids.Remove(toolId);
        _ids.Insert(0, toolId);
        RecentsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetRecentToolIds() => _ids;

    public Task ClearAsync()
    {
        _ids.Clear();
        RecentsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public event EventHandler? RecentsChanged;
}
