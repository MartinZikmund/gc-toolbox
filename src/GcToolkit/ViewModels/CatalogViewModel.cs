using System.Collections.ObjectModel;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.ViewModels;

/// <summary>A catalog category with its (optionally search-filtered) tools.</summary>
public sealed class CatalogGroup(string name, IReadOnlyList<ToolListItem> tools)
{
    public string Name { get; } = name;

    public IReadOnlyList<ToolListItem> Tools { get; } = tools;
}

/// <summary>
/// Browsable catalog: tools grouped by category (localized, ordered), filtered live by the
/// shared <see cref="SearchViewModel"/>. Opens a tool into the tool host and toggles favorites.
/// </summary>
public partial class CatalogViewModel : ViewModelBase
{
    private readonly ICatalogService _catalog;
    private readonly IFavoriteToolsService _favorites;
    private readonly INavigationService _navigation;
    private readonly IStringLocalizer _localizer;
    private readonly SearchViewModel _search;
    private bool _subscribed;

    public CatalogViewModel(
        ICatalogService catalog,
        IFavoriteToolsService favorites,
        INavigationService navigation,
        IStringLocalizer localizer,
        SearchViewModel search)
    {
        _catalog = catalog;
        _favorites = favorites;
        _navigation = navigation;
        _localizer = localizer;
        _search = search;
        PageTitle = _localizer["Catalog"];
    }

    public ObservableCollection<CatalogGroup> Groups { get; } = new();

    /// <summary>True when the catalog itself contains no tools.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>True when a non-empty search matched nothing.</summary>
    [ObservableProperty]
    public partial bool HasNoResults { get; set; }

    public override void ViewCreated()
    {
        if (!_subscribed)
        {
            _favorites.FavoriteToolsChanged += OnFavoritesChanged;
            _search.PropertyChanged += OnSearchChanged;
            _subscribed = true;
        }

        Rebuild();
    }

    private void OnSearchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchViewModel.Query))
        {
            Rebuild();
        }
    }

    private void OnFavoritesChanged(object? sender, EventArgs e)
    {
        foreach (var group in Groups)
        {
            foreach (var item in group.Tools)
            {
                item.IsFavorite = _favorites.IsFavorite(item.ToolId);
            }
        }
    }

    private void Rebuild()
    {
        var matchingByCategory = _catalog.Search(_search.Query).ToLookup(t => t.CategoryId, StringComparer.Ordinal);

        Groups.Clear();
        foreach (var category in _catalog.GetCategories())
        {
            var tools = matchingByCategory[category.Id].Select(CreateItem).ToList();
            if (tools.Count > 0)
            {
                Groups.Add(new CatalogGroup(_localizer[category.NameKey].Value, tools));
            }
        }

        IsEmpty = _catalog.GetTools().Count == 0;
        HasNoResults = !IsEmpty && Groups.Count == 0;
    }

    private ToolListItem CreateItem(ToolDescriptor tool)
        => new(
            tool.Id,
            _localizer[tool.NameKey].Value,
            tool.IconKey,
            _favorites.IsFavorite(tool.Id),
            OpenTool,
            ToggleFavorite);

    private void OpenTool(string toolId) => _navigation.Navigate<ToolHostViewModel>(toolId);

    private void ToggleFavorite(string toolId) => _ = _favorites.ToggleAsync(toolId);
}
