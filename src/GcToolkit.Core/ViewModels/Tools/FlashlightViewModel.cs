using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

[Tool("Flashlight", ToolCategory.Field,
      Introduced = "2026-02-10", Updated = "2026-02-10",
      Keywords = ["flashlight", "torch", "light", "baterka", "světlo"])]
public sealed partial class FlashlightViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer)
    : ToolViewModelBase("Flashlight", catalog, recents, favorites, localizer);
