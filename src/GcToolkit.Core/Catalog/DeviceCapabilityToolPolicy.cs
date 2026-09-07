using System.Collections.Concurrent;
using System.Reflection;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.Services.Devices;

namespace GcToolkit.Core.Catalog;

/// <summary>
/// Reads each tool's declared hardware requirements off its ViewModel TYPE. It never constructs a
/// ViewModel: tool VMs are transient and their base ctor takes <see cref="ICatalogService"/>, so asking
/// an instance from inside the catalog would be a DI cycle and would fire
/// <c>IRecentsService.RecordOpenedAsync</c> for every tool in the gallery.
/// </summary>
public sealed class DeviceCapabilityToolPolicy(IDeviceCapabilityService capabilities) : IToolAvailabilityPolicy
{
    private static readonly DeviceCapability[] NoRequirements = [];

    // Concurrent: this is a singleton, and each window builds its own scoped CatalogService on its own UI thread.
    private readonly ConcurrentDictionary<Type, DeviceCapability[]> _requirements = new();

    public bool IsAvailable(ToolDescriptor tool)
    {
        foreach (var capability in RequirementsOf(tool.ViewModelType))
        {
            if (!capabilities.IsSupported(capability))
            {
                return false;
            }
        }

        return true;
    }

    private DeviceCapability[] RequirementsOf(Type viewModelType)
        => _requirements.GetOrAdd(
            viewModelType,
            static type => type.GetCustomAttribute<RequiresDeviceCapabilityAttribute>(inherit: false) is { } attribute
                ? [.. attribute.Capabilities]
                : NoRequirements);
}
