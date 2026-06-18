using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Records the last text copied so tool VM tests can assert the Copy command's payload.</summary>
public sealed class FakeClipboardService : IClipboardService
{
    public string? LastText { get; private set; }

    public int SetTextCallCount { get; private set; }

    public void SetText(string text)
    {
        LastText = text;
        SetTextCallCount++;
    }
}
