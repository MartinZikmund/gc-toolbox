using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.ViewModels;

/// <summary>
/// Stub host for a catalog tool. Receives the tool id as the navigation parameter, resolves the
/// descriptor via the catalog, shows its localized name, records the open in recents, and exposes
/// a favorite toggle. Real tool functionality replaces this in later phases.
/// </summary>
public partial class ToolHostViewModel : ViewModelBase
{
    private readonly ICatalogService _catalog;
    private readonly IRecentsService _recents;
    private readonly IFavoriteToolsService _favorites;
    private readonly IStringLocalizer _localizer;
    private string? _toolId;

    public ToolHostViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer)
    {
        _catalog = catalog;
        _recents = recents;
        _favorites = favorites;
        _localizer = localizer;
    }

    [ObservableProperty]
    public partial string ToolName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPlaceholder { get; set; }

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);

        if (parameter is not string toolId)
        {
            return;
        }

        var tool = _catalog.GetTools().FirstOrDefault(t => string.Equals(t.Id, toolId, StringComparison.Ordinal));
        if (tool is null)
        {
            return;
        }

        _toolId = tool.Id;
        ToolName = _localizer[tool.NameKey].Value;
        PageTitle = ToolName;
        IsPlaceholder = tool.IsPlaceholder;
        IsFavorite = _favorites.IsFavorite(tool.Id);

        _ = _recents.RecordOpenedAsync(tool.Id);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (_toolId is null)
        {
            return;
        }

        await _favorites.ToggleAsync(_toolId);
        IsFavorite = _favorites.IsFavorite(_toolId);
    }
}
