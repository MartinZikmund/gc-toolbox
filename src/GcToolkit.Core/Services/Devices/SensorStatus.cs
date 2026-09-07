namespace GcToolkit.Core.Services.Devices;

/// <summary>
/// Why a live sensor is or is not producing data right now. Distinct from
/// <c>IDeviceCapabilityService.IsSupported</c>: presence decides visibility, status decides in-tool UI.
/// </summary>
public enum SensorStatus
{
    /// <summary>Not started, or this device has no such hardware.</summary>
    Unavailable,

    /// <summary>Hardware present, consent not granted or since revoked. Recoverable by the user.</summary>
    PermissionDenied,

    /// <summary>Present, permitted, and currently taken by something else (e.g. another app holds the lamp).</summary>
    Busy,

    /// <summary>Started and permitted, but no reading has arrived yet (cold sensor, stale driver, browser flag off).</summary>
    NoData,

    /// <summary>Producing readings.</summary>
    Ready,
}
