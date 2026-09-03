using System.Collections;
using System.Reflection;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.Search;
using GcToolkit.Core.Services.Devices;
using GcToolkit.Core.Tests.Fakes;

namespace GcToolkit.Core.Tests.Catalog;

/// <summary>
/// Device gating: a tool whose declared hardware is missing disappears from the catalog, the
/// navigation tree and search, while still resolving by id so restored selections and deep links
/// keep working. Validation must stay pre-filter, or a bad tool id would only fail on the one
/// platform that can see it.
/// </summary>
[TestClass]
public class ToolAvailabilityTests
{
    private static readonly Category Field = new("Field", "Category_Field", 0, "Field");
    private static readonly Category Ciphers = new("Ciphers", "Category_Ciphers", 1, "Ciphers");

    /// <summary>Stands in for a tool VM that cannot work without a magnetometer.</summary>
    [RequiresDeviceCapability(DeviceCapability.Compass)]
    private sealed class CompassRequiringViewModel;

    /// <summary>Stands in for the other 46 tools: pure logic, available everywhere.</summary>
    private sealed class NoRequirementViewModel;

    private static ToolDescriptor ToolFor(string id, string categoryId, Type viewModelType)
        => new(id, $"{id}_Name", categoryId, [], id, viewModelType);

    private static CatalogService Create(
        IEnumerable<Category> categories,
        IEnumerable<ToolDescriptor> tools,
        params DeviceCapability[] supported)
        => new(
            [new StubCategoryContributor([.. categories])],
            [new StubToolContributor([.. tools])],
            [new DeviceCapabilityToolPolicy(new FakeDeviceCapabilityService(supported))],
            new ToolMatcher(),
            new FakeStringLocalizer());

    [TestMethod]
    public void GetTools_CompassRequiredAndDeviceHasNone_OmitsTool()
    {
        var service = Create(
            [Field],
            [ToolFor("Compass", "Field", typeof(CompassRequiringViewModel))]);

        Assert.AreEqual(0, service.GetTools().Count);
        Assert.AreEqual(0, service.GetToolsByCategory("Field").Count);
        Assert.AreEqual(0, service.Search("Compass").Count);
    }

    [TestMethod]
    public void GetTools_CompassRequiredAndDeviceHasCompass_IncludesTool()
    {
        var service = Create(
            [Field],
            [ToolFor("Compass", "Field", typeof(CompassRequiringViewModel))],
            DeviceCapability.Compass);

        Assert.AreEqual("Compass", service.GetTools().Single().Id);
    }

    [TestMethod]
    public void GetTools_ToolWithNoRequirement_IsAlwaysAvailable()
    {
        // Flashlight: a full-brightness white screen works on every head, so it declares nothing.
        var service = Create(
            [Field],
            [ToolFor("Flashlight", "Field", typeof(NoRequirementViewModel))]);

        Assert.AreEqual("Flashlight", service.GetTools().Single().Id);
    }

    [TestMethod]
    public void GetNavigationTree_HidingLastToolInCategory_DropsCategoryNode()
    {
        var service = Create(
            [Field, Ciphers],
            [
                ToolFor("Compass", "Field", typeof(CompassRequiringViewModel)),
                ToolFor("Caesar", "Ciphers", typeof(NoRequirementViewModel)),
            ]);

        var categoryIds = service.GetNavigationTree().Roots
            .SelectMany(r => r.Categories)
            .Select(c => c.CategoryId)
            .ToList();

        CollectionAssert.AreEqual(new[] { "Ciphers" }, categoryIds);
    }

    [TestMethod]
    public void GetNavigationTree_CapabilityPresent_KeepsCategoryNode()
    {
        var service = Create(
            [Field],
            [ToolFor("Compass", "Field", typeof(CompassRequiringViewModel))],
            DeviceCapability.Compass);

        var tools = service.GetNavigationTree().Roots
            .SelectMany(r => r.Categories)
            .SelectMany(c => c.Tools)
            .Select(t => t.ToolId)
            .ToList();

        CollectionAssert.AreEqual(new[] { "Compass" }, tools);
    }

    [TestMethod]
    public void FindTool_HiddenTool_StillResolves()
    {
        var service = Create(
            [Field],
            [ToolFor("Compass", "Field", typeof(CompassRequiringViewModel))]);

        Assert.IsNotNull(service.FindTool("Compass"));
        Assert.IsNull(service.FindTool("NoSuchTool"));
    }

    [TestMethod]
    public void GetAllTools_HiddenTool_IsPresent()
    {
        var service = Create(
            [Field],
            [ToolFor("Compass", "Field", typeof(CompassRequiringViewModel))]);

        Assert.AreEqual("Compass", service.GetAllTools().Single().Id);
        Assert.AreEqual(0, service.GetTools().Count);
    }

    [TestMethod]
    public void Constructor_DuplicateIdInsideHiddenTool_StillThrows()
    {
        // Validation runs over the full discovered set, so a desktop CI machine still catches a
        // duplicate id that only an Android device would ever display.
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => Create(
            [Field],
            [
                ToolFor("Compass", "Field", typeof(CompassRequiringViewModel)),
                ToolFor("Compass", "Field", typeof(CompassRequiringViewModel)),
            ]));

        StringAssert.Contains(ex.Message, "Compass");
    }

    [TestMethod]
    public void Constructor_NoPoliciesRegistered_BehavesAsBefore()
    {
        CatalogService service = new(
            [new StubCategoryContributor(Field)],
            [new StubToolContributor(ToolFor("Compass", "Field", typeof(CompassRequiringViewModel)))],
            [],
            new ToolMatcher(),
            new FakeStringLocalizer());

        Assert.AreEqual(1, service.GetTools().Count);
    }

    [TestMethod]
    public void IsAvailable_ReadsAttributeOncePerType()
    {
        DeviceCapabilityToolPolicy policy = new(new FakeDeviceCapabilityService(DeviceCapability.Compass));
        var tool = ToolFor("Compass", "Field", typeof(CompassRequiringViewModel));

        policy.IsAvailable(tool);
        policy.IsAvailable(tool);
        policy.IsAvailable(tool with { Id = "CompassClone" });

        // One memoized entry per ViewModel type, not one reflection hit per call.
        var cache = (IDictionary)typeof(DeviceCapabilityToolPolicy)
            .GetField("_requirements", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(policy)!;

        Assert.AreEqual(1, cache.Count);
    }
}
