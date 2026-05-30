namespace GcToolkit.Core.Catalog;

/// <summary>
/// Contributes one or more <see cref="ToolDescriptor"/>s to the catalog. Resolved
/// from DI so the shell never references individual tools (FR-015). Later phases add
/// a tool by registering a contributor that yields its descriptor — no shell changes.
/// </summary>
public interface IToolContributor
{
    IEnumerable<ToolDescriptor> GetTools();
}

/// <summary>
/// Contributes one or more <see cref="Category"/> definitions to the catalog.
/// </summary>
public interface ICategoryContributor
{
    IEnumerable<Category> GetCategories();
}
