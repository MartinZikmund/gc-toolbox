using System.Globalization;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Localization;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Search;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Dialogs;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Core.Services.Theming;
using GcToolkit.Core.ViewModels;
using GcToolkit.Services.Dialogs;
using GcToolkit.Services.Navigation;
using GcToolkit.Services.Rating;
using GcToolkit.Services.Settings;
using GcToolkit.Services.Theming;
using GcToolkit.ViewModels;
using Uno.Resizetizer;
using IPreferences = MZikmund.Toolkit.WinUI.Services.IPreferences;
using Preferences = MZikmund.Toolkit.WinUI.Services.Preferences;

namespace GcToolkit;

public partial class App : Application, IApplication
{
    public static new App Current => (App)Application.Current;

    public IServiceProvider Services => Host!.Services;

    public string AppVersion
    {
        get
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(ConfigureLogging, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                .UseLocalization()
                .UseDefaultServiceProvider((context, options) =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                })
                .UseHttp((context, services) =>
                {
#if DEBUG
                    services.AddTransient<DelegatingHandler, DebugHttpHandler>();
#endif
                })
                .ConfigureServices(RegisterServices)
            );

        MainWindow = builder.Window;

#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        Host = builder.Build();
        IoC.SetProvider(Host.Services);

        // Apply the persisted/resolved language before building the shell so the selection takes
        // effect on this launch. Resolving ILanguageService here (before the override) lets it
        // capture the real system language for first-run default resolution (FR-009).
        ApplyLanguage(Host.Services.GetRequiredService<ILanguageService>().Current.Code);

        // Run app lifecycle updates
        var appPreferences = Host.Services.GetRequiredService<IAppPreferences>();
        var appUpdater = Host.Services.GetRequiredService<IAppUpdater>();
        await appUpdater.EnsureAppUpToDateAsync();
        appPreferences.LaunchCount++;

        // Create WindowShell as root content
        if (MainWindow.Content is not WindowShell)
        {
            var shell = new WindowShell(Host.Services, MainWindow);
            MainWindow.Content = shell;
        }

        MainWindow.Activate();
    }

    private static void RegisterServices(HostBuilderContext context, IServiceCollection services)
    {
        // Singleton services
        services.AddSingleton<IApplication>(sp => Current);
        services.AddSingleton<IPreferences, Preferences>();
        services.AddSingleton<IAppPreferences, AppPreferences>();
        services.AddSingleton<IDisplayRequestManager, DisplayRequestManager>();
        services.AddSingleton<IAppUpdater, Infrastructure.AppUpdater>();
        services.AddScoped<IAppRatingService, AppRatingService>();

        // Per-window scoped services
        services.AddScoped<IThemeManager, ThemeManager>();
        services.AddScoped<WindowShellProvider>();
        services.AddScoped<IWindowShellProvider>(sp => sp.GetRequiredService<WindowShellProvider>());
        services.AddScoped<IXamlRootProvider>(sp => sp.GetRequiredService<WindowShellProvider>());
        services.AddScoped<IDialogCoordinator, DialogCoordinator>();
        services.AddScoped<IDialogService, DialogService>();
        services.AddScoped<IConfirmationDialogService, ConfirmationDialogService>();
        services.AddScoped<ILauncherService, LauncherService>();
        services.AddScoped<IShareService, ShareService>();
        services.AddScoped<INavigationService>(sp =>
        {
            var service = new NavigationService(sp.GetRequiredService<IWindowShellProvider>());
            service.RegisterView(typeof(Views.HomeView), typeof(HomeViewModel));
            service.RegisterView(typeof(Views.CatalogView), typeof(CatalogViewModel));
            service.RegisterView(typeof(Views.ToolHostView), typeof(ToolHostViewModel));
            service.RegisterView(typeof(Views.SettingsView), typeof(SettingsViewModel));
            return service;
        });

        // Catalog domain (FR-015): contributors feed the catalog; the placeholder set targets
        // the stub tool host. The matcher is a pure singleton; the catalog is per-window.
        services.AddSingleton<IToolMatcher, ToolMatcher>();
        services.AddSingleton(_ => new PlaceholderToolContributor(typeof(ToolHostViewModel)));
        services.AddSingleton<IToolContributor>(sp => sp.GetRequiredService<PlaceholderToolContributor>());
        services.AddSingleton<ICategoryContributor>(sp => sp.GetRequiredService<PlaceholderToolContributor>());
        services.AddScoped<ICatalogService, CatalogService>();

        // Favorite tools, recents, and language selection.
        services.AddScoped<IFavoriteToolsService, FavoriteToolsService>();
        services.AddScoped<IRecentsService, RecentsService>();
        services.AddSingleton<ILanguageService>(sp => new LanguageService(
            sp.GetRequiredService<IPreferences>(),
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));

        // Scoped ViewModels
        services.AddScoped<WindowShellViewModel>();
        services.AddScoped<SearchViewModel>();

        // Transient ViewModels (new instance per navigation)
        services.AddTransient<HomeViewModel>();
        services.AddTransient<CatalogViewModel>();
        services.AddTransient<ToolHostViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    /// <summary>
    /// Applies <paramref name="languageCode"/> via the WinRT language override and the current
    /// thread culture. The stock one-shot LocalizeExtension reads these when the shell loads, so a
    /// language change takes effect on the next launch (restart accepted, FR-008/009).
    /// </summary>
    private static void ApplyLanguage(string languageCode)
    {
        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = languageCode;

        var culture = new CultureInfo(languageCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private static void ConfigureLogging(HostBuilderContext context, ILoggingBuilder logBuilder)
    {
        logBuilder
            .SetMinimumLevel(
                context.HostingEnvironment.IsDevelopment() ?
                    LogLevel.Information :
                    LogLevel.Warning)
            .CoreLogLevel(LogLevel.Warning);
    }
}
