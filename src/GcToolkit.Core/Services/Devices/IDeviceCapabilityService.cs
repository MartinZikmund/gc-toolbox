namespace GcToolkit.Core.Services.Devices;

/// <summary>
/// Answers whether this device physically has a capability. Presence only — it says nothing about
/// permission or about whether the sensor is producing data right now (see <c>SensorStatus</c>).
/// Deliberately synchronous: presence gates catalog visibility, which is decided during
/// <c>CatalogService</c> construction, so it can never be allowed to await.
/// Implementations probe once per capability and cache the answer for the process lifetime.
/// </summary>
public interface IDeviceCapabilityService
{
    bool IsSupported(DeviceCapability capability);
}
