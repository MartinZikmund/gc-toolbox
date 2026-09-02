using System.Collections.ObjectModel;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels;

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
    private string? _scopedCategoryId;

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
        PageTitle = _localizer["Tools"];
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

    /// <summary>
    /// Enters category-scoped mode: lists only the given category's tools and deliberately ignores the
    /// global search query (research R12). Navigated via <c>Navigate&lt;CatalogViewModel&gt;(categoryId)</c>.
    /// </summary>
    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);

        // A null/empty parameter is an explicit request for the unscoped catalog. CatalogView is a cached
        // page (NavigationCacheMode="Required"), so the same instance is reused across navigations — the
        // scope must therefore be (re)resolved on every navigation, not only set on the scoped path (R12).
        _scopedCategoryId = parameter is string categoryId && categoryId.Length > 0 ? categoryId : null;
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
        if (_scopedCategoryId is not null)
        {
            RebuildScoped(_scopedCategoryId);
            return;
        }

        // Recover the title when leaving scoped mode (RebuildScoped sets it to the category name).
        PageTitle = _localizer["Tools"];

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

    private void RebuildScoped(string categoryId)
    {
        // Scoped mode ignores the search query entirely (R12).
        var category = _catalog.GetCategories().FirstOrDefault(c => string.Equals(c.Id, categoryId, StringComparison.Ordinal));
        var tools = _catalog.GetToolsByCategory(categoryId).Select(CreateItem).ToList();

        Groups.Clear();
        if (tools.Count > 0)
        {
            var name = category is not null ? _localizer[category.NameKey].Value : categoryId;
            Groups.Add(new CatalogGroup(name, tools));
            PageTitle = name;
        }

        IsEmpty = tools.Count == 0;
        HasNoResults = false;
    }

    private ToolListItem CreateItem(ToolDescriptor tool)
    {
        ToolListItem item = new(
            tool.Id,
            _localizer[tool.NameKey].Value,
            tool.TooltipKey is null ? null : _localizer[tool.TooltipKey].Value,
            tool.IconKey,
            _favorites.IsFavorite(tool.Id),
            _localizer,
            OpenTool,
            ToggleFavorite);
        item.BadgeString = RecencyBadge(tool);
        return item;
    }

    private string RecencyBadge(ToolDescriptor tool)
        => ToolRecencyClassifier.Classify(tool.IntroducedDate, tool.UpdatedDate, DateOnly.FromDateTime(DateTime.Now)) switch
        {
            ToolRecency.New => _localizer["Badge_New"].Value,
            ToolRecency.Updated => _localizer["Badge_Updated"].Value,
            _ => string.Empty,
        };

    private void OpenTool(string toolId)
    {
        var tool = _catalog.GetTools().FirstOrDefault(t => string.Equals(t.Id, toolId, StringComparison.Ordinal));
        if (tool is not null)
        {
            _navigation.Navigate(tool.ViewModelType);
        }
    }

    private void ToggleFavorite(string toolId) => _ = _favorites.ToggleAsync(toolId);
}
