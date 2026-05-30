namespace GcToolkit.Core.FavoriteTools;

/// <summary>A favorited tool. Persisted as part of a JSON list under the <c>favoriteTools</c> key.</summary>
/// <param name="ToolId">References <see cref="Catalog.ToolDescriptor.Id"/>.</param>
/// <param name="AddedUtc">When the tool was favorited (drives stable ordering).</param>
public sealed record FavoriteToolEntry(string ToolId, DateTimeOffset AddedUtc);
