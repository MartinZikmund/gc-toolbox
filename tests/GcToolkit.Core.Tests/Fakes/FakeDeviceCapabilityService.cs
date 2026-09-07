using GcToolkit.Core.Services.Devices;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>
/// Reports exactly the capabilities it was constructed with, so a test can pretend to be a
/// phone with a compass or a desktop with none.
/// </summary>
internal sealed class FakeDeviceCapabilityService(params DeviceCapability[] supported) : IDeviceCapabilityService
{
    public bool IsSupported(DeviceCapability capability) => supported.Contains(capability);
}
