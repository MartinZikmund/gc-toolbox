using GcToolkit.Core.Navigation;

namespace GcToolkit.ViewModels;

/// <summary>
/// Shared, incremental search state. A single instance backs both the shell search surface
/// (WinUI <c>TitleBar</c> on Windows, the <c>NavigationView</c> elsewhere) and the Home search
/// entry, so search behaves identically everywhere (FR-003/FR-018). Typing a query routes the
/// user to the Catalog, which filters its list against <see cref="Query"/>.
/// </summary>
public partial class SearchViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public SearchViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    partial void OnQueryChanged(string value)
    {
        // Route to the Catalog so results are visible. Only navigate when arriving from another
        // section; subsequent keystrokes refine in place (the Catalog observes this Query).
        if (!string.IsNullOrWhiteSpace(value) && _navigation.CurrentSection != NavigationSection.Catalog)
        {
            _navigation.Navigate<CatalogViewModel>();
        }
    }

    public void Clear() => Query = string.Empty;
}
