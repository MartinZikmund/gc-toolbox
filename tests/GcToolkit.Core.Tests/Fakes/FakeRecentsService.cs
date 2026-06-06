using GcToolkit.Core.Recents;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>No-op <see cref="IRecentsService"/> that records opens, for tool ViewModel tests.</summary>
public sealed class FakeRecentsService : IRecentsService
{
    private readonly List<string> _opened = [];

    public IReadOnlyList<string> Opened => _opened;

    public event EventHandler? RecentsChanged;

    public Task RecordOpenedAsync(string toolId)
    {
        _opened.Add(toolId);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetRecentToolIds() => _opened;

    public Task ClearAsync()
    {
        _opened.Clear();
        RecentsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
