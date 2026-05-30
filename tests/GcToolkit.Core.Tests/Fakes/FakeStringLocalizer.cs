using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>
/// Dictionary-backed <see cref="IStringLocalizer"/>. Returns the mapped value when present;
/// otherwise echoes the key and reports <see cref="LocalizedString.ResourceNotFound"/>.
/// </summary>
public sealed class FakeStringLocalizer : IStringLocalizer
{
    private readonly IReadOnlyDictionary<string, string> _map;

    public FakeStringLocalizer(IReadOnlyDictionary<string, string>? map = null)
        => _map = map ?? new Dictionary<string, string>(StringComparer.Ordinal);

    public LocalizedString this[string name]
        => _map.TryGetValue(name, out var value)
            ? new LocalizedString(name, value, resourceNotFound: false)
            : new LocalizedString(name, name, resourceNotFound: true);

    public LocalizedString this[string name, params object[] arguments] => this[name];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => _map.Select(kvp => new LocalizedString(kvp.Key, kvp.Value, resourceNotFound: false));
}
