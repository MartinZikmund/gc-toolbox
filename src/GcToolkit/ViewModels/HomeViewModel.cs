using System.Collections.ObjectModel;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.ViewModels;

/// <summary>
/// Landing screen: a search entry into the catalog plus the user's favorite tools and recently
/// used tools. Stays in sync via the favorite-tools and recents change events.
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly IFavoriteToolsService _favorites;
    private readonly IRecentsService _recents;
    private readonly ICatalogService _catalog;
    private readonly INavigationService _navigation;
    private readonly IStringLocalizer _localizer;
    private bool _subscribed;

    public HomeViewModel(
        IFavoriteToolsService favorites,
        IRecentsService recents,
        ICatalogService catalog,
        INavigationService navigation,
        IStringLocalizer localizer,
        SearchViewModel search)
    {
        _favorites = favorites;
        _recents = recents;
        _catalog = catalog;
        _navigation = navigation;
        _localizer = localizer;
        Search = search;
        PageTitle = _localizer["ApplicationName"];
    }

    public SearchViewModel Search { get; }

    public ObservableCollection<ToolListItem> FavoriteTools { get; } = new();

    public ObservableCollection<ToolListItem> RecentTools { get; } = new();

    [ObservableProperty]
    public partial bool HasFavoriteTools { get; set; }

    [ObservableProperty]
    public partial bool HasRecentTools { get; set; }

    public override void ViewCreated()
    {
        if (!_subscribed)
        {
            _favorites.FavoriteToolsChanged += OnDataChanged;
            _recents.RecentsChanged += OnDataChanged;
            _subscribed = true;
        }

        Refresh();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        Refresh();
    }

    [RelayCommand]
    private void GoToCatalog() => _navigation.Navigate<CatalogViewModel>();

    private void OnDataChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        Populate(FavoriteTools, _favorites.GetFavoriteToolIds());
        Populate(RecentTools, _recents.GetRecentToolIds());
        HasFavoriteTools = FavoriteTools.Count > 0;
        HasRecentTools = RecentTools.Count > 0;
    }

    private void Populate(ObservableCollection<ToolListItem> target, IReadOnlyList<string> toolIds)
    {
        target.Clear();
        foreach (var id in toolIds)
        {
            var tool = _catalog.GetTools().FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
            if (tool is not null)
            {
                target.Add(CreateItem(tool));
            }
        }
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
