using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Captures the last text written to the clipboard so VM copy commands can be asserted.</summary>
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
