using GcToolkit.Core.Infrastructure;

namespace GcToolkit.Services.Localization;

public sealed class Localizer
{
    private static readonly Lazy<IStringLocalizer> _stringLocalizer =
        new(() => IoC.GetRequiredService<IStringLocalizer>());

    private Localizer() { }

    public static Localizer Instance { get; } = new();

    public string GetString(string key)
    {
        var result = _stringLocalizer.Value.GetString(key);
        return !result.ResourceNotFound ? result.Value : $"???{key}???";
    }

    public string this[string key] => GetString(key);
}
