using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

[Tool("BaseConverter", ToolCategory.Numbers,
      Introduced = "2026-02-10", Updated = "2026-02-10",
      Keywords = ["binary", "hex", "base", "soustava", "převod", "číslo"])]
public sealed partial class BaseConverterViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer)
    : ToolViewModelBase("BaseConverter", catalog, recents, favorites, localizer);
