using Microsoft.UI.Dispatching;

namespace GcToolkit.Core.Infrastructure;

/// <summary>
/// Safe access to the current thread's <see cref="DispatcherQueue"/> for view models that publish
/// off-thread work back to the UI.
/// </summary>
public static class UiDispatcher
{
    /// <summary>
    /// The current thread's dispatcher, or <see langword="null"/> when there is none — including under
    /// the unit-test host, where the Uno ref assemblies throw <see cref="NotSupportedException"/>
    /// ("Ref assembly") instead of reporting its absence. Callers treat null as "run synchronously".
    /// </summary>
    public static DispatcherQueue? TryGetForCurrentThread()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
