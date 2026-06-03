namespace GcToolkit.Core.Services;

/// <summary>Copies text to the system clipboard. Implemented per platform in the app head.</summary>
public interface IClipboardService
{
    void SetText(string text);
}
