namespace GcToolkit.Core.Localization;

/// <summary>A selectable UI language.</summary>
/// <param name="Code">Two-letter ISO code (<c>en</c> / <c>cs</c>).</param>
/// <param name="NativeNameKey">Localization key for the language's native display name.</param>
public sealed record AppLanguage(string Code, string NativeNameKey);

/// <summary>
/// Persists the selected UI language and resolves the first-run default. The selection is
/// applied at startup, so a change takes effect on the next launch (restart accepted, FR-008/009).
/// </summary>
public interface ILanguageService
{
    AppLanguage Current { get; }

    IReadOnlyList<AppLanguage> Available { get; }

    /// <summary>Persists the chosen language code; applied on next launch.</summary>
    Task SetAsync(string code);

    /// <summary>Raised when the persisted selection changes (drives the restart-to-apply notice).</summary>
    event EventHandler? LanguageChanged;
}
