using GcToolkit.Core.ViewModels;
using GcToolkit.Services.Settings;
using GcToolkit.Services.Theming;

namespace GcToolkit.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IStringLocalizer _localizer;
    private readonly IAppPreferences _appPreferences;
    private readonly IThemeManager _themeManager;
    private bool _isInitializing;

    public SettingsViewModel(
        IStringLocalizer localizer,
        IAppPreferences appPreferences,
        IThemeManager themeManager)
    {
        _localizer = localizer;
        _appPreferences = appPreferences;
        _themeManager = themeManager;
        PageTitle = _localizer["Settings"];
    }

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        try
        {
            _isInitializing = true;
            Theme = _appPreferences.Theme;
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

    public string AppVersion
    {
        get
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public bool IsDebug =>
#if DEBUG
        true;
#else
		false;
#endif

    [RelayCommand]
    private void ClearPreferences()
    {
        Windows.Storage.ApplicationData.Current.LocalSettings.Values.Clear();
    }
}
