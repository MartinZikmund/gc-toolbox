using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Devices;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>The lamp colours. Red is the one that matters: it preserves night vision in the field.</summary>
public enum FlashlightColour
{
    White,
    Red,
    Green,
}

/// <summary>
/// Two lamps behind one tool: the camera LED where the device has one, and a full-brightness screen
/// everywhere else — which is a legitimate torch on every head, so the tool is never hidden and carries
/// no <c>[RequiresDeviceCapability]</c>. The torch is an optional feature *inside* the tool: absent, its
/// row is collapsed rather than disabled, so a desktop user never sees a switch they can never enable.
/// </summary>
[Tool("Flashlight", ToolCategory.Field,
      Introduced = "2026-02-10", Updated = "2026-09-03",
      Keywords = ["flashlight", "torch", "light", "lamp", "sos", "baterka", "svítilna", "světlo", "noc"])]
public sealed partial class FlashlightViewModel : ToolViewModelBase
{
    /// <summary>SOS at 6 WPM is exactly the 200 ms dit / 600 ms dah / 1400 ms word gap the tool wants,
    /// derived from <see cref="MorseTiming"/> so this blink and the Morse tool's beep stay in agreement.</summary>
    private const int SosWordsPerMinute = 6;

    private const string SosMorse = "... --- ...";

    /// <summary>The word gap <see cref="MorseTimeline"/> deliberately omits at the end of a message.</summary>
    private const int RepeatGapUnits = 7;

    private readonly ITorchService _torch;
    private readonly IDisplayRequestManager _display;

    private IDisposable? _wakeLock;
    private CancellationTokenSource? _blinkCts;
    private bool _torchFollowsBlink;
    private bool _syncingTorch;
    private volatile bool _isUnloaded;
    private bool _isSubscribed;

