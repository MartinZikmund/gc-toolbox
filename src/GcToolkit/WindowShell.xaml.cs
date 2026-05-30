using GcToolkit.Core.Navigation;
using GcToolkit.Core.Services;
using GcToolkit.Core.ViewModels;
using GcToolkit.Infrastructure;
using GcToolkit.Services.Navigation;
using GcToolkit.Services.Settings;
using GcToolkit.Services.Theming;
using GcToolkit.ViewModels;
using Microsoft.UI.Windowing;
using Windows.Foundation.Metadata;

namespace GcToolkit;

public sealed partial class WindowShell : Page, IWindowShell
{
    private readonly IServiceScope _windowScope;
    private readonly Window _associatedWindow;
    private bool _isWindowClosed;
    private ViewModelBase? _currentPageViewModel;

    public WindowShell(IServiceProvider serviceProvider, Window associatedWindow)
    {
        InitializeComponent();

        _windowScope = serviceProvider.CreateScope();
        var windowShellProvider = (WindowShellProvider)ServiceProvider.GetRequiredService<IWindowShellProvider>();
        windowShellProvider.SetShell(this, associatedWindow);

        var navigationService = ServiceProvider.GetRequiredService<INavigationService>();
        navigationService.Initialize();

        // Restore saved theme
        var settings = ServiceProvider.GetRequiredService<IAppPreferences>();
        var themeManager = ServiceProvider.GetRequiredService<IThemeManager>();
        themeManager.SetTheme(settings.Theme);

        _associatedWindow = associatedWindow;
        _associatedWindow.Closed += OnWindowClosed;
        CustomizeWindow();

        ViewModel = ServiceProvider.GetRequiredService<WindowShellViewModel>();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        Search = ServiceProvider.GetRequiredService<SearchViewModel>();

        InnerFrame.Navigated += InnerFrame_Navigated;
        Loading += WindowShell_Loading;

        UpdateWindowTitle();
    }

    public IServiceProvider ServiceProvider => _windowScope.ServiceProvider;

    public WindowShellViewModel ViewModel { get; }

    public SearchViewModel Search { get; }

    public Frame RootFrame => InnerFrame;

    public bool HasCustomTitleBar { get; private set; }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WindowShellViewModel.Title))
        {
            UpdateWindowTitle();
        }
    }

    private void UpdateWindowTitle()
    {
        if (ViewModel.Title is not null && !_isWindowClosed)
        {
            _associatedWindow.Title = ViewModel.Title;
        }
    }

    private void InnerFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        // Unsubscribe from previous page's ViewModel
        if (_currentPageViewModel is not null)
        {
            _currentPageViewModel.PropertyChanged -= PageViewModel_PropertyChanged;
            _currentPageViewModel = null;
        }

        // Subscribe to new page's ViewModel for title updates
        if (e.Content is FrameworkElement { DataContext: ViewModelBase pageViewModel })
        {
            _currentPageViewModel = pageViewModel;
            _currentPageViewModel.PropertyChanged += PageViewModel_PropertyChanged;
            UpdatePageTitle();
        }
        else
        {
            ViewModel.Title = ServiceProvider.GetRequiredService<IStringLocalizer>()["ApplicationName"];
        }

        ViewModel.NotifyCanGoBackChanged();
        UpdateNavigationViewSelection();
    }

    private void PageViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModelBase.PageTitle))
        {
            UpdatePageTitle();
        }
    }

    private void UpdatePageTitle()
    {
        if (_currentPageViewModel is not null)
        {
            ViewModel.Title = _currentPageViewModel.PageTitle
                ?? ServiceProvider.GetRequiredService<IStringLocalizer>()["ApplicationName"];
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _isWindowClosed = true;
        _associatedWindow.Closed -= OnWindowClosed;
        InnerFrame.Navigated -= InnerFrame_Navigated;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        if (_currentPageViewModel is not null)
        {
            _currentPageViewModel.PropertyChanged -= PageViewModel_PropertyChanged;
            _currentPageViewModel = null;
        }

        // Dispose the window-scoped container so scoped services (e.g. ThemeManager,
        // which subscribes to UISettings.ColorValuesChanged) release their handlers/resources.
        _windowScope.Dispose();
    }

    private void WindowShell_Loading(FrameworkElement sender, object args)
    {
        var windowShellProvider = (WindowShellProvider)ServiceProvider.GetRequiredService<IWindowShellProvider>();
        windowShellProvider.SetXamlRoot(XamlRoot ?? throw new InvalidOperationException("XamlRoot must be set."));
    }

    public void SetTitleBar(UIElement? titleBar)
    {
#if !HAS_UNO
        // The WinUI TitleBar control (AppTitleBar) is the Windows custom title bar; other heads use
        // the system title bar, so SetTitleBar is a no-op there.
        if (!_isWindowClosed)
        {
            _associatedWindow.SetTitleBar(titleBar ?? AppTitleBar);
        }
#endif
    }

    private void CustomizeWindow()
    {
#if !HAS_UNO
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            _associatedWindow.ExtendsContentIntoTitleBar = true;
            _associatedWindow.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            _associatedWindow.SetTitleBar(AppTitleBar);
            HasCustomTitleBar = true;
        }
#endif

        if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.Window", "SystemBackdrop"))
        {
            _associatedWindow.SystemBackdrop = new MicaBackdrop();
            Background = null;
        }
    }

    #region Navigation

    private void NavView_Loaded(object sender, RoutedEventArgs e)
        => NavigateToSection(NavigationSection.Home);

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem item)
        {
            return;
        }

        var tag = item.Tag?.ToString();
        if (tag is null && item == (NavigationViewItem)sender.SettingsItem)
        {
            tag = NavigationSection.Settings.ToString();
        }

        if (Enum.TryParse<NavigationSection>(tag, out var section))
        {
            NavigateToSection(section);
        }
    }

    private void NavigateToSection(NavigationSection section)
    {
        var nav = ServiceProvider.GetRequiredService<INavigationService>();
        switch (section)
        {
            case NavigationSection.Home:
                nav.Navigate<HomeViewModel>();
                break;
            case NavigationSection.Catalog:
                nav.Navigate<CatalogViewModel>();
                break;
            case NavigationSection.Settings:
                nav.Navigate<SettingsViewModel>();
                break;
            default:
                throw new NotSupportedException($"Navigation section not supported: {section}");
        }
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        var nav = ServiceProvider.GetRequiredService<INavigationService>();
        if (nav.GoBack())
        {
            UpdateNavigationViewSelection();
        }
    }

    private void UpdateNavigationViewSelection()
    {
        var section = ServiceProvider.GetRequiredService<INavigationService>().CurrentSection;
        NavView.SelectedItem = section switch
        {
            NavigationSection.Home => HomeNavItem,
            NavigationSection.Catalog => CatalogNavItem,
            NavigationSection.Settings => NavView.SettingsItem,
            // Tool host has no menu item — keep the current selection.
            _ => NavView.SelectedItem,
        };
    }

    #endregion
}
