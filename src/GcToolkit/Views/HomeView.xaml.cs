using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;
using GcToolkit.Services.Localization;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Home)]
public partial class HomeViewBase : ViewBase<HomeViewModel> { }

public sealed partial class HomeView : HomeViewBase
{
    public HomeView()
    {
        this.InitializeComponent();
    }

    public string RecentsHeader { get; } = SectionCaption("RecentlyUsed");

    public string FavoritesHeader { get; } = SectionCaption("FavoriteTools");

    public string NewAndUpdatedHeader { get; } = SectionCaption("NewAndUpdated");

    // XAML has no text-transform, so the uppercase section captions are cased here. Invariant is
    // safe for the shipped languages and a language switch already requires a restart.
    private static string SectionCaption(string key) => Localizer.Instance[key].ToUpperInvariant();

    private string HeaderStatus(bool hasFavorites, bool hasRecents)
        => Localizer.Instance[hasFavorites || hasRecents ? "HomeStatusReady" : "HomeStatusEmpty"];
}
