using System.Threading.Tasks;
using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Records share calls so VM share commands can be asserted without a platform share sheet.</summary>
public sealed class FakeShareService : IShareService
{
    public int ShareTextCount { get; private set; }

    public string? LastTitle { get; private set; }

    public string? LastText { get; private set; }

    public Task ShareAsync(string title, string uri) => Task.CompletedTask;

    public Task ShareTextAsync(string title, string text)
    {
        ShareTextCount++;
        LastTitle = title;
        LastText = text;
        return Task.CompletedTask;
    }
}
