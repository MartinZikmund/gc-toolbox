using GcToolkit.Core.Infrastructure;

namespace GcToolkit.Core.Tests.Infrastructure;

/// <summary>
/// View models that debounce or publish off-thread work are constructed by unit tests, where there is
/// no UI dispatcher. The Uno ref assemblies throw <see cref="NotSupportedException"/> ("Ref assembly")
/// from <c>DispatcherQueue.GetForCurrentThread()</c> rather than reporting its absence, so every such
/// call goes through <see cref="UiDispatcher"/> and degrades to synchronous mode instead of throwing.
/// </summary>
[TestClass]
public class UiDispatcherTests
{
    [TestMethod]
    public void TryGetForCurrentThread_WithoutUiThread_ReturnsNullInsteadOfThrowing()
    {
        Assert.IsNull(UiDispatcher.TryGetForCurrentThread());
    }

    [TestMethod]
    public void UiDebouncer_WithoutDispatcher_ConstructsAndRunsSynchronously()
    {
        UiDebouncer debouncer = new(TimeSpan.FromMilliseconds(200));

        var ran = 0;
        debouncer.Debounce(() => ran++);
        debouncer.RunNow(() => ran++);

        // No timer to defer to, so both invocations already happened.
        Assert.AreEqual(2, ran);
    }
}
