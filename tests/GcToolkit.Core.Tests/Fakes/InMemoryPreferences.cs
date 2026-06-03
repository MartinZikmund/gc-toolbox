using System.Text.Json;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IPreferences"/> test double. Complex values are round-tripped
/// through System.Text.Json so service serialization behavior is exercised, mirroring the
/// real (ApplicationData-backed) implementation.
/// </summary>
public sealed class InMemoryPreferences : IPreferences
{
    private readonly Dictionary<string, object?> _scalars = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _complex = new(StringComparer.Ordinal);

    public T Get<T>(string key, T defaultValue)
        => _scalars.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

    public bool TryGet<T>(string key, out T value)
    {
        if (_scalars.TryGetValue(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    public void Set<T>(string key, T value) => _scalars[key] = value;

    public T GetComplex<T>(string key, T defaultValue)
    {
        if (_complex.TryGetValue(key, out var json))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json) ?? defaultValue;
            }
            catch (JsonException)
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    public bool TryGetComplex<T>(string key, out T value)
    {
        if (_complex.TryGetValue(key, out var json))
        {
            try
            {
                var result = JsonSerializer.Deserialize<T>(json);
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

    public void SetComplex<T>(string key, T value) => _complex[key] = JsonSerializer.Serialize(value);

    public bool ContainsKey(string key) => _scalars.ContainsKey(key) || _complex.ContainsKey(key);

    public void Remove(string key)
    {
        _scalars.Remove(key);
        _complex.Remove(key);
    }

    public void Clear()
    {
        _scalars.Clear();
        _complex.Clear();
    }

    /// <summary>Test helper: writes raw JSON for a complex key to simulate corrupt stored data.</summary>
    public void SetRawComplex(string key, string json) => _complex[key] = json;
}
