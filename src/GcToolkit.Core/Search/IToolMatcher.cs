namespace GcToolkit.Core.Search;

/// <summary>
/// Pure, accent- and case-insensitive matcher used to filter the catalog. Kept free of
/// localization so it can be unit-tested in isolation; callers (the catalog service)
/// assemble the searchable values (localized name + keywords) for each tool.
/// </summary>
public interface IToolMatcher
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="query"/> appears (ignoring case
    /// and diacritics, in either direction) within any of <paramref name="values"/>.
    /// An empty or whitespace query matches everything.
    /// </summary>
    bool Matches(string query, IEnumerable<string> values);
}
