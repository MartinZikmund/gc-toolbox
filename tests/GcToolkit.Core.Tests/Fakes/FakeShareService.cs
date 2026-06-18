using System.Threading.Tasks;
using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Records the last text/title shared so tests can assert a ViewModel's share payload.</summary>
public sealed class FakeShareService : IShareService
{
    public string? LastTitle { get; private set; }

    public string? LastText { get; private set; }

    public Task ShareAsync(string title, string uri)
    {
        LastTitle = title;
        LastText = uri;
        return Task.CompletedTask;
    }

    public Task ShareTextAsync(string title, string text)
    {
        LastTitle = title;
        LastText = text;
        return Task.CompletedTask;
    }
}
