using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels;

/// <summary>
/// Base for stub and (future) real tool ViewModels. Absorbs the host behavior the old
/// <c>ToolHostViewModel</c> carried so one shared <c>ToolHostView</c> can host any tool VM,
/// while navigation stays view-model-first (research R10). A subclass supplies its constant
/// <c>Id</c> to the base; the base resolves the descriptor and everything derived from it.
/// </summary>
public abstract partial class ToolViewModelBase : ViewModelBase
{
    private readonly ICatalogService _catalog;
    private readonly IRecentsService _recents;
    private readonly IFavoriteToolsService _favorites;
    private readonly IStringLocalizer _localizer;
    private bool _activated;

    protected ToolViewModelBase(
        string toolId,
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer)
    {
        ToolId = toolId;
        _catalog = catalog;
        _recents = recents;
        _favorites = favorites;
        _localizer = localizer;
    }

    /// <summary>The tool's stable <c>Id</c> (matches its <see cref="ToolDescriptor.Id"/>).</summary>
    public string ToolId { get; }

    [ObservableProperty]
    public partial string ToolName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Tooltip { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPlaceholder { get; set; }

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public override void ViewCreated()
    {
        base.ViewCreated();
        Activate();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        Activate();
    }

    /// <summary>
    /// Resolves this tool's descriptor, populates the localized name/tooltip/title, reflects the
    /// favorite state, and records the open in recents (FR-020). Runs once per instance regardless
    /// of whether <see cref="ViewCreated"/> or <see cref="OnNavigatedTo"/> fires first.
    /// </summary>
    private void Activate()
    {
        if (_activated)
        {
            return;
        }

        var tool = _catalog.GetTools().FirstOrDefault(t => string.Equals(t.Id, ToolId, StringComparison.Ordinal));
        if (tool is null)
        {
            return;
        }

        _activated = true;
        ToolName = _localizer[tool.NameKey].Value;
        Tooltip = string.IsNullOrEmpty(tool.TooltipKey) ? string.Empty : _localizer[tool.TooltipKey].Value;
        PageTitle = ToolName;
        IsPlaceholder = tool.IsPlaceholder;
        IsFavorite = _favorites.IsFavorite(ToolId);

        _ = _recents.RecordOpenedAsync(ToolId);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        await _favorites.ToggleAsync(ToolId);
        IsFavorite = _favorites.IsFavorite(ToolId);
    }
}
