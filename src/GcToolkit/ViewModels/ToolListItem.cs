using GcToolkit.Services.Localization;

namespace GcToolkit.ViewModels;

/// <summary>
/// View-facing representation of a catalog tool shown in lists (Home, Catalog). Carries its
/// own commands so item templates can bind with <c>x:Bind</c> without reaching back to the page.
/// </summary>
public partial class ToolListItem : ObservableObject
{
    public ToolListItem(
        string toolId,
        string name,
        string? iconKey,
        bool isFavorite,
        Action<string> open,
        Action<string> toggleFavorite)
    {
        ToolId = toolId;
        Name = name;
        IconKey = iconKey;
        IsFavorite = isFavorite;
        OpenCommand = new RelayCommand(() => open(toolId));
        ToggleFavoriteCommand = new RelayCommand(() => toggleFavorite(toolId));
    }

    public string ToolId { get; }

    public string Name { get; }

    public string? IconKey { get; }

    public bool HasIcon => !string.IsNullOrEmpty(IconKey);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarGlyph))]
    [NotifyPropertyChangedFor(nameof(FavoriteToggleLabel))]
    public partial bool IsFavorite { get; set; }

    /// <summary>Filled star (U+E735) when favorited, outline (U+E734) otherwise — Segoe Fluent Icons.</summary>
    public string StarGlyph => IsFavorite ? "" : "";

    /// <summary>Accessible name for the favorite toggle, reflecting the action it performs.</summary>
    public string FavoriteToggleLabel => Localizer.Instance.GetString(
        IsFavorite ? "RemoveFromFavorites" : "AddToFavorites");

    public IRelayCommand OpenCommand { get; }

    public IRelayCommand ToggleFavoriteCommand { get; }
}
