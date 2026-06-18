using System.Globalization;
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
/// Antipodes calculator (issue #2). Computes the point diametrically opposite a coordinate on Earth —
/// the latitude hemisphere flips and the longitude becomes <c>lon ± 180</c> — live as the user types.
/// Goes beyond the geocachingtoolbox.com single-format converter: it accepts any notation the shared
/// <see cref="CoordinateParser"/> understands (DD/DDM/DMS, UTM, MGRS), renders the antipode in all three
/// angular formats at once with per-format copy, reports the ~20,000 km distance, and validates/clears
/// gracefully. The math lives in the pure <see cref="Antipode"/> helper (thin-VM convention).
/// </summary>
[Tool("Antipodes", ToolCategory.Coordinates,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["antipode", "antipodes", "opposite", "coordinate", "protinožci", "souřadnice"])]
public sealed partial class AntipodesViewModel : ToolViewModelBase
{
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public AntipodesViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Antipodes", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The antipode in degrees-decimal-minutes (the geocaching standard, and the parity format).</summary>
    [ObservableProperty]
    public partial string OutputDdm { get; set; } = string.Empty;

    /// <summary>The antipode in decimal degrees.</summary>
    [ObservableProperty]
    public partial string OutputDd { get; set; } = string.Empty;

    /// <summary>The antipode in degrees-minutes-seconds.</summary>
    [ObservableProperty]
    public partial string OutputDms { get; set; } = string.Empty;

    /// <summary>The distance fact, e.g. "About 20,004 km to the antipode."</summary>
    [ObservableProperty]
    public partial string DistanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    partial void OnInputTextChanged(string value) => Compute();

    partial void OnHasResultChanged(bool value)
    {
        CopyDdmCommand.NotifyCanExecuteChanged();
        CopyDdCommand.NotifyCanExecuteChanged();
        CopyDmsCommand.NotifyCanExecuteChanged();
        ShareCommand.NotifyCanExecuteChanged();
    }

    private void Compute()
    {
        // Empty input is the neutral state: no result, no error.
        if (string.IsNullOrWhiteSpace(InputText))
        {
            Reset(error: false);
            return;
        }

        if (!CoordinateParser.TryParse(InputText, out var source, out _))
        {
            Reset(error: true);
            ErrorMessage = _localizer["Antipodes_InvalidInput"].Value;
            return;
        }

        var antipode = Antipode.Of(source);

        OutputDdm = CoordinateFormatter.Format(antipode, CoordinateFormat.DegreesDecimalMinutes);
        OutputDd = CoordinateFormatter.Format(antipode, CoordinateFormat.DecimalDegrees);
        OutputDms = CoordinateFormatter.Format(antipode, CoordinateFormat.DegreesMinutesSeconds);
        DistanceText = BuildDistanceText(source, antipode);

        HasError = false;
        ErrorMessage = string.Empty;
        HasResult = true;
    }

    private string BuildDistanceText(GeoCoordinate source, GeoCoordinate antipode)
    {
        var kilometres = Geodesy.DistanceMeters(source, antipode) / 1000.0;
        var rounded = Math.Round(kilometres).ToString("N0", CultureInfo.CurrentCulture);

        // Format here (not via the localizer's arg overload) so the substitution is deterministic and
        // testable regardless of the IStringLocalizer implementation.
        var template = _localizer["Antipodes_DistanceFormat"].Value;
        return string.Format(CultureInfo.CurrentCulture, template, rounded);
    }

    private void Reset(bool error)
    {
        OutputDdm = string.Empty;
        OutputDd = string.Empty;
        OutputDms = string.Empty;
        DistanceText = string.Empty;
        HasResult = false;
        HasError = error;
        if (!error)
        {
            ErrorMessage = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyDdm() => _clipboard.SetText(OutputDdm);

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyDd() => _clipboard.SetText(OutputDd);

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyDms() => _clipboard.SetText(OutputDms);

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, OutputDdm);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
