using GcToolkit.Core.Recents;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>No-op <see cref="IRecentsService"/> that just records the ids it was told about.</summary>
public sealed class FakeRecentsService : IRecentsService
{
    public List<string> Opened { get; } = [];

    public Task RecordOpenedAsync(string toolId)
    {
        Opened.Add(toolId);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> GetRecentToolIds() => Opened;

    public Task ClearAsync()
    {
        Opened.Clear();
        return Task.CompletedTask;
    }

    public event EventHandler? RecentsChanged;
}
