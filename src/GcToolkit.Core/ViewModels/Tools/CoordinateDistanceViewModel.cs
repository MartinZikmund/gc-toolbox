using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Distance, bearing and midpoint between two points (issue #22). Parses Point A and Point B from any
/// supported notation (DD / DDM / DMS / UTM / MGRS) and, the moment both are valid, shows the geodesic
/// distance in metres, kilometres, feet <em>and</em> miles, both the initial and final bearing, and the
/// great-circle midpoint formatted in the geocaching norm (degrees + decimal minutes). Beyond
/// geocachingtoolbox.com parity (which gives one distance, one bearing and the midpoint): every distance
/// unit at once and the final bearing as well. All math is the library's (<see cref="Geodesy"/>); the
/// <see cref="CoordinateDistanceCalculator"/> helper assembles the result, keeping this VM thin.
/// </summary>
[Tool("CoordinateDistance", ToolCategory.Coordinates,
      Introduced = "2026-02-10", Updated = "2026-06-06",
      Keywords = ["distance", "vzdálenost", "azimuth", "bearing", "azimut", "midpoint", "střed", "heading", "kurz"])]
public sealed partial class CoordinateDistanceViewModel : ToolViewModelBase
{
    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public CoordinateDistanceViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("CoordinateDistance", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;
    }

    [ObservableProperty]
    public partial string PointAText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PointBText { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when both points parsed and the results below are populated.</summary>
    [ObservableProperty]
    public partial bool HasResult { get; set; }

    /// <summary><see langword="true"/> when a non-empty input failed to parse — surfaces the InfoBar.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DistanceMetersText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DistanceKilometersText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DistanceFeetText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DistanceMilesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InitialBearingText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FinalBearingText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MidpointText { get; set; } = string.Empty;

    partial void OnPointATextChanged(string value) => Recompute();

    partial void OnPointBTextChanged(string value) => Recompute();

    partial void OnHasResultChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        var aEntered = !string.IsNullOrWhiteSpace(PointAText);
        var bEntered = !string.IsNullOrWhiteSpace(PointBText);

        // Nothing typed yet (or one side cleared): a clean, message-free idle state.
        if (!aEntered && !bEntered)
        {
            ClearOutputs();
            HasError = false;
            return;
        }

        var a = default(GeoCoordinate);
        var b = default(GeoCoordinate);
        var aOk = aEntered && CoordinateParser.TryParse(PointAText, out a, out _);
        var bOk = bEntered && CoordinateParser.TryParse(PointBText, out b, out _);

        // A point that was typed but doesn't parse is an error; an empty side is just "incomplete".
        if ((aEntered && !aOk) || (bEntered && !bOk))
        {
            ClearOutputs();
            ErrorMessage = _localizer["CoordinateDistanceParseError"].Value;
            HasError = true;
            return;
        }

        HasError = false;

        // Both sides must be present and valid before there is anything to compute.
        if (!aOk || !bOk)
        {
            ClearOutputs();
            return;
        }

        var result = CoordinateDistanceCalculator.Calculate(a, b);

        DistanceMetersText = $"{result.DistanceMeters.ToString("N2", CultureInfo.InvariantCulture)} m";
        DistanceKilometersText = $"{result.DistanceKilometers.ToString("N3", CultureInfo.InvariantCulture)} km";
        DistanceFeetText = $"{result.DistanceFeet.ToString("N0", CultureInfo.InvariantCulture)} ft";
        DistanceMilesText = $"{result.DistanceMiles.ToString("N3", CultureInfo.InvariantCulture)} mi";
        InitialBearingText = FormatBearing(result.InitialBearingDegrees);
        FinalBearingText = FormatBearing(result.FinalBearingDegrees);
        MidpointText = CoordinateDistanceCalculator.FormatMidpoint(result.Midpoint);

        HasResult = true;
    }

    private static string FormatBearing(double degrees)
        => $"{degrees.ToString("N2", CultureInfo.InvariantCulture)}°";

    private void ClearOutputs()
    {
        HasResult = false;
        DistanceMetersText = string.Empty;
        DistanceKilometersText = string.Empty;
        DistanceFeetText = string.Empty;
        DistanceMilesText = string.Empty;
        InitialBearingText = string.Empty;
        FinalBearingText = string.Empty;
        MidpointText = string.Empty;
    }

    /// <summary>Builds the labelled, multi-line summary the Copy/Share actions emit.</summary>
    private string BuildSummary()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{_localizer["CoordinateDistancePointA"].Value}: {PointAText}");
        builder.AppendLine($"{_localizer["CoordinateDistancePointB"].Value}: {PointBText}");
        builder.AppendLine($"{_localizer["CoordinateDistanceDistance"].Value}: {DistanceMetersText} / {DistanceKilometersText} / {DistanceFeetText} / {DistanceMilesText}");
        builder.AppendLine($"{_localizer["CoordinateDistanceInitialBearing"].Value}: {InitialBearingText}");
        builder.AppendLine($"{_localizer["CoordinateDistanceFinalBearing"].Value}: {FinalBearingText}");
        builder.Append($"{_localizer["CoordinateDistanceMidpoint"].Value}: {MidpointText}");
        return builder.ToString();
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyOutput() => _clipboard.SetText(BuildSummary());

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildSummary());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear()
    {
        PointAText = string.Empty;
        PointBText = string.Empty;
    }
}
