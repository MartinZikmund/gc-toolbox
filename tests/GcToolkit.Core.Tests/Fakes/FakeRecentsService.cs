using GcToolkit.Core.Recents;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>In-memory <see cref="IRecentsService"/> that records opens for ViewModel tests.</summary>
public sealed class FakeRecentsService : IRecentsService
{
    private readonly List<string> _opened = [];

    public IReadOnlyList<string> Opened => _opened;

    public Task RecordOpenedAsync(string toolId)
    {
        _opened.Add(toolId);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetRecentToolIds() => _opened;

    public Task ClearAsync()
    {
        _opened.Clear();
        return Task.CompletedTask;
    }

    public event EventHandler? RecentsChanged;
}
