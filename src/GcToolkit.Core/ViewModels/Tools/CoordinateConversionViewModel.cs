using System.Collections.ObjectModel;
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
/// Coordinate conversion (issue #4). Takes one coordinate as free text, auto-detects its notation via
/// <see cref="CoordinateParser"/>, and renders the same point simultaneously in every supported
/// notation — Decimal Degrees, Degrees Decimal Minutes, Degrees Minutes Seconds, UTM and MGRS — each
/// on its own copyable row. This beats the geocachingtoolbox.com reference (which converts only the
/// three angular notations and needs an explicit datum/grid choice) by auto-detecting the input,
/// showing all five formats at once with per-row copy, reporting which format was detected, and
/// offering copy-all / share. All conversion math lives in the foundation library (thin-VM convention).
/// </summary>
[Tool("CoordinateConversion", ToolCategory.Coordinates,
      Introduced = "2026-05-20", Updated = "2026-06-06",
      Keywords = ["wgs84", "utm", "mgrs", "convert", "notation", "dd", "ddm", "dms", "souřadnice", "převod", "gps", "formát"])]
public sealed partial class CoordinateConversionViewModel : ToolViewModelBase
{
    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public CoordinateConversionViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("CoordinateConversion", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary><see langword="true"/> once a valid coordinate has been parsed and the formats populated.</summary>
    [ObservableProperty]
    public partial bool HasResult { get; set; }

    /// <summary><see langword="true"/> when the current (non-empty) input could not be parsed.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>The notation auto-detected from the input (only meaningful while <see cref="HasResult"/>).</summary>
    [ObservableProperty]
    public partial CoordinateFormat DetectedFormat { get; set; }

    /// <summary>Resource key for the detected format's label — the single source of truth tests assert on.</summary>
    [ObservableProperty]
    public partial string DetectedFormatLabelKey { get; set; } = string.Empty;

    /// <summary>The detected format's localized display name — drives the "Detected notation" banner.</summary>
    [ObservableProperty]
    public partial string DetectedFormatLabel { get; set; } = string.Empty;

    /// <summary>The same point rendered in every supported notation, each row independently copyable.</summary>
    public ObservableCollection<CoordinateFormatRow> Results { get; } = [];

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnHasResultChanged(bool value)
    {
        CopyAllCommand.NotifyCanExecuteChanged();
        ShareCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        Results.Clear();

        if (string.IsNullOrWhiteSpace(InputText))
        {
            HasResult = false;
            HasError = false;
            DetectedFormatLabelKey = string.Empty;
            DetectedFormatLabel = string.Empty;
            return;
        }

        if (!CoordinateParser.TryParse(InputText, out var coordinate, out var detected))
        {
            HasResult = false;
            HasError = true;
            DetectedFormatLabelKey = string.Empty;
            DetectedFormatLabel = string.Empty;
            return;
        }

        DetectedFormat = detected;
        DetectedFormatLabelKey = CoordinateFormatResources.LabelKey(detected);
        DetectedFormatLabel = _localizer[DetectedFormatLabelKey].Value;

        foreach (var result in CoordinateConversions.ToAllFormats(coordinate))
        {
            var label = _localizer[CoordinateFormatResources.LabelKey(result.Format)].Value;
            var copyName = string.Format(_localizer["CoordConvCopyFormat"].Value, label);
            Results.Add(new CoordinateFormatRow(result.Format, label, result.Value, copyName, _clipboard.SetText));
        }

        HasError = false;
        HasResult = true;
    }

    /// <summary>The text Copy-all / Share emit: one <c>Label: value</c> line per format.</summary>
    private string BuildResultText()
        => string.Join(Environment.NewLine, Results.Select(r => $"{r.Label}: {r.Value}"));

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyAll() => _clipboard.SetText(BuildResultText());

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareAsync()
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

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
