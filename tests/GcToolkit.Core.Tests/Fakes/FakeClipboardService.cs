using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Records the last text copied so command behavior can be asserted without a real clipboard.</summary>
public sealed class FakeClipboardService : IClipboardService
{
    public string? LastText { get; private set; }

    public void SetText(string text) => LastText = text;
}
