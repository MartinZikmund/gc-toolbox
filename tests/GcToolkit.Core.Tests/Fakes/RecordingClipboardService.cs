using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Captures the last text copied so command behaviour can be asserted.</summary>
public sealed class RecordingClipboardService : IClipboardService
{
    public string? LastText { get; private set; }

    public int SetTextCallCount { get; private set; }

    public void SetText(string text)
    {
        LastText = text;
        SetTextCallCount++;
    }
}
