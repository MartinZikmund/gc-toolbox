using GcToolkit.Core.Catalog;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Yields a fixed set of categories for catalog tests.</summary>
public sealed class StubCategoryContributor(params Category[] categories) : ICategoryContributor
{
    public IEnumerable<Category> GetCategories() => categories;
}

/// <summary>Yields a fixed set of tools for catalog tests.</summary>
public sealed class StubToolContributor(params ToolDescriptor[] tools) : IToolContributor
{
    public IEnumerable<ToolDescriptor> GetTools() => tools;
}
