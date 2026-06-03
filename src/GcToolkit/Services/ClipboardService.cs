using GcToolkit.Core.Services;
using Windows.ApplicationModel.DataTransfer;

namespace GcToolkit.Services;

/// <summary>Copies text to the system clipboard via the WinRT data-transfer API (portable across heads).</summary>
public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(text ?? string.Empty);
            Clipboard.SetContent(package);
#if !HAS_UNO
            // Persist the content so it survives after the app closes (Windows only).
            Clipboard.Flush();
#endif
        }
        catch (Exception)
        {
            // Clipboard contention (another process holding it) is transient and must not crash the tool.
        }
    }
}
