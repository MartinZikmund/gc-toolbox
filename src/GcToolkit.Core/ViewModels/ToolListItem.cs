using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels;

/// <summary>
/// View-facing representation of a catalog tool shown in lists (Home, Catalog). Carries its
/// own commands so item templates can bind with <c>x:Bind</c> without reaching back to the page.
/// </summary>
public partial class ToolListItem : ObservableObject
{
    private readonly IStringLocalizer _localizer;

    public ToolListItem(
        string toolId,
        string name,
        string? tooltip,
        string? iconKey,
        bool isFavorite,
        IStringLocalizer localizer,
        Action<string> open,
        Action<string> toggleFavorite)
    {
        ToolId = toolId;
        Name = name;
        Tooltip = tooltip;
        IconKey = iconKey;
        _localizer = localizer;
        IsFavorite = isFavorite;
        OpenCommand = new RelayCommand(() => open(toolId));
        ToggleFavoriteCommand = new RelayCommand(() => toggleFavorite(toolId));
    }

    public string ToolId { get; }

    public string Name { get; }

    public string? Tooltip { get; }

    public string? IconKey { get; }

    public bool HasIcon => !string.IsNullOrEmpty(IconKey);

    /// <summary>Badge text ("New"/"Updated"/empty) derived from the tool's recency (US4).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    public partial string BadgeString { get; set; } = string.Empty;

    public bool HasBadge => !string.IsNullOrEmpty(BadgeString);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarGlyph))]
    [NotifyPropertyChangedFor(nameof(FavoriteToggleLabel))]
    public partial bool IsFavorite { get; set; }

    /// <summary>Filled star (U+E735) when favorited, outline (U+E734) otherwise — Segoe Fluent Icons.</summary>
    public string StarGlyph => IsFavorite ? "" : "";

    /// <summary>Accessible name for the favorite toggle, reflecting the action it performs.</summary>
    public string FavoriteToggleLabel => _localizer[IsFavorite ? "RemoveFromFavorites" : "AddToFavorites"].Value;

    public IRelayCommand OpenCommand { get; }

    public IRelayCommand ToggleFavoriteCommand { get; }
}
