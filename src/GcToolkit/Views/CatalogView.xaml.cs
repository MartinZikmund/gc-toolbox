using System.Collections.Specialized;
using System.Globalization;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;
using GcToolkit.Services.Localization;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Catalog)]
public partial class CatalogViewBase : ViewBase<CatalogViewModel> { }

public sealed partial class CatalogView : CatalogViewBase
{
    public CatalogView()
    {
        this.InitializeComponent();

        Loaded += OnCatalogLoaded;
        Unloaded += OnCatalogUnloaded;
    }

    private void OnCatalogLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        // The page is cached (NavigationCacheMode="Required"), so Loaded runs again on every return.
        ViewModel.Groups.CollectionChanged -= OnGroupsChanged;
        ViewModel.Groups.CollectionChanged += OnGroupsChanged;
        UpdateScaleReadout();
    }

    private void OnCatalogUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.Groups.CollectionChanged -= OnGroupsChanged;
        }
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateScaleReadout();

    /// <summary>
    /// The header's scale line. Counted from the groups on screen — the view model exposes no
    /// totals — so while a search is active it states the size of the result, not of the catalog.
    /// </summary>
    private void UpdateScaleReadout()
    {
        var groups = ViewModel?.Groups;
        if (groups is null || groups.Count == 0)
        {
            ScaleReadout.Text = string.Empty;
            ScaleReadout.Visibility = Visibility.Collapsed;
            return;
        }

        // A category-scoped catalog is always one category; saying so is noise.
        var format = Localizer.Instance[groups.Count == 1 ? "CatalogScaleToolsOnly" : "CatalogScale"];
        ScaleReadout.Text = string.Format(CultureInfo.CurrentCulture, format, groups.Sum(g => g.Tools.Count), groups.Count);
        ScaleReadout.Visibility = Visibility.Visible;
    }

    private Visibility StateVisibility(bool isEmpty, bool hasNoResults)
        => isEmpty || hasNoResults ? Visibility.Visible : Visibility.Collapsed;

    private string StateHeadline(bool isEmpty) => Localizer.Instance[isEmpty ? "CatalogEmpty" : "SearchNoResults"];

    private string StateHint(bool isEmpty) => Localizer.Instance[isEmpty ? "CatalogEmptyHint" : "SearchNoResultsHint"];
}
