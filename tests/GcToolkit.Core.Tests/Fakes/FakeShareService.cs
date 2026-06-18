using System.Threading.Tasks;
using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Records the last shared payload; never touches a real share sheet.</summary>
public sealed class FakeShareService : IShareService
{
    public (string Title, string Text)? LastShared { get; private set; }

    public Task ShareAsync(string title, string uri) => Task.CompletedTask;

    public Task ShareTextAsync(string title, string text)
    {
        LastShared = (title, text);
        return Task.CompletedTask;
    }
}
