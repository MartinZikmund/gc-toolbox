using System.Threading.Tasks;
using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Records the last shared payload so VM share commands can be asserted without a real share sheet.</summary>
public sealed class FakeShareService : IShareService
{
    public string? LastTitle { get; private set; }

    public string? LastText { get; private set; }

    public int ShareTextCallCount { get; private set; }

    public Task ShareAsync(string title, string uri) => Task.CompletedTask;

    public Task ShareTextAsync(string title, string text)
    {
        LastTitle = title;
        LastText = text;
        ShareTextCallCount++;
        return Task.CompletedTask;
    }
}
