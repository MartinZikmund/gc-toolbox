using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

[Tool("MorseCode", ToolCategory.Ciphers,
      Introduced = "2026-02-10", Updated = "2026-02-10",
      Keywords = ["morse", "code", "kód", "telegraph"])]
public sealed partial class MorseCodeViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer)
    : ToolViewModelBase("MorseCode", catalog, recents, favorites, localizer);
