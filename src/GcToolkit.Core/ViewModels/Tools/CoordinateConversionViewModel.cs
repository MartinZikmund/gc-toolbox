using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

[Tool("CoordinateConversion", ToolCategory.Coordinates,
      Introduced = "2026-05-20", Updated = "2026-05-20",
      Keywords = ["wgs84", "utm", "convert", "souřadnice", "převod", "gps"])]
public sealed partial class CoordinateConversionViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer)
    : ToolViewModelBase("CoordinateConversion", catalog, recents, favorites, localizer);
