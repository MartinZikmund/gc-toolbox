using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Resistor colour-code tool (issue #55). Decodes 4-, 5- and 6-band colour sequences into resistance,
/// tolerance and (6-band) temperature coefficient, and — beyond the geocachingtoolbox.com tool, which
/// only decodes — encodes a resistance value (with band count and tolerance) back into colour bands.
/// All electrical logic lives in the pure <see cref="ResistorCode"/>; this VM only wires it to bindings.
/// </summary>
[Tool("ResistorCode", ToolCategory.Alphabets,
      Introduced = "2026-06-06", Updated = "2026-06-06",
      Keywords = ["resistor", "resistance", "ohm", "colour", "color", "band", "tolerance",
                  "rezistor", "odpor", "ohm", "barva", "barevný", "pásek", "tolerance", "elektronika"])]
public sealed partial class ResistorCodeViewModel : ToolViewModelBase
{
    private readonly ResistorCode _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressRecompute;

    public ResistorCodeViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("ResistorCode", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        BuildToleranceOptions();
        BuildTempCoOptions();
        RebuildBands();
        Recompute();
    }

    /// <summary>0 = Decode (colours → value), 1 = Encode (value → colours).</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    /// <summary>0 = 4-band, 1 = 5-band, 2 = 6-band.</summary>
    [ObservableProperty]
    public partial int BandCountIndex { get; set; }

    // ---- Decode side ----

    /// <summary>The per-band colour selectors shown in decode mode (rebuilt when the band count changes).</summary>
    public ObservableCollection<ResistorBandSlot> Bands { get; } = [];

    // ---- Encode side ----

    [ObservableProperty]
    public partial string ResistanceInput { get; set; } = string.Empty;

    /// <summary>Tolerance choices for encode mode (one per tolerance colour).</summary>
    public ObservableCollection<ResistorColorOption> ToleranceOptions { get; } = [];

    [ObservableProperty]
    public partial ResistorColorOption? SelectedTolerance { get; set; }

    /// <summary>Temperature-coefficient choices for encode mode (6-band only).</summary>
    public ObservableCollection<ResistorColorOption> TempCoOptions { get; } = [];

    [ObservableProperty]
    public partial ResistorColorOption? SelectedTempCo { get; set; }

    /// <summary>The colour bands produced by encoding (chips shown left→right).</summary>
    public ObservableCollection<ResistorColorOption> ResultBands { get; } = [];

    // ---- Shared output ----

    [ObservableProperty]
    public partial string ResistanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ToleranceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TempCoText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTempCo { get; set; }

    /// <summary>The significant digits as they read off the bands (brown-black-red → <c>102</c>) — what cache puzzles want.</summary>
    [ObservableProperty]
    public partial string DigitsText { get; set; } = string.Empty;

    /// <summary>Set when the entered value has no exact band representation and the nearest one is shown.</summary>
    [ObservableProperty]
    public partial bool IsApproximate { get; set; }

    [ObservableProperty]
    public partial string ApproximateMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public bool IsDecodeMode => ModeIndex == 0;

    public bool IsEncodeMode => ModeIndex == 1;

    public bool IsSixBand => BandCountIndex == 2;

    private ResistorBandCount BandCount => BandCountIndex switch
    {
        1 => ResistorBandCount.Five,
        2 => ResistorBandCount.Six,
        _ => ResistorBandCount.Four,
    };

    partial void OnModeIndexChanged(int value)
    {
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        OnPropertyChanged(nameof(IsDecodeMode));
        OnPropertyChanged(nameof(IsEncodeMode));

        // Carry the previous result across the toggle so a round-trip is one tap.
        _suppressRecompute = true;
        if (value == 1)
        {
            CarryDecodedValueIntoEncodeInput();
        }
        else
        {
            CarryEncodedBandsIntoSelectors();
        }

        _suppressRecompute = false;
        Recompute();
    }

    /// <summary>Decode → encode: seed the value box (and tolerance) from the bands the user had selected.</summary>
    private void CarryDecodedValueIntoEncodeInput()
    {
        var colors = Bands.Select(b => b.SelectedColor).ToArray();
        var decoded = _codec.Decode(colors);
        if (!decoded.Success)
        {
            return;
        }

        ResistanceInput = decoded.Value!.Resistance.ToString("0.############", CultureInfo.InvariantCulture);

        var toleranceColor = colors[ResistorCode.DigitBandCount(BandCount) + 1];
        SelectedTolerance = ToleranceOptions.FirstOrDefault(o => o.Color == toleranceColor) ?? SelectedTolerance;
        if (IsSixBand)
        {
            var tempCoColor = colors[5];
            SelectedTempCo = TempCoOptions.FirstOrDefault(o => o.Color == tempCoColor) ?? SelectedTempCo;
        }
    }

    /// <summary>Encode → decode: seed the band selectors from the colours the encoder produced.</summary>
    private void CarryEncodedBandsIntoSelectors()
    {
        if (ResultBands.Count != Bands.Count)
        {
            return;
        }

        for (var i = 0; i < Bands.Count; i++)
        {
            var slot = Bands[i];
            var match = slot.Options.FirstOrDefault(o => o.Color == ResultBands[i].Color);
            if (match is not null)
            {
                slot.SelectedOption = match;
            }
        }
    }

    partial void OnBandCountIndexChanged(int value)
    {
        if (value is not (0 or 1 or 2))
        {
            return;
        }

        OnPropertyChanged(nameof(IsSixBand));
        RebuildBands();
        Recompute();
    }

    partial void OnResistanceInputChanged(string value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnSelectedToleranceChanged(ResistorColorOption? value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnSelectedTempCoChanged(ResistorColorOption? value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Rebuilds the decode band selectors for the active band count, preserving any still-valid colours.</summary>
    private void RebuildBands()
    {
        var previous = Bands.Select(b => b.SelectedColor).ToList();
        Bands.Clear();

        var count = BandCount;
        var digitBands = ResistorCode.DigitBandCount(count);
        var totalBands = (int)count;

        var digitOptions = OptionsFor(ResistorCode.DigitColors);
        var multiplierOptions = OptionsFor(ResistorCode.MultiplierColors);
        var toleranceOptions = OptionsFor(ResistorCode.ToleranceColors);
        var tempCoOptions = OptionsFor(ResistorCode.TemperatureCoefficientColors);

        for (var i = 0; i < totalBands; i++)
        {
            var (role, options, fallbackColor) = BandLayout(i, digitBands, count, digitOptions, multiplierOptions, toleranceOptions, tempCoOptions);
            var selected = i < previous.Count && options.Any(o => o.Color == previous[i])
                ? IndexOfColor(options, previous[i])
                : IndexOfColor(options, fallbackColor);

            Bands.Add(new ResistorBandSlot(role, options, selected, OnBandChanged));
        }
    }

    private (string Role, IReadOnlyList<ResistorColorOption> Options, ResistorColor Fallback) BandLayout(
        int index,
        int digitBands,
        ResistorBandCount count,
        IReadOnlyList<ResistorColorOption> digitOptions,
        IReadOnlyList<ResistorColorOption> multiplierOptions,
        IReadOnlyList<ResistorColorOption> toleranceOptions,
        IReadOnlyList<ResistorColorOption> tempCoOptions)
    {
        if (index < digitBands)
        {
            // Default the digits to 4,7 / 4,7,0 so the initial preview is a recognizable value.
            var fallback = index == 0 ? ResistorColor.Yellow : index == 1 ? ResistorColor.Violet : ResistorColor.Black;
            return (string.Format(CultureInfo.CurrentCulture, _localizer["ResistorBandDigit"].Value, index + 1), digitOptions, fallback);
        }

        if (index == digitBands)
        {
            return (_localizer["ResistorBandMultiplier"].Value, multiplierOptions, ResistorColor.Brown);
        }

        if (index == digitBands + 1)
        {
            return (_localizer["ResistorBandTolerance"].Value, toleranceOptions, ResistorColor.Gold);
        }

        return (_localizer["ResistorBandTempCo"].Value, tempCoOptions, ResistorColor.Brown);
    }

    private void OnBandChanged()
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    private void Recompute()
    {
        if (IsEncodeMode)
        {
            RecomputeEncode();
        }
        else
        {
            RecomputeDecode();
        }
    }

    private void RecomputeDecode()
    {
        ResultBands.Clear();

        var colors = Bands.Select(b => b.SelectedColor).ToArray();
        var result = _codec.Decode(colors);
        if (!result.Success)
        {
            ShowError(result.Error);
            return;
        }

        // The preview mirrors the selected bands so decode mode gets the same visual resistor as encode.
        foreach (var color in colors)
        {
            ResultBands.Add(Option(color));
        }

        ApplyValue(result.Value!);
        DigitsText = ResistorCode.DigitString(colors);
    }

    private void RecomputeEncode()
    {
        ResultBands.Clear();

        if (string.IsNullOrWhiteSpace(ResistanceInput))
        {
            ClearOutput();
            return;
        }

        if (!TryParseResistance(ResistanceInput, out var ohms))
        {
            ShowError(ResistorError.UnrepresentableValue);
            return;
        }

        var tolerance = SelectedTolerance?.Color ?? ResistorColor.Gold;
        var tolerancePercent = ResistorCode.ToleranceValue(tolerance) ?? 5m;
        int? tempCo = IsSixBand ? ResistorCode.TemperatureCoefficientValue(SelectedTempCo?.Color ?? ResistorColor.Brown) : null;

        var result = _codec.Encode(ohms, BandCount, tolerancePercent, tempCo);
        if (!result.Success)
        {
            ShowError(result.Error);
            return;
        }

        foreach (var color in result.Bands!)
        {
            ResultBands.Add(Option(color));
        }

        // Re-decode the produced bands so the shown value/tolerance reflect the rounded representation.
        var decoded = _codec.Decode(result.Bands);
        if (decoded.Success)
        {
            ApplyValue(decoded.Value!);
            DigitsText = ResistorCode.DigitString(result.Bands);

            if (result.IsApproximate)
            {
                IsApproximate = true;
                ApproximateMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    _localizer["ResistorApproximate"].Value,
                    ResistorCode.FormatResistance(decoded.Value!.Resistance));
            }
        }
    }

    private void ApplyValue(ResistorValue value)
    {
        ResistanceText = ResistorCode.FormatResistance(value.Resistance);
        ToleranceText = FormatTolerance(value.TolerancePercent);
        if (value.TemperatureCoefficient is int ppm)
        {
            HasTempCo = true;
            TempCoText = string.Format(CultureInfo.CurrentCulture, _localizer["ResistorTempCoValue"].Value, ppm);
        }
        else
        {
            HasTempCo = false;
            TempCoText = string.Empty;
        }

        HasOutput = true;
        HasError = false;
        ErrorMessage = string.Empty;
        IsApproximate = false;
        ApproximateMessage = string.Empty;
    }

    private void ShowError(ResistorError error)
    {
        ClearOutput();
        HasError = true;
        ErrorMessage = _localizer[ErrorKey(error)].Value;
    }

    private void ClearOutput()
    {
        ResistanceText = string.Empty;
        ToleranceText = string.Empty;
        TempCoText = string.Empty;
        DigitsText = string.Empty;
        HasTempCo = false;
        HasOutput = false;
        HasError = false;
        ErrorMessage = string.Empty;
        IsApproximate = false;
        ApproximateMessage = string.Empty;
    }

    private string FormatTolerance(decimal percent)
    {
        var number = percent.ToString("0.###", CultureInfo.CurrentCulture);
        return string.Format(CultureInfo.CurrentCulture, _localizer["ResistorToleranceValue"].Value, number);
    }

    private void BuildToleranceOptions()
    {
        foreach (var color in ResistorCode.ToleranceColors)
        {
            ToleranceOptions.Add(Option(color));
        }

        // Default to ±5% (gold), the most common through-hole tolerance.
        SelectedTolerance = ToleranceOptions.FirstOrDefault(o => o.Color == ResistorColor.Gold) ?? ToleranceOptions.FirstOrDefault();
    }

    private void BuildTempCoOptions()
    {
        foreach (var color in ResistorCode.TemperatureCoefficientColors)
        {
            TempCoOptions.Add(Option(color));
        }

        SelectedTempCo = TempCoOptions.FirstOrDefault(o => o.Color == ResistorColor.Brown) ?? TempCoOptions.FirstOrDefault();
    }

    private IReadOnlyList<ResistorColorOption> OptionsFor(IReadOnlyList<ResistorColor> colors)
        => [.. colors.Select(Option)];

    private ResistorColorOption Option(ResistorColor color) => new(color, ColorName(color));

    private string ColorName(ResistorColor color) => _localizer[$"ResistorColor_{color}"].Value;

    private static int IndexOfColor(IReadOnlyList<ResistorColorOption> options, ResistorColor color)
    {
        for (var i = 0; i < options.Count; i++)
        {
            if (options[i].Color == color)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Parses a resistance entered as plain ohms or with a k / M / G suffix (e.g. <c>4k7</c> is not supported; <c>4.7k</c> is).</summary>
    private static bool TryParseResistance(string text, out decimal ohms)
    {
        ohms = 0m;
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        // Strip an optional trailing ohm sign and unit, then a single SI prefix.
        trimmed = trimmed.TrimEnd('Ω', 'ω', 'R', 'r').Trim();

        decimal scale = 1m;
        if (trimmed.Length > 0)
        {
            switch (trimmed[^1])
            {
                case 'k' or 'K':
                    scale = 1_000m;
                    trimmed = trimmed[..^1].Trim();
                    break;
                case 'M':
                    scale = 1_000_000m;
                    trimmed = trimmed[..^1].Trim();
                    break;
                case 'G':
                    scale = 1_000_000_000m;
                    trimmed = trimmed[..^1].Trim();
                    break;
            }
        }

        // Accept both '.' and ',' as the decimal separator for convenience.
        trimmed = trimmed.Replace(',', '.');
        if (decimal.TryParse(trimmed, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var magnitude))
        {
            ohms = magnitude * scale;
            return true;
        }

        return false;
    }

    private static string ErrorKey(ResistorError error) => error switch
    {
        ResistorError.WrongBandCount => "ResistorErrorBandCount",
        ResistorError.InvalidDigitBand => "ResistorErrorDigitBand",
        ResistorError.InvalidMultiplierBand => "ResistorErrorMultiplierBand",
        ResistorError.InvalidToleranceBand => "ResistorErrorToleranceBand",
        ResistorError.InvalidTemperatureCoefficientBand => "ResistorErrorTempCoBand",
        ResistorError.UnrepresentableTolerance => "ResistorErrorTolerance",
        ResistorError.UnrepresentableTemperatureCoefficient => "ResistorErrorTempCo",
        ResistorError.ValueOutOfRange => "ResistorErrorValueRange",
        _ => "ResistorErrorValue",
    };

    /// <summary>A one-line summary of the current result for copy/share.</summary>
    private string BuildResultText()
    {
        var parts = new List<string> { ResistanceText };
        if (!string.IsNullOrEmpty(ToleranceText))
        {
            parts.Add(ToleranceText);
        }

        if (HasTempCo && !string.IsNullOrEmpty(TempCoText))
        {
            parts.Add(TempCoText);
        }

        if (!string.IsNullOrEmpty(DigitsText))
        {
            parts.Add(DigitsText);
        }

        return string.Join(", ", parts);
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(BuildResultText());

    [RelayCommand(CanExecute = nameof(HasOutput))]
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

    [RelayCommand]
    private void Clear()
    {
        if (IsEncodeMode)
        {
            ResistanceInput = string.Empty;
        }
        else
        {
            _suppressRecompute = true;
            RebuildBands();
            _suppressRecompute = false;
            Recompute();
        }
    }
}
