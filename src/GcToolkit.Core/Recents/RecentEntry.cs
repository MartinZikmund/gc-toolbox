namespace GcToolkit.Core.Recents;

/// <summary>A recently opened tool. Persisted as part of a JSON list under the <c>recents</c> key.</summary>
/// <param name="ToolId">References <see cref="Catalog.ToolDescriptor.Id"/>.</param>
/// <param name="LastOpenedUtc">When the tool was last opened.</param>
public sealed record RecentEntry(string ToolId, DateTimeOffset LastOpenedUtc);
