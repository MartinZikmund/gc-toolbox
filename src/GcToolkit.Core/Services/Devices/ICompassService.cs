using System;

namespace GcToolkit.Core.Services.Devices;

/// <summary>
/// A startable compass. Scoped per window: it captures the window's dispatcher at construction and
/// raises <see cref="HeadingChanged"/> on it, so ViewModels bind without marshalling.
/// </summary>
public interface ICompassService
{
    /// <summary>Presence only; equivalent to <c>IDeviceCapabilityService.IsSupported(DeviceCapability.Compass)</c>.</summary>
    bool IsSupported { get; }

    SensorStatus Status { get; }

    /// <summary>The heading interval actually honoured, clamped to the platform minimum.</summary>
    TimeSpan ReportInterval { get; }

    event EventHandler<CompassHeading>? HeadingChanged;

    event EventHandler? StatusChanged;

    /// <summary>Starts reading. Returns false when the sensor is absent or refused; check
    /// <see cref="Status"/> for which. Idempotent — a second call re-applies the interval only.</summary>
    bool Start(TimeSpan reportInterval);

    /// <summary>Stops reading and releases the sensor. Safe to call when not started.</summary>
    void Stop();
}
