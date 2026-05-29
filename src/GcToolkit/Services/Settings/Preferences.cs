using System.Text.Json;
using Windows.Storage;

namespace GcToolkit.Services.Settings;

public sealed class Preferences : IPreferences
{
    private readonly ApplicationDataContainer _container = ApplicationData.Current.LocalSettings;

    public T Get<T>(string key, T defaultValue)
    {
        if (_container.Values.TryGetValue(key, out var value))
        {
            if (value is T typed)
            {
                return typed;
            }

            // Handle numeric type conversions (ApplicationData stores as object)
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        _container.Values[key] = value;
    }

    public T GetComplex<T>(string key, T defaultValue)
    {
        if (_container.Values.TryGetValue(key, out var value) && value is string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    public void SetComplex<T>(string key, T value)
    {
        _container.Values[key] = JsonSerializer.Serialize(value);
    }

    public bool ContainsKey(string key) => _container.Values.ContainsKey(key);

    public void Remove(string key) => _container.Values.Remove(key);

    public void Clear() => _container.Values.Clear();
}
