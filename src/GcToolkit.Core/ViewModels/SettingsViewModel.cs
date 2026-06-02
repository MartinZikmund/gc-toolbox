using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Localization;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services.Dialogs;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Core.Services.Theming;

namespace GcToolkit.Core.ViewModels;

/// <summary>A selectable language with its localized native display name.</summary>
public sealed record LanguageOption(string Code, string DisplayName);

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IStringLocalizer _localizer;
    private readonly IAppPreferences _appPreferences;
    private readonly IThemeManager _themeManager;
    private readonly ILanguageService _languageService;
    private readonly IRecentsService _recentsService;
    private readonly IConfirmationDialogService _confirmationDialog;
    private readonly IApplication _application;
    private bool _isInitializing;

    public SettingsViewModel(
        IStringLocalizer localizer,
        IAppPreferences appPreferences,
        IThemeManager themeManager,
        ILanguageService languageService,
        IRecentsService recentsService,
        IConfirmationDialogService confirmationDialog,
        IApplication application)
    {
        _localizer = localizer;
        _appPreferences = appPreferences;
        _themeManager = themeManager;
        _languageService = languageService;
        _recentsService = recentsService;
        _confirmationDialog = confirmationDialog;
        _application = application;
        PageTitle = _localizer["Settings"];

        Languages = _languageService.Available
            .Select(l => new LanguageOption(l.Code, _localizer[l.NativeNameKey]))
            .ToList();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        try
        {
            _isInitializing = true;
            Theme = _appPreferences.Theme;
            SelectedLanguage = Languages.FirstOrDefault(l =>
                string.Equals(l.Code, _languageService.Current.Code, StringComparison.Ordinal));
        }
        finally
        {
            _isInitializing = false;
        }
    }

    public ElementTheme[] ThemeOptions { get; } = [ElementTheme.Default, ElementTheme.Light, ElementTheme.Dark];

    [ObservableProperty]
    public partial ElementTheme Theme { get; set; }

    partial void OnThemeChanged(ElementTheme value)
    {
        if (_isInitializing)
        {
            return;
        }

        _themeManager.SetTheme(value);
        _appPreferences.Theme = value;
    }

    public IReadOnlyList<LanguageOption> Languages { get; }

    [ObservableProperty]
    public partial LanguageOption? SelectedLanguage { get; set; }

    /// <summary>Shown after a language change to tell the user a restart is needed to apply it.</summary>
    [ObservableProperty]
    public partial bool ShowLanguageRestartNotice { get; set; }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_isInitializing || value is null)
        {
            return;
        }

        _ = _languageService.SetAsync(value.Code);
        ShowLanguageRestartNotice = true;
    }

    public string AppVersion => _application.AppVersion;

    public bool IsDebug =>
#if DEBUG
        true;
#else
		false;
#endif

    [RelayCommand]
    private async Task ClearRecentsAsync()
    {
        var result = await _confirmationDialog.ShowAsync(
            _localizer["ClearRecentsConfirmTitle"],
            _localizer["ClearRecentsConfirmMessage"]);

        if (result == ConfirmationResult.Confirmed)
        {
            await _recentsService.ClearAsync();
        }
    }

    [RelayCommand]
    private void ClearPreferences()
    {
        _appPreferences.Clear();
    }
}
