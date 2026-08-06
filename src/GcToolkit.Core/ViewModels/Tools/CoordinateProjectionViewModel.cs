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
/// Coordinate projection (issue #16). Projects a free-text start coordinate by a distance along a
/// bearing measured clockwise from North, live as the user types, using the ellipsoidal
/// <see cref="Geodesy.Destination"/> (Vincenty). Matches geocachingtoolbox.com's projection (start
/// coordinate + angle + distance with Meter/Kilometer/Feet/Mile units) and goes beyond it by rendering
/// the result in all three angular notations at once (DD, DDM default, DMS) and offering Copy/Share.
/// All math lives in the coordinate library; <see cref="CoordinateProjection"/> builds the result —
/// the VM only wires text fields to bindings (thin-VM convention).
/// </summary>
[Tool("CoordinateProjection", ToolCategory.Coordinates,
      Introduced = "2026-02-10", Updated = "2026-06-06",
      Keywords = ["projection", "project", "waypoint", "bearing", "distance", "destination", "projekce", "bod", "azimut", "vzdálenost", "směr"])]
public sealed partial class CoordinateProjectionViewModel : ToolViewModelBase
{
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public CoordinateProjectionViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("CoordinateProjection", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>The free-text start coordinate (any notation <see cref="CoordinateParser"/> accepts).</summary>
    [ObservableProperty]
    public partial string StartCoordinate { get; set; } = string.Empty;

    /// <summary>The projection bearing in degrees, clockwise from North (free text, parsed invariantly).</summary>
    [ObservableProperty]
    public partial string AngleDegrees { get; set; } = string.Empty;

    /// <summary>The projection distance in the selected <see cref="DistanceUnitIndex"/> (free text).</summary>
    [ObservableProperty]
    public partial string Distance { get; set; } = string.Empty;

    /// <summary>0 = Meter, 1 = Kilometer, 2 = Feet, 3 = Yard, 4 = Mile (matches the unit picker order).</summary>
    [ObservableProperty]
    public partial int DistanceUnitIndex { get; set; }

    /// <summary>Projected destination as decimal degrees.</summary>
    [ObservableProperty]
    public partial string DecimalDegrees { get; set; } = string.Empty;

    /// <summary>Projected destination as degrees + decimal minutes (the geocaching default).</summary>
    [ObservableProperty]
    public partial string DegreesDecimalMinutes { get; set; } = string.Empty;

    /// <summary>Projected destination as degrees, minutes and seconds.</summary>
    [ObservableProperty]
    public partial string DegreesMinutesSeconds { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when a valid projection is shown (gates Copy/Share).</summary>
    [ObservableProperty]
    public partial bool HasResult { get; set; }

    /// <summary><see langword="true"/> when the current input cannot be projected (drives the error InfoBar).</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>Localized message describing why the input could not be projected.</summary>
    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    private DistanceUnit SelectedUnit => DistanceUnitIndex switch
    {
        1 => DistanceUnit.Kilometer,
        2 => DistanceUnit.Feet,
        3 => DistanceUnit.Yard,
        4 => DistanceUnit.Mile,
        _ => DistanceUnit.Meter,
    };

    partial void OnStartCoordinateChanged(string value) => Recompute();

    partial void OnAngleDegreesChanged(string value) => Recompute();

    partial void OnDistanceChanged(string value) => Recompute();

    partial void OnDistanceUnitIndexChanged(int value) => Recompute();

    partial void OnHasResultChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        // A blank form is the neutral state: no result, no error.
        if (string.IsNullOrWhiteSpace(StartCoordinate) &&
            string.IsNullOrWhiteSpace(AngleDegrees) &&
            string.IsNullOrWhiteSpace(Distance))
        {
            ClearResult();
            HasError = false;
            ErrorMessage = string.Empty;
            return;
        }

        if (!CoordinateParser.TryParse(StartCoordinate, out var start, out _))
        {
            Fail("CoordinateProjectionErrorStart");
            return;
        }

        if (!TryParseInvariant(AngleDegrees, out var angle))
        {
            Fail("CoordinateProjectionErrorAngle");
            return;
        }

        if (!TryParseInvariant(Distance, out var distanceValue))
        {
            Fail("CoordinateProjectionErrorDistance");
            return;
        }

        // A negative leg would silently project the opposite way — call it out instead.
        if (distanceValue < 0.0)
        {
            Fail("CoordinateProjectionErrorDistanceNegative");
            return;
        }

        var distanceMeters = DistanceUnits.ToMeters(distanceValue, SelectedUnit);
        var result = CoordinateProjection.Project(start, distanceMeters, angle);

        DecimalDegrees = result.DecimalDegrees;
        DegreesDecimalMinutes = result.DegreesDecimalMinutes;
        DegreesMinutesSeconds = result.DegreesMinutesSeconds;

        HasError = false;
        ErrorMessage = string.Empty;
        HasResult = true;
    }

    private void Fail(string messageKey)
    {
        ClearResult();
        ErrorMessage = _localizer[messageKey].Value;
        HasError = true;
    }

    private void ClearResult()
    {
        DecimalDegrees = string.Empty;
        DegreesDecimalMinutes = string.Empty;
        DegreesMinutesSeconds = string.Empty;
        HasResult = false;
    }

    private static bool TryParseInvariant(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>The text Copy/Share emit: all three formats labelled, DDM first (the default).</summary>
    private string BuildResultText() =>
        string.Join(
            Environment.NewLine,
            DegreesDecimalMinutes,
            DecimalDegrees,
            DegreesMinutesSeconds);

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyOutput() => _clipboard.SetText(DegreesDecimalMinutes);

    /// <summary>Copies a single result line (one format row's value).</summary>
    [RelayCommand]
    private void CopyText(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _clipboard.SetText(text);
        }
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildResultText());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    /// <summary>"Reset fields" parity: clears every input, including the unit picker.</summary>
    [RelayCommand]
    private void Clear()
    {
        StartCoordinate = string.Empty;
        AngleDegrees = string.Empty;
        Distance = string.Empty;
        DistanceUnitIndex = 0;
    }
}
