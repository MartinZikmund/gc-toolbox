using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

[Tool("Checksum", ToolCategory.Numbers,
      Introduced = "2026-02-10", Updated = "2026-05-25",
      Keywords = ["checksum", "crc", "kontrolní", "součet", "hash"])]
public sealed partial class ChecksumViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer)
    : ToolViewModelBase("Checksum", catalog, recents, favorites, localizer);
