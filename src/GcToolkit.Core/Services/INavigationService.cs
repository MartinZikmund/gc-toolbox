using GcToolkit.Core.Navigation;

namespace GcToolkit.Core.Services;

public interface INavigationService
{
    bool CanGoBack { get; }

    NavigationSection? CurrentSection { get; }

    void Initialize();

    void Navigate<TViewModel>();

    void Navigate<TViewModel>(object? parameter);

    /// <summary>
    /// Navigates view-model-first by runtime type. Used to open a discovered tool whose concrete
    /// ViewModel type is only known at runtime (from its <c>ToolDescriptor</c>); the shared tool host
    /// receives the type and resolves the ViewModel from DI (research R10).
    /// </summary>
    void Navigate(Type viewModelType, object? parameter = null);

    bool GoBack();

    void ClearBackStack();
}
