using System.Diagnostics.CodeAnalysis;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace GcToolkit.Views;

/// <summary>
/// Shared host for every discovered tool. Unlike <see cref="ViewBase{TViewModel}"/>, which binds one
/// fixed ViewModel, this host resolves the concrete tool ViewModel conveyed as the navigation
/// parameter (a <see cref="Type"/>) from DI and drives its lifecycle — so a single view hosts any
/// <see cref="ToolViewModelBase"/> while navigation stays view-model-first (research R10).
/// </summary>
[NavigationInfo(NavigationSection.Tool)]
public partial class ToolHostViewBase : Page, IViewBase
{
    private Type? _viewModelType;
    private bool _isNavigationDeferred;

    protected ToolHostViewBase()
    {
        Loading += OnPageLoading;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    public ToolViewModelBase? ViewModel { get; private set; }

    object? IViewBase.ViewModel => ViewModel;

    [MemberNotNull(nameof(ViewModel))]
    private void EnsureViewModel()
    {
        if (ViewModel is not null)
        {
            return;
        }

        if (_viewModelType is null)
        {
            throw new InvalidOperationException("ToolHostView was navigated to without a tool ViewModel type.");
        }

        if (FindWindowShell(Frame.XamlRoot?.Content) is not WindowShell windowShell)
        {
            throw new InvalidOperationException("View must be hosted inside a WindowShell.");
        }

        ViewModel = (ToolViewModelBase)windowShell.ServiceProvider.GetRequiredService(_viewModelType);
        DataContext = ViewModel;
        ViewModel.ViewCreated();
    }

    private bool TryEnsureViewModel()
    {
        if (ViewModel is not null)
        {
            return true;
        }

        if (_viewModelType is null || FindWindowShell(Frame?.XamlRoot?.Content) is not WindowShell windowShell)
        {
            return false;
        }

        ViewModel = (ToolViewModelBase)windowShell.ServiceProvider.GetRequiredService(_viewModelType);
        DataContext = ViewModel;
        ViewModel.ViewCreated();
        return true;
    }

    private static WindowShell? FindWindowShell(UIElement? windowRoot)
        => windowRoot switch
        {
            WindowShell shell => shell,
            ContentControl { Content: WindowShell contentShell } => contentShell,
            _ => null,
        };

    private void OnPageLoading(FrameworkElement sender, object args)
    {
        EnsureViewModel();
        if (_isNavigationDeferred)
        {
            ViewModel.OnNavigatedTo(null);
            _isNavigationDeferred = false;
        }

        ViewModel.ViewLoading();
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => ViewModel?.ViewLoaded();

    private void OnPageUnloaded(object sender, RoutedEventArgs e) => ViewModel?.ViewUnloaded();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is Type viewModelType)
        {
            _viewModelType = viewModelType;
        }

        if (TryEnsureViewModel())
        {
            ViewModel!.OnNavigatedTo(null);
        }
        else
        {
            // XamlRoot not yet available — resolve and activate during the Loading event.
            _isNavigationDeferred = true;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel?.OnNavigatedFrom();
    }
}

public sealed partial class ToolHostView : ToolHostViewBase
{
    public ToolHostView()
    {
        this.InitializeComponent();
    }
}
