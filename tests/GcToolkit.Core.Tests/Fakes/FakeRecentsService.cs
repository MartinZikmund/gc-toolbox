using GcToolkit.Core.Recents;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>In-memory <see cref="IRecentsService"/> for VM tests; records opens in order.</summary>
public sealed class FakeRecentsService : IRecentsService
{
    private readonly List<string> _opened = [];

    public IReadOnlyList<string> Opened => _opened;

    public Task RecordOpenedAsync(string toolId)
    {
        _opened.Remove(toolId);
        _opened.Insert(0, toolId);
        RecentsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetRecentToolIds() => _opened;

    public Task ClearAsync()
    {
        _opened.Clear();
        RecentsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public event EventHandler? RecentsChanged;
}
