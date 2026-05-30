using System.Globalization;

namespace GcToolkit.Core.Search;

/// <summary>
/// Accent- and case-insensitive substring matcher. Uses
/// <see cref="CompareInfo.IndexOf(string, string, CompareOptions)"/> with
/// <see cref="CompareOptions.IgnoreNonSpace"/> so a query without diacritics matches
/// accented text and vice versa (e.g. <c>reseni</c> ⇄ <c>řešení</c>), satisfying FR-003.
/// </summary>
public sealed class ToolMatcher : IToolMatcher
{
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
    private static readonly CompareInfo CompareInfo = CultureInfo.InvariantCulture.CompareInfo;

    public bool Matches(string query, IEnumerable<string> values)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var trimmed = query.Trim();
        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(value) && CompareInfo.IndexOf(value, trimmed, Options) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
