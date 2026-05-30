using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Localization;

/// <summary>
/// Persists the chosen language under the <c>AppLanguage</c> preferences key and resolves the
/// first-run default per FR-009: the system language when it is <c>en</c> or <c>cs</c>, otherwise <c>en</c>.
/// </summary>
public sealed class LanguageService : ILanguageService
{
    public const string DefaultCode = "en";
    private const string StorageKey = "AppLanguage";

    private static readonly AppLanguage English = new("en", "Language_English");
    private static readonly AppLanguage Czech = new("cs", "Language_Czech");

    private readonly IPreferences _preferences;
    private readonly string _systemLanguageCode;

    public LanguageService(IPreferences preferences, string systemLanguageCode)
    {
        _preferences = preferences;
        _systemLanguageCode = systemLanguageCode ?? DefaultCode;
    }

    public IReadOnlyList<AppLanguage> Available { get; } = [English, Czech];

    public AppLanguage Current
    {
        get
        {
            var stored = _preferences.Get(StorageKey, string.Empty);
            var code = IsSupported(stored) ? stored : ResolveDefaultCode();
            return Available.First(l => string.Equals(l.Code, code, StringComparison.Ordinal));
        }
    }

    public event EventHandler? LanguageChanged;

    public Task SetAsync(string code)
    {
        if (!IsSupported(code))
        {
            throw new ArgumentException($"Unsupported language code '{code}'.", nameof(code));
        }

        if (!string.Equals(_preferences.Get(StorageKey, string.Empty), code, StringComparison.Ordinal))
        {
            _preferences.Set(StorageKey, code);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    private string ResolveDefaultCode()
        => IsSupported(_systemLanguageCode) ? _systemLanguageCode : DefaultCode;

    private bool IsSupported(string code)
        => Available.Any(l => string.Equals(l.Code, code, StringComparison.Ordinal));
}
