using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

[Tool("CoordinateDistance", ToolCategory.Coordinates,
      Introduced = "2026-02-10", Updated = "2026-02-10",
      Keywords = ["distance", "vzdálenost", "azimuth", "bearing", "azimut"])]
public sealed partial class CoordinateDistanceViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer)
    : ToolViewModelBase("CoordinateDistance", catalog, recents, favorites, localizer);
