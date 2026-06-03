using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

[Tool("CoordinateProjection", ToolCategory.Coordinates,
      Introduced = "2026-02-10", Updated = "2026-02-10",
      Keywords = ["projection", "projekce", "waypoint", "bod"])]
public sealed partial class CoordinateProjectionViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer)
    : ToolViewModelBase("CoordinateProjection", catalog, recents, favorites, localizer);
