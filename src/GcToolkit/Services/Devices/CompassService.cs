// Uno implements Compass on Android/iOS/WASM only. On Skia desktop, Compass.unsupported.cs declares
// GetDefault() => null and nothing else — no ReadingChanged, no ReportInterval — so the reading API
// has to compile out entirely there. GetDefault() itself exists on every head.
#if !HAS_UNO || __ANDROID__ || __IOS__ || __WASM__
#define HAS_COMPASS_READINGS
#endif

// Uno's iOS compass has no ReportInterval: CoreLocation drives the cadence.
#if !HAS_UNO || __ANDROID__ || __WASM__
#define HAS_COMPASS_REPORT_INTERVAL
#endif

// MinimumReportInterval is WinRT-only; no Uno head exposes it.
#if !HAS_UNO
#define HAS_COMPASS_MINIMUM_REPORT_INTERVAL
#endif

using System;
using System.Threading;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Services.Devices;
using Microsoft.UI.Dispatching;
using Windows.Devices.Sensors;
using SensorStatus = GcToolkit.Core.Services.Devices.SensorStatus;

namespace GcToolkit.Services.Devices;

/// <summary>
/// <see cref="ICompassService"/> over <see cref="Compass"/>. Presence (<see cref="IsSupported"/>) is
/// fixed for the session and gates catalog visibility; <see cref="Status"/> is live and gates in-tool UI.
/// </summary>
internal sealed class CompassService : ICompassService, IDisposable
{
    /// <summary>How long a started compass may stay silent before it is reported as <see cref="SensorStatus.NoData"/>.</summary>
    private static readonly TimeSpan _silenceBudget = TimeSpan.FromSeconds(3);

    // UiDispatcher.TryGetForCurrentThread() rather than DispatcherQueue.GetForCurrentThread():
    // the latter throws under a non-UI host, and this type is constructed from the window scope.
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();
    private readonly Compass? _compass = SafeGetDefault();

    private Timer? _watchdog;
    private SensorStatus _status = SensorStatus.Unavailable;
    private bool _started;

    public bool IsSupported => _compass is not null;

    public SensorStatus Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public TimeSpan ReportInterval { get; private set; }

    public event EventHandler<CompassHeading>? HeadingChanged;

    public event EventHandler? StatusChanged;

    public bool Start(TimeSpan reportInterval)
    {
        if (_compass is null)
        {
            Status = SensorStatus.Unavailable;
            return false;
        }

        ApplyReportInterval(_compass, reportInterval);

        if (!_started)
        {
            _started = SubscribeReadings(_compass);

            if (!_started)
            {
                // Compiled out on desktop; a null compass already returned above, so reaching here
                // means the head has no reading API at all.
                Status = SensorStatus.Unavailable;
                return false;
            }
        }

        // A second Start (a re-entered tool, a changed interval) must not knock a live compass back to
        // NoData and flicker the readout.
        if (Status != SensorStatus.Ready)
        {
            Status = SensorStatus.NoData;
        }

        RearmWatchdog();
        return true;
    }

    public void Stop()
    {
        _watchdog?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        if (_compass is null || !_started)
        {
            Status = SensorStatus.Unavailable;
            return;
        }

        UnsubscribeReadings(_compass);
        ReleaseReportInterval(_compass);
        _started = false;
        ReportInterval = TimeSpan.Zero;
        Status = SensorStatus.Unavailable;
    }

    /// <summary>Closing the window scope must release the sensor even if the tool never called <see cref="Stop"/>.</summary>
    public void Dispose()
    {
        Stop();
        _watchdog?.Dispose();
        _watchdog = null;
    }

