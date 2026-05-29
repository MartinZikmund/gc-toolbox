using Uno.Disposables;
using Windows.System.Display;

namespace GcToolkit.Services;

internal sealed class DisplayRequestManager : IDisplayRequestManager
{
    private int _activeRequestCount;
    private int _generation;
    private readonly DisplayRequest _displayRequest = new();

    public IDisposable RequestActive()
    {
        var currentGeneration = _generation;
        _activeRequestCount++;
        _displayRequest.RequestActive();

        return Disposable.Create(() =>
        {
            // Only release if Clear() hasn't been called in between
            if (_generation == currentGeneration)
            {
                _activeRequestCount--;
                _displayRequest.RequestRelease();
            }
        });
    }

    public void Clear()
    {
        while (_activeRequestCount > 0)
        {
            _displayRequest.RequestRelease();
            _activeRequestCount--;
        }

        _generation++;
    }
}
