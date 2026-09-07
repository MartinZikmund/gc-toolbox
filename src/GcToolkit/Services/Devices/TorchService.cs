// Uno implements Lamp on Android/iOS only. Lamp.unsupported.cs (Skia desktop, WASM) declares
// GetDefaultAsync() => null and nothing else — no IsEnabled, no BrightnessLevel, not even IDisposable —
// so every instance member has to compile out there. GetDefaultAsync() itself exists on every head.
#if !HAS_UNO || __ANDROID__ || __IOS__
#define HAS_LAMP_CONTROL
#endif

// AvailabilityChanged/IsAvailable are WinRT-only; no Uno head raises them.
#if !HAS_UNO
#define HAS_LAMP_AVAILABILITY
#endif

using System;
using System.Threading;
using System.Threading.Tasks;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Services.Devices;
using Microsoft.UI.Dispatching;
using Windows.Devices.Lights;
using SensorStatus = GcToolkit.Core.Services.Devices.SensorStatus;

namespace GcToolkit.Services.Devices;

/// <summary>
/// <see cref="ITorchService"/> over <see cref="Lamp"/>. Never a visibility gate — the Flashlight tool
/// ships everywhere and falls back to a full-brightness screen — so the lamp is probed only when the
/// tool first asks for it.
/// </summary>
internal sealed class TorchService : ITorchService, IDisposable
{
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    private Lamp? _lamp;
    private Task<bool>? _probe;
    private SensorStatus _status = SensorStatus.Unavailable;

    /// <summary>Bumped by <see cref="Release"/> so a probe still in flight knows its result is stale.</summary>
    private int _generation;

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

    public bool IsOn { get; private set; }

#if __IOS__
    /// <remarks>
    /// Uno's iOS lamp dims through <c>SetTorchModeLevel</c>. It does not surface <c>HasTorch</c>, and a
    /// flash-only device silently treats any non-zero level as full on, so the slider degrades to an
    /// on/off switch there instead of misreporting.
    /// </remarks>
    public bool SupportsBrightness => true;
#else
    /// <remarks>
    /// Android treats any non-zero <c>BrightnessLevel</c> as full; WinRT exposes the property but
    /// virtually no PC has a lamp to honour it; desktop and WASM have no lamp at all.
    /// </remarks>
    public bool SupportsBrightness => false;
#endif

    public event EventHandler? StatusChanged;

