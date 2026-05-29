using GcToolkit.Core.Services;

namespace GcToolkit.Core.ViewModels;

public partial class WindowShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    public WindowShellViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string LoadingStatusMessage { get; set; } = "";

    public bool CanGoBack => _navigationService.CanGoBack;

    public void NotifyCanGoBackChanged() => OnPropertyChanged(nameof(CanGoBack));

    [RelayCommand]
    public void GoBack()
    {
        _navigationService.GoBack();
        NotifyCanGoBackChanged();
    }
}
