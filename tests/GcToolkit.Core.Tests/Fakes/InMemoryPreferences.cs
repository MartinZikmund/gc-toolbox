using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GcToolkit.Core.Serialization;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IPreferences"/> test double. Mirrors the real (ApplicationData-backed)
/// implementation: plain and complex values share one store, complex ones as their JSON string,
/// resolved through the contracts of a <see cref="JsonSerializerOptions"/> instance. Use
/// <see cref="TrimmedHead"/> for a double that behaves like a trimmed head, where only the
/// source-generated contracts resolve.
/// </summary>
public sealed class InMemoryPreferences(JsonSerializerOptions? jsonOptions = null) : IPreferences
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly JsonSerializerOptions _jsonOptions = jsonOptions ?? JsonSerializerOptions.Default;

    /// <summary>
    /// A double wired the way the app is on a trimmed head (the WASM release publish): reflection-based
    /// contracts are gone, so a type missing from <see cref="PreferencesJsonContext"/> fails to serialize.
    /// </summary>
    public static InMemoryPreferences TrimmedHead()
        => new(new JsonSerializerOptions { TypeInfoResolver = PreferencesJsonContext.Default });

    public T Get<T>(string key, T defaultValue)
        => TryGet<T>(key, out var value) ? value : defaultValue;

    public bool TryGet<T>(string key, out T value)
    {
        if (_values.TryGetValue(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    public void Set<T>(string key, T value) => _values[key] = value;

    public T GetComplex<T>(string key, T defaultValue)
        => TryGetComplex<T>(key, out var value) ? value : defaultValue;

    public bool TryGetComplex<T>(string key, out T value)
    {
        if (TryGet<string>(key, out var json))
        {
            try
            {
                var result = JsonSerializer.Deserialize(json, TypeInfo<T>());
                if (result is not null)
                {
                    value = result;
                    return true;
                }
            }
            catch (JsonException)
            {
                // fall through
            }
        }

        value = default!;
        return false;
    }

    public void SetComplex<T>(string key, T value) => _values[key] = JsonSerializer.Serialize(value, TypeInfo<T>());

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public void Remove(string key) => _values.Remove(key);

    public void Clear() => _values.Clear();

    /// <summary>Test helper: writes a raw stored value for a key, to simulate corrupt stored data.</summary>
    public void SetRawComplex(string key, string json) => _values[key] = json;

    private JsonTypeInfo<T> TypeInfo<T>() => (JsonTypeInfo<T>)_jsonOptions.GetTypeInfo(typeof(T));
}
