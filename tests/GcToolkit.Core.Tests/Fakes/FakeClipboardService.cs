using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Records the last text set on the clipboard so tool tests can assert on copy actions.</summary>
public sealed class FakeClipboardService : IClipboardService
{
    public string? LastText { get; private set; }

    public void SetText(string text) => LastText = text;
}
