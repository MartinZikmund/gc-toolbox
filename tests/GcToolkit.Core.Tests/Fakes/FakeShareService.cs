using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Records the last text/uri shared so tool ViewModels' share commands can be asserted.</summary>
public sealed class FakeShareService : IShareService
{
    public string? LastSharedText { get; private set; }

    public string? LastSharedUri { get; private set; }

    public Task ShareAsync(string title, string uri)
    {
        LastSharedUri = uri;
        return Task.CompletedTask;
    }

    public Task ShareTextAsync(string title, string text)
    {
        LastSharedText = text;
        return Task.CompletedTask;
    }
}
