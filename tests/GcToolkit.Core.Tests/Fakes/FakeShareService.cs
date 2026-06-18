using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Records the last shared text/title for ViewModel tests.</summary>
public sealed class FakeShareService : IShareService
{
    public string? LastText { get; private set; }

    public Task ShareAsync(string title, string uri) => Task.CompletedTask;

    public Task ShareTextAsync(string title, string text)
    {
        LastText = text;
        return Task.CompletedTask;
    }
}
