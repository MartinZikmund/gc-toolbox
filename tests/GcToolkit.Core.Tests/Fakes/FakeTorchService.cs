using GcToolkit.Core.Services.Devices;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>
/// A torch with no lamp behind it. <see cref="ProbeCount"/> lets a test assert the lazy-probe contract
/// (the lamp is touched on the tool's first ask, never at startup), and <see cref="FailureStatus"/>
/// simulates the lamp being taken by another app.
/// </summary>
public sealed class FakeTorchService : ITorchService
{
    private SensorStatus _status = SensorStatus.Unavailable;

    /// <summary>What the probe reports. False models a head or device with no lamp at all.</summary>
    public bool IsLampPresent { get; set; } = true;

    /// <summary>When set, <see cref="SetEnabledAsync"/> refuses and adopts this status.</summary>
    public SensorStatus? FailureStatus { get; set; }

    public bool SupportsBrightness { get; set; }

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

    public double LastBrightness { get; private set; } = 1.0;

    public int ProbeCount { get; private set; }

    public int ReleaseCallCount { get; private set; }

    public event EventHandler? StatusChanged;

    public ValueTask<bool> GetIsSupportedAsync(CancellationToken cancellationToken = default)
    {
        ProbeCount++;
        Status = IsLampPresent ? SensorStatus.Ready : SensorStatus.Unavailable;
        return new ValueTask<bool>(IsLampPresent);
    }

    public async Task<bool> SetEnabledAsync(bool isOn, double brightness = 1.0, CancellationToken cancellationToken = default)
    {
        if (!await GetIsSupportedAsync(cancellationToken))
        {
            return false;
        }

        if (FailureStatus is { } failure)
        {
            Status = failure;
            return false;
        }

        LastBrightness = Math.Clamp(brightness, 0d, 1d);
        IsOn = isOn && LastBrightness > 0d;
        Status = SensorStatus.Ready;
        return true;
    }

    public void Release()
    {
        ReleaseCallCount++;
        IsOn = false;
        Status = SensorStatus.Unavailable;
    }
}