    public FlashlightViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        ITorchService torch,
        IDisplayRequestManager display)
        : base("Flashlight", catalog, recents, favorites, localizer)
    {
        _torch = torch;
        _display = display;

        // A lamp is granted to the most recent requester, so it really does get taken from under us.
        UpdateTorchState();
    }

    public double MinimumBrightness => 0.1;

    public double MaximumBrightness => 1.0;

    /// <summary>The screen light. The lit surface ignores the app theme — white is white in dark mode.</summary>
    [ObservableProperty]
    public partial bool IsLit { get; set; }

    /// <summary>Answered by the lazy probe on first load; drives Visibility, never IsEnabled.</summary>
    [ObservableProperty]
    public partial bool IsTorchSupported { get; set; }

    /// <summary>Two-way: the user writes it (which drives the lamp) and the service writes it back
    /// (a lamp stolen by another app), so VM-originated writes go through <see cref="SetTorchOn"/>
    /// to avoid a status echo re-commanding the hardware.</summary>
    [ObservableProperty]
    public partial bool IsTorchOn { get; set; }

    [ObservableProperty]
    public partial SensorStatus TorchStatus { get; set; }

    [ObservableProperty]
    public partial bool SupportsBrightness { get; set; }

    [ObservableProperty]
    public partial bool IsTorchBusy { get; set; }

    [ObservableProperty]
    public partial bool IsTorchPermissionDenied { get; set; }

    /// <summary>0.1–1.0. Always dims the screen surface; reaches the LED only where the hardware allows it.</summary>
    [ObservableProperty]
    public partial double Brightness { get; set; } = 1.0;

    /// <summary>A ticking figure — rendered in the mono face so the row does not jitter as the thumb moves.</summary>
    [ObservableProperty]
    public partial string BrightnessText { get; set; } = FormatPercent(1.0);

    [ObservableProperty]
    public partial FlashlightColour Colour { get; set; }

    /// <summary>Swatch selection, kept in step with <see cref="Colour"/>.</summary>
    [ObservableProperty]
    public partial int ColourIndex { get; set; }

    [ObservableProperty]
    public partial bool IsColourWhite { get; set; } = true;

    [ObservableProperty]
    public partial bool IsColourRed { get; set; }

    [ObservableProperty]
    public partial bool IsColourGreen { get; set; }

    [ObservableProperty]
    public partial bool IsSosBlinking { get; set; }

    /// <summary>The current blink phase; only meaningful while <see cref="IsSosBlinking"/>.</summary>
    [ObservableProperty]
    public partial bool IsBlinkOn { get; set; }

    /// <summary>What the coloured surface actually shows: lit, minus the off phases of an SOS.</summary>
    [ObservableProperty]
    public partial bool IsSurfaceLit { get; set; }

    public override void ViewLoaded()
    {
        base.ViewLoaded();
        _isUnloaded = false;

        // Subscribe here, not in the constructor: ViewUnloaded unsubscribes, so a constructor-time
        // subscription leaves a re-shown page deaf to torch status.
        if (!_isSubscribed)
        {
            _torch.StatusChanged += OnTorchStatusChanged;
            _isSubscribed = true;
        }

        // The one and only place the lamp is probed — Uno's Android probe enumerates cameras.
        _ = ProbeTorchAsync();
    }

    public override void ViewUnloaded()
    {
        base.ViewUnloaded();

        // Set first: the blink loop finishes on a pool thread and must not re-take a display request
        // (or the lamp) as this method tears them down.
        _isUnloaded = true;
        _torchFollowsBlink = false;
        StopBlink();
        if (_isSubscribed)
        {
            _torch.StatusChanged -= OnTorchStatusChanged;
            _isSubscribed = false;
        }

        // Mandatory: Uno's iOS/Android Lamp holds unmanaged resources.
        _torch.Release();
        SetTorchOn(false);
        IsLit = false;

        // Belt and braces — a leaked display request keeps the user's screen on forever.
        _wakeLock?.Dispose();
        _wakeLock = null;
    }

    [RelayCommand]
    private void ToggleLight() => IsLit = !IsLit;

    /// <summary>Sync, like <see cref="ToggleLight"/>: the lamp call is driven by the property change so
    /// the switch and the command share one code path, and no async command can disable its own button.</summary>
    [RelayCommand(CanExecute = nameof(IsTorchSupported))]
    private void ToggleTorch() => IsTorchOn = !IsTorchOn;

    /// <summary>Deliberately not an async command: an <c>AsyncRelayCommand</c> reports CanExecute=false
    /// for the whole blink, leaving no way to press the same button to stop it.</summary>
    [RelayCommand]
    private void ToggleSos()
    {
        if (IsSosBlinking)
        {
            StopBlink();
        }
        else
        {
            StartBlink();
        }
    }

    partial void OnIsLitChanged(bool value)
    {
        if (!value)
        {
            StopBlink();
        }

        UpdateSurface();
        UpdateWakeLock();
    }

    partial void OnIsSosBlinkingChanged(bool value) => UpdateSurface();

    partial void OnIsTorchOnChanged(bool value)
    {
        // A write from the service is only a report; a write from the user is an order.
        if (_syncingTorch)
        {
            UpdateWakeLock();
            return;
        }

        _ = ApplyTorchAsync(value);
    }

    partial void OnIsBlinkOnChanged(bool value) => UpdateSurface();

    partial void OnIsTorchSupportedChanged(bool value)
    {
        ToggleTorchCommand.NotifyCanExecuteChanged();
        UpdateTorchState();
    }

    partial void OnBrightnessChanged(double value)
    {
        // Math.Clamp passes NaN straight through, and NaN != NaN would then recurse forever.
        var clamped = double.IsNaN(value) ? MaximumBrightness : Math.Clamp(value, MinimumBrightness, MaximumBrightness);
        if (clamped != value)
        {
            Brightness = clamped;
            return;
        }

        BrightnessText = FormatPercent(value);

        if (IsTorchOn && SupportsBrightness)
        {
            _ = ApplyTorchBrightnessAsync();
        }
    }

    partial void OnColourChanged(FlashlightColour value)
    {
        IsColourWhite = value == FlashlightColour.White;
        IsColourRed = value == FlashlightColour.Red;
        IsColourGreen = value == FlashlightColour.Green;
        ColourIndex = (int)value;
    }

    partial void OnColourIndexChanged(int value)
    {
        // A RadioButtons control emits a transient -1 while its items are realised.
        if (value is < 0 or > (int)FlashlightColour.Green)
        {
            return;
        }

        Colour = (FlashlightColour)value;
    }

    private static string FormatPercent(double value) => value.ToString("P0", CultureInfo.CurrentCulture);

    private async Task ProbeTorchAsync()
    {
        IsTorchSupported = await _torch.GetIsSupportedAsync();
        UpdateTorchState();
    }

    /// <summary>The first press is where Android asks for the CAMERA permission — never at startup.</summary>
    private async Task ApplyTorchAsync(bool desired)
    {
        try
        {
            await _torch.SetEnabledAsync(desired, Brightness);
        }
        catch (Exception)
        {
            // A refusal is reported through Status, not an exception; anything else is still not fatal.
        }

        // The lamp, not the switch, is the truth: a refused turn-on snaps the switch back off.
        SetTorchOn(_torch.IsOn);
        UpdateTorchState();
        UpdateWakeLock();
    }

    private async Task ApplyTorchBrightnessAsync()
    {
        await _torch.SetEnabledAsync(true, Brightness);
        SetTorchOn(_torch.IsOn);
        UpdateTorchState();
    }

    private void OnTorchStatusChanged(object? sender, EventArgs e)
    {
        // While the blink owns the lamp, IsOn flips every dit; mirroring it would erase the user's
        // torch setting and leave the lamp off once the SOS stops.
        if (!_torchFollowsBlink)
        {
            SetTorchOn(_torch.IsOn);
        }

        UpdateTorchState();
        UpdateWakeLock();
    }

    /// <summary>Writes the switch from the service's answer without re-commanding the lamp.</summary>
    private void SetTorchOn(bool value)
    {
        _syncingTorch = true;
        try
        {
            IsTorchOn = value;
        }
        finally
        {
            _syncingTorch = false;
        }
    }

    private void UpdateTorchState()
    {
        TorchStatus = _torch.Status;
        SupportsBrightness = _torch.SupportsBrightness;

        // Only ever surfaced on a device that has a lamp; elsewhere the whole row is collapsed.
        IsTorchBusy = IsTorchSupported && TorchStatus == SensorStatus.Busy;
        IsTorchPermissionDenied = IsTorchSupported && TorchStatus == SensorStatus.PermissionDenied;
    }

    private void UpdateSurface() => IsSurfaceLit = IsLit && (!IsSosBlinking || IsBlinkOn);

    /// <summary>One request at most, held exactly while the tool is producing light.</summary>
    private void UpdateWakeLock()
    {
        var wanted = !_isUnloaded && (IsLit || IsSosBlinking || IsTorchOn);
        if (wanted && _wakeLock is null)
        {
            _wakeLock = _display.RequestActive();
        }
        else if (!wanted && _wakeLock is not null)
        {
            _wakeLock.Dispose();
            _wakeLock = null;
        }
    }

    private void StartBlink()
    {
        StopBlink();

        CancellationTokenSource cts = new();
        _blinkCts = cts;
        _torchFollowsBlink = IsTorchSupported && IsTorchOn;

        IsSosBlinking = true;
        IsLit = true;
        UpdateWakeLock();

        _ = RunBlinkAsync(cts);
    }

    private void StopBlink()
    {
        IsSosBlinking = false;

        var cts = _blinkCts;
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already finished.
        }
    }

    private async Task RunBlinkAsync(CancellationTokenSource cts)
    {
        var timeline = MorseTimeline.Build(SosMorse);
        var unitMs = MorseTiming.UnitMilliseconds(SosWordsPerMinute);
        var token = cts.Token;

        // Monotonic baseline, as in MorseCodeViewModel: Task.Delay overshoot must not accumulate over a
        // signal that may run for many minutes.
        var stopwatch = Stopwatch.StartNew();
        long targetMs = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var signal in timeline)
                {
                    await EmitAsync(signal.On, signal.Units);
                }

                await EmitAsync(false, RepeatGapUnits);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped by the user, or the view went away — expected.
        }
        catch (Exception)
        {
            // A lamp glitch mid-pattern must not tear the tool down.
        }
        finally
        {
            IsBlinkOn = false;
            IsSosBlinking = false;
            UpdateSurface();
            UpdateWakeLock();
            await RestoreTorchAsync();

            if (ReferenceEquals(_blinkCts, cts))
            {
                _blinkCts = null;
            }

            cts.Dispose();
        }

        async Task EmitAsync(bool on, int units)
        {
            token.ThrowIfCancellationRequested();
            IsBlinkOn = on;

            if (_torchFollowsBlink)
            {
                await _torch.SetEnabledAsync(on, Brightness, token);
            }

            targetMs += (long)units * unitMs;
            var remaining = targetMs - stopwatch.ElapsedMilliseconds;
            if (remaining > 0)
            {
                await Task.Delay((int)remaining, token);
            }
        }
    }

    private async Task RestoreTorchAsync()
    {
        if (!_torchFollowsBlink)
        {
            return;
        }

        _torchFollowsBlink = false;

        try
        {
            await _torch.SetEnabledAsync(IsTorchOn, Brightness);
        }
        catch (Exception)
        {
            // Best effort; the status refresh below reports whatever actually happened.
        }

        SetTorchOn(_torch.IsOn);
        UpdateTorchState();
        UpdateWakeLock();
    }
}
