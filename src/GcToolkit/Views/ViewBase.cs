using System.Diagnostics.CodeAnalysis;
using GcToolkit.Core.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace GcToolkit.Views;

public abstract partial class ViewBase<TViewModel> : Page, IViewBase
    where TViewModel : ViewModelBase
{
    private object? _pendingParameter;
    private bool _isNavigationDeferred;

    protected ViewBase()
    {
        Loading += OnPageLoading;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    public TViewModel? ViewModel { get; private set; }

    object? IViewBase.ViewModel => ViewModel;

    [MemberNotNull(nameof(ViewModel))]
    private void EnsureViewModel()
    {
        if (ViewModel is not null)
        {
            return;
        }

        if (FindWindowShell(Frame.XamlRoot?.Content) is not WindowShell windowShell)
        {
            throw new InvalidOperationException("View must be hosted inside a WindowShell.");
        }

        ViewModel = windowShell.ServiceProvider.GetRequiredService<TViewModel>();
        DataContext = ViewModel;
        ViewModel.ViewCreated();
    }

    private bool TryEnsureViewModel()
    {
        if (ViewModel is not null)
        {
            return true;
        }

        if (FindWindowShell(Frame?.XamlRoot?.Content) is not WindowShell windowShell)
        {
            return false;
        }

        ViewModel = windowShell.ServiceProvider.GetRequiredService<TViewModel>();
        DataContext = ViewModel;
        ViewModel.ViewCreated();
        return true;
    }

    private static WindowShell? FindWindowShell(UIElement? windowRoot)
    {
        if (windowRoot is WindowShell shell)
        {
            return shell;
        }

        // Handles Hot Design taking over the root
        if (windowRoot is ContentControl { Content: WindowShell contentShell })
        {
            return contentShell;
        }

        return null;
    }

    private void OnPageLoading(FrameworkElement sender, object args)
    {
        EnsureViewModel();
        if (_isNavigationDeferred)
        {
            ViewModel.OnNavigatedTo(_pendingParameter);
            _pendingParameter = null;
            _isNavigationDeferred = false;
        }
        ViewModel.ViewLoading();
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => ViewModel?.ViewLoaded();

    private void OnPageUnloaded(object sender, RoutedEventArgs e) => ViewModel?.ViewUnloaded();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (TryEnsureViewModel())
        {
            ViewModel!.OnNavigatedTo(e.Parameter);
        }
        else
        {
            // XamlRoot not yet available — defer until Loading event
            _isNavigationDeferred = true;
            _pendingParameter = e.Parameter;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel?.OnNavigatedFrom();
    }
}