    private static Compass? SafeGetDefault()
    {
        try
        {
            return Compass.GetDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Runs <paramref name="action"/> on the window's dispatcher; synchronously when there is none.</summary>
    private void RunOnUi(Action action)
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcher.TryEnqueue(() => action());
    }

    /// <summary>
    /// Re-armed by every reading, so it doubles as a stall detector: a Windows machine whose orientation
    /// sensor is synthesised but stale, or a WASM page whose browser exposes Magnetometer but denies the
    /// sensor permission, both start cleanly and then say nothing. Sized off the report interval so a
    /// deliberately slow caller is not mistaken for a dead sensor.
    /// </summary>
    private void RearmWatchdog()
    {
        var interval = ReportInterval;
        var stretched = interval > TimeSpan.Zero ? interval * 3 : TimeSpan.Zero;
        var budget = stretched > _silenceBudget ? stretched : _silenceBudget;

        // Local: readings arrive off the UI thread, so Dispose() can null the field between the two lines.
        var watchdog = _watchdog ??= new Timer(_ => OnSilenceElapsed(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        try
        {
            watchdog.Change(budget, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // The window closed while a reading was in flight.
        }
    }

    private void OnSilenceElapsed() => RunOnUi(() =>
    {
        if (_started)
        {
            Status = SensorStatus.NoData;
        }
    });

    private void OnHeading(CompassHeading heading)
    {
        RunOnUi(() =>
        {
            if (!_started)
            {
                return;
            }

            Status = SensorStatus.Ready;
            HeadingChanged?.Invoke(this, heading);
        });

        RearmWatchdog();
    }

#if HAS_COMPASS_READINGS
    private bool SubscribeReadings(Compass compass)
    {
        compass.ReadingChanged += OnReadingChanged;
        return true;
    }

    private void UnsubscribeReadings(Compass compass) => compass.ReadingChanged -= OnReadingChanged;

    private void OnReadingChanged(Compass sender, CompassReadingChangedEventArgs args)
    {
        var reading = args.Reading;

        if (reading is null)
        {
            return;
        }

        if (CompassHeading.TryCreate(reading.HeadingMagneticNorth, reading.HeadingTrueNorth, reading.Timestamp) is { } heading)
        {
            OnHeading(heading);
        }
    }
#else
    private bool SubscribeReadings(Compass compass) => false;

    private void UnsubscribeReadings(Compass compass)
    {
    }
#endif

#if HAS_COMPASS_REPORT_INTERVAL
    private void ApplyReportInterval(Compass compass, TimeSpan requested)
    {
        var milliseconds = (uint)Math.Clamp(Math.Round(requested.TotalMilliseconds), 0d, uint.MaxValue);

#if HAS_COMPASS_MINIMUM_REPORT_INTERVAL
        milliseconds = Math.Max(milliseconds, compass.MinimumReportInterval);
#endif

        try
        {
            compass.ReportInterval = milliseconds;
            ReportInterval = TimeSpan.FromMilliseconds(compass.ReportInterval);
        }
        catch (Exception)
        {
            // Some WinRT drivers throw on an out-of-range interval rather than clamping. Zero asks the
            // driver for its own default, which is always honoured.
            TryUseDriverDefaultInterval(compass);
        }
    }

    private void TryUseDriverDefaultInterval(Compass compass)
    {
        try
        {
            compass.ReportInterval = 0;
            ReportInterval = TimeSpan.FromMilliseconds(compass.ReportInterval);
        }
        catch (Exception)
        {
            ReportInterval = TimeSpan.Zero;
        }
    }

    /// <summary>Hands the sensor back to its driver default — WinRT keeps resources allocated otherwise.</summary>
    private void ReleaseReportInterval(Compass compass)
    {
        try
        {
            compass.ReportInterval = 0;
        }
        catch (Exception)
        {
            // Nothing to do: we are tearing down anyway.
        }
    }
#else
    private void ApplyReportInterval(Compass compass, TimeSpan requested)
    {
        // iOS reports at CoreLocation's own cadence. Echo the request so callers see what they asked for
        // rather than a misleading zero.
        ReportInterval = requested;
    }

    private void ReleaseReportInterval(Compass compass)
    {
    }
#endif
}
