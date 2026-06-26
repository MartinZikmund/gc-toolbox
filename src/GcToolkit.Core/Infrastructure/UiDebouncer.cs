using System;
using Microsoft.UI.Dispatching;

namespace GcToolkit.Core.Infrastructure;

/// <summary>
/// Coalesces rapid callbacks into a single UI-thread invocation after a quiet interval — so a tool
/// that runs an expensive recompute (e.g. a brute-force candidate list) doesn't rebuild on every
/// keystroke and freeze the UI while the user types fast.
/// </summary>
/// <remarks>
/// Backed by a non-repeating <see cref="DispatcherQueueTimer"/> captured on the current (UI) thread,
/// so the debounced action always runs on the UI thread (safe to touch bindings/observable
/// collections). When constructed off a dispatcher thread — i.e. in unit tests — there is no timer
/// and both <see cref="RunNow"/> and <see cref="Debounce"/> invoke synchronously, keeping view
/// models testable without a UI head.
/// </remarks>
public sealed class UiDebouncer
{
    private readonly DispatcherQueueTimer? _timer;
    private Action? _pending;

    public UiDebouncer(TimeSpan interval)
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        if (dispatcher is null)
        {
            return;
        }

        _timer = dispatcher.CreateTimer();
        _timer.Interval = interval;
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => _pending?.Invoke();
    }

    /// <summary>Cancels any pending debounce and runs <paramref name="action"/> immediately.</summary>
    public void RunNow(Action action)
    {
        _timer?.Stop();
        _pending = null;
        action();
    }

    /// <summary>
    /// Schedules <paramref name="action"/> to run once the interval elapses with no further calls,
    /// restarting the window on each call. Runs synchronously when there is no dispatcher (tests).
    /// </summary>
    public void Debounce(Action action)
    {
        if (_timer is null)
        {
            action();
            return;
        }

        _pending = action;
        _timer.Stop();
        _timer.Start();
    }
}