    public ValueTask<bool> GetIsSupportedAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(cancellationToken);
        }

        // One probe per service, shared by concurrent callers. Lamp.GetDefaultAsync() takes no
        // cancellation token, so the caller's token only gates entry.
        _probe ??= AcquireLampAsync();
        return new ValueTask<bool>(_probe);
    }

    public async Task<bool> SetEnabledAsync(bool isOn, double brightness = 1.0, CancellationToken cancellationToken = default)
    {
        if (!await GetIsSupportedAsync(cancellationToken).ConfigureAwait(true) || _lamp is null)
        {
            return false;
        }

        var level = Math.Clamp(brightness, 0d, 1d);

        try
        {
            ApplyLampState(_lamp, isOn, level);
        }
        catch (UnauthorizedAccessException)
        {
            Status = SensorStatus.PermissionDenied;
            return false;
        }
        catch (Exception)
        {
            // Uno's iOS lamp throws when LockForConfiguration fails and Android when the camera is held
            // elsewhere: the lamp exists and we may ask for it, someone else simply has it right now.
            Status = SensorStatus.Busy;
            return false;
        }

        IsOn = isOn && level > 0d;
        Status = SensorStatus.Ready;
        return true;
    }

    public void Release()
    {
        // A probe may still be running: retire this generation so its lamp is handed straight back
        // instead of publishing Ready (and an AvailabilityChanged subscription) after the tool closed.
        _generation++;
        _probe = null;

        if (_lamp is null)
        {
            return;
        }

        UnsubscribeAvailability(_lamp);

        try
        {
            ApplyLampState(_lamp, isOn: false, brightness: 0d);
        }
        catch (Exception)
        {
            // Tearing down: a lamp we can no longer talk to is already off as far as we are concerned.
        }

        DisposeLamp(_lamp);
        _lamp = null;
        _probe = null;
        IsOn = false;
        Status = SensorStatus.Unavailable;
    }

    /// <summary>Closing the window scope must return the lamp even if the tool never called <see cref="Release"/>.</summary>
    public void Dispose() => Release();

    private async Task<bool> AcquireLampAsync()
    {
        // Published to _lamp only once the probe is known to still be current; a Release mid-probe
        // must not leave a lit, subscribed lamp behind with nobody left to return it.
        var generation = _generation;
        Lamp? lamp;

        try
        {
            lamp = await Lamp.GetDefaultAsync();
        }
        catch (Exception ex) when (IsPermissionRefusal(ex))
        {
            // Below API 23 Uno's probe falls through to Android.Hardware.Camera.Open(), which throws
            // without the CAMERA runtime permission. Only that shape is a refusal.
            SetStatusIfCurrent(generation, SensorStatus.PermissionDenied);
            return false;
        }
        catch (Exception)
        {
            // Anything else means we could not get a lamp, which the user cannot act on — telling
            // them to grant a permission they were never asked for is worse than saying it is absent.
            SetStatusIfCurrent(generation, SensorStatus.Unavailable);
            return false;
        }

        if (lamp is null)
        {
            SetStatusIfCurrent(generation, SensorStatus.Unavailable);
            return false;
        }

        if (generation != _generation)
        {
            DisposeLamp(lamp);
            return false;
        }

        _lamp = lamp;
        SubscribeAvailability(lamp);
        Status = SensorStatus.Ready;
        return true;
    }

    private void SetStatusIfCurrent(int generation, SensorStatus status)
    {
        if (generation == _generation)
        {
            Status = status;
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

#if HAS_LAMP_CONTROL
    private static void ApplyLampState(Lamp lamp, bool isOn, double brightness)
    {
        // Turning off touches IsEnabled only: writing BrightnessLevel first would re-apply a lit state
        // for one frame on the heads whose setter pushes straight to the hardware.
        if (!isOn)
        {
            lamp.IsEnabled = false;
            return;
        }

        lamp.BrightnessLevel = (float)brightness;
        lamp.IsEnabled = true;
    }
#else
    private static void ApplyLampState(Lamp lamp, bool isOn, double brightness)
    {
        // Unreachable: GetDefaultAsync() is hard-coded to null on these heads, so _lamp is never set.
    }
#endif

#if HAS_LAMP_AVAILABILITY
    private void SubscribeAvailability(Lamp lamp) => lamp.AvailabilityChanged += OnAvailabilityChanged;

    private void UnsubscribeAvailability(Lamp lamp) => lamp.AvailabilityChanged -= OnAvailabilityChanged;

    private void OnAvailabilityChanged(Lamp sender, LampAvailabilityChangedEventArgs args) => RunOnUi(() =>
    {
        if (!args.IsAvailable)
        {
            IsOn = false;
        }

        Status = args.IsAvailable ? SensorStatus.Ready : SensorStatus.Busy;
    });
#else
    private void SubscribeAvailability(Lamp lamp)
    {
        // Uno's Lamp has no AvailabilityChanged. A lamp stolen by the camera app is invisible to us
        // until the next SetEnabledAsync throws and reports Busy.
    }

    private void UnsubscribeAvailability(Lamp lamp)
    {
    }
#endif

#if !HAS_UNO
    private static void DisposeLamp(Lamp lamp) => lamp.Dispose();
#else
    private static void DisposeLamp(Lamp lamp)
    {
        // Deliberately NOT disposed on the Uno heads. Uno caches the Lamp in a static field behind a
        // one-shot _initializationAttempted flag, so a disposed instance is handed back to every later
        // caller — iOS then NREs on a null AVCaptureDevice and the torch stays dead until the app is
        // restarted. Turning it off above is the whole of our cleanup; the shared instance lives on.
    }
#endif

    /// <summary>
    /// Only a genuine refusal counts as PermissionDenied. Android's pre-API-23 lamp probe opens the
    /// camera directly, which throws when CAMERA was never granted; every other failure means the
    /// lamp is simply not obtainable, and prompting for a permission is then misleading advice.
    /// </summary>
    private static bool IsPermissionRefusal(Exception ex)
        => ex is UnauthorizedAccessException
            || ex is System.Security.SecurityException
            || ex.GetType().Name.Contains("CameraAccess", StringComparison.Ordinal);

}
