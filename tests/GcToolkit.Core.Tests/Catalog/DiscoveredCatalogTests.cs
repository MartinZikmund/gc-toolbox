using System;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Search;
using GcToolkit.Core.Tests.Fakes;

namespace GcToolkit.Core.Tests.Catalog;

/// <summary>
/// T018: a contributor mirroring the generated shape feeds extended <see cref="ToolDescriptor"/>s
/// into <see cref="CatalogService"/>; verifies the new metadata (TooltipKey, GroupId,
/// IntroducedDate, UpdatedDate, IconKey) is exposed verbatim and ordering stays deterministic
/// (category Order, then Id ordinal).
/// </summary>
[TestClass]
public class DiscoveredCatalogTests
{
    private static readonly Category Coordinates = new("Coordinates", "Category_Coordinates", 0, "Coordinates", "Conversion");
    private static readonly Category Ciphers = new("Ciphers", "Category_Ciphers", 1, "Ciphers");

    private static readonly ToolDescriptor CoordinateConversion = new(
        Id: "CoordinateConversion",
        NameKey: "CoordinateConversion_Name",
        CategoryId: "Coordinates",
        Keywords: ["coords", "gps"],
        IconKey: "CoordinateConversion",
        ViewModelType: typeof(object),
        IsPlaceholder: false,
        TooltipKey: "CoordinateConversion_Tooltip",
        GroupId: "Conversion",
        IntroducedDate: new DateOnly(2024, 1, 15),
        UpdatedDate: new DateOnly(2025, 3, 20));

    private static readonly ToolDescriptor Bearing = new(
        Id: "Bearing",
        NameKey: "Bearing_Name",
        CategoryId: "Coordinates",
        Keywords: ["heading"],
        IconKey: "Bearing",
        ViewModelType: typeof(object),
        IsPlaceholder: true,
        TooltipKey: "Bearing_Tooltip",
        GroupId: "Conversion",
        IntroducedDate: new DateOnly(2024, 6, 1),
        UpdatedDate: new DateOnly(2024, 6, 1));

    private static readonly ToolDescriptor Caesar = new(
        Id: "Caesar",
        NameKey: "Caesar_Name",
        CategoryId: "Ciphers",
        Keywords: ["shift", "rot"],
        IconKey: "Caesar",
        ViewModelType: typeof(object),
        IsPlaceholder: false,
        TooltipKey: "Caesar_Tooltip",
        GroupId: null,
        IntroducedDate: new DateOnly(2023, 11, 9),
        UpdatedDate: new DateOnly(2025, 1, 2));

    private static CatalogService CreateCatalog()
        => new(
            [new StubCategoryContributor(Coordinates, Ciphers)],
            [new StubToolContributor(CoordinateConversion, Bearing, Caesar)],
            new ToolMatcher(),
            new FakeStringLocalizer());

    [TestMethod]
    public void GetTools_ExposesExtendedMetadata_Verbatim()
    {
        var service = CreateCatalog();

        var tool = service.GetTools().Single(t => t.Id == "CoordinateConversion");

        Assert.AreEqual("CoordinateConversion_Tooltip", tool.TooltipKey);
        Assert.AreEqual("Conversion", tool.GroupId);
        Assert.AreEqual(new DateOnly(2024, 1, 15), tool.IntroducedDate);
        Assert.AreEqual(new DateOnly(2025, 3, 20), tool.UpdatedDate);
        Assert.IsFalse(tool.IsPlaceholder);
    }

    [TestMethod]
    public void GetTools_OrdersByCategoryOrderThenIdOrdinal()
    {
        var service = CreateCatalog();

        var ids = service.GetTools().Select(t => t.Id).ToList();

        // Coordinates (Order 0) before Ciphers (Order 1); within Coordinates, Id ordinal: Bearing < CoordinateConversion.
        CollectionAssert.AreEqual(new[] { "Bearing", "CoordinateConversion", "Caesar" }, ids);
    }

    [TestMethod]
    public void GetToolsByCategory_ReturnsMatchingTools_WithMetadata()
    {
        var service = CreateCatalog();

        var tools = service.GetToolsByCategory("Coordinates");

        CollectionAssert.AreEqual(new[] { "Bearing", "CoordinateConversion" }, tools.Select(t => t.Id).ToList());

        var bearing = tools.Single(t => t.Id == "Bearing");
        Assert.AreEqual("Bearing_Tooltip", bearing.TooltipKey);
        Assert.AreEqual("Conversion", bearing.GroupId);
        Assert.AreEqual(new DateOnly(2024, 6, 1), bearing.IntroducedDate);
        Assert.AreEqual(new DateOnly(2024, 6, 1), bearing.UpdatedDate);
        Assert.IsTrue(bearing.IsPlaceholder);
    }

    [TestMethod]
    public void GetTools_IconKey_RoundTripsAndEqualsId()
    {
        var service = CreateCatalog();

        foreach (var tool in service.GetTools())
        {
            Assert.AreEqual(tool.Id, tool.IconKey);
        }
    }

    [TestMethod]
    public void GetTools_NullGroupId_RoundTrips()
    {
        var service = CreateCatalog();

        var caesar = service.GetTools().Single(t => t.Id == "Caesar");

        Assert.IsNull(caesar.GroupId);
        Assert.AreEqual("Caesar_Tooltip", caesar.TooltipKey);
        Assert.AreEqual(new DateOnly(2023, 11, 9), caesar.IntroducedDate);
        Assert.AreEqual(new DateOnly(2025, 1, 2), caesar.UpdatedDate);
    }
}
