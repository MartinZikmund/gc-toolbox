namespace GcToolkit.Core.Services;

/// <summary>
/// Keeps the display awake while held. Ref-counted: each <see cref="RequestActive"/> returns a token that
/// releases its request on dispose. Implemented per platform in the app head.
/// </summary>
public interface IDisplayRequestManager
{
    IDisposable RequestActive();

    void Clear();
}
