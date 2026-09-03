using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.Services.Devices;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

// Hidden entirely on a device with no magnetometer — desktop, most Windows, most WASM.
[RequiresDeviceCapability(DeviceCapability.Compass)]
[Tool("Compass", ToolCategory.Field,
      Introduced = "2026-05-18", Updated = "2026-05-18",
      Keywords = ["compass", "kompas", "heading", "směr", "north"])]
public sealed partial class CompassViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer)
    : ToolViewModelBase("Compass", catalog, recents, favorites, localizer);
