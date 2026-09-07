using GcToolkit.Core.Services.Devices;

namespace GcToolkit.Core.Discovery;

/// <summary>
/// Declares hardware a tool cannot work without; a device lacking any of it hides the tool from the
/// catalog, the navigation pane, search, favorites and recents. Read at runtime off
/// <c>ToolDescriptor.ViewModelType</c> — deliberately NOT part of <see cref="ToolAttribute"/>, so the
/// source generator and every generated file stay untouched. No attribute means available everywhere.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RequiresDeviceCapabilityAttribute(params DeviceCapability[] capabilities) : Attribute
{
    public IReadOnlyList<DeviceCapability> Capabilities { get; } = capabilities;
}
