using System.Collections.Concurrent;
using GcToolkit.Core.Services.Devices;
using Windows.Devices.Sensors;

namespace GcToolkit.Services.Devices;

/// <summary>
/// Probes hardware presence lazily and caches it for the process. No conditional compilation is
/// needed: Uno ships <c>Compass.unsupported.cs</c> shims that return null off-platform, and WinRT
/// returns null when the hardware is absent, so one unguarded call answers every head.
/// </summary>
internal sealed class DeviceCapabilityService : IDeviceCapabilityService
{
    private readonly ConcurrentDictionary<DeviceCapability, bool> _cache = new();

    public bool IsSupported(DeviceCapability capability) => _cache.GetOrAdd(capability, Probe);

    private static bool Probe(DeviceCapability capability)
    {
        try
        {
            return capability switch
            {
                // Verified in Uno sources: null unconditionally on Skia desktop; on Android requires an
                // accelerometer AND a magnetometer; on iOS CLLocationManager.HeadingAvailable; on WASM
                // requires the browser Generic Sensor API. None of these prompt.
                DeviceCapability.Compass => Compass.GetDefault() is not null,
                _ => false,
            };
        }
        catch (Exception)
        {
            // Fail closed: a capability that cannot prove itself hides its tool rather than shipping a dead one.
            return false;
        }
    }
}
