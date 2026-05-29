namespace GcToolkit.Services.Settings;

public interface IPreferences
{
    T Get<T>(string key, T defaultValue);

    void Set<T>(string key, T value);

    T GetComplex<T>(string key, T defaultValue);

    void SetComplex<T>(string key, T value);

    bool ContainsKey(string key);

    void Remove(string key);

    void Clear();
}
