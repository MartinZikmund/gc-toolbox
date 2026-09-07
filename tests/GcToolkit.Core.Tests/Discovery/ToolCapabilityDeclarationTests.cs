using System.Reflection;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.Services.Devices;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Discovery;

/// <summary>
/// The trimming fence. Gating is driven by a runtime attribute lookup, which fails OPEN if the
/// attribute instance is ever stripped — the compass would then appear on every head. These tests
/// break loudly if the declaration is removed or if attribute trimming is ever enabled.
/// </summary>
[TestClass]
public class ToolCapabilityDeclarationTests
{
    [TestMethod]
    public void CompassViewModel_DeclaresCompassRequirement()
    {
        var attribute = typeof(CompassViewModel)
            .GetCustomAttribute<RequiresDeviceCapabilityAttribute>(inherit: false);

        Assert.IsNotNull(attribute);
        CollectionAssert.AreEqual(new[] { DeviceCapability.Compass }, attribute.Capabilities.ToList());
    }

    [TestMethod]
    public void FlashlightViewModel_DeclaresNoRequirement()
    {
        // A full-brightness white screen is a real flashlight on every head, torch or not.
        var attribute = typeof(FlashlightViewModel)
            .GetCustomAttribute<RequiresDeviceCapabilityAttribute>(inherit: false);

        Assert.IsNull(attribute);
    }
}
