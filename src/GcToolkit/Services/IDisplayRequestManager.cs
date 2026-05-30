namespace GcToolkit.Services;

public interface IDisplayRequestManager
{
    IDisposable RequestActive();

    void Clear();
}
