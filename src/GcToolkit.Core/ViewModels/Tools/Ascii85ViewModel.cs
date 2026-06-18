using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One variant's decode attempt shown in the "try all variants" panel.</summary>
public sealed record Ascii85VariantRow(string Variant, bool Success, string Text, string Hex, string Status);

/// <summary>
/// ASCII-85 / Base85 encoder &amp; decoder (issue #112). Converts live in either direction (Encrypt:
/// text→ASCII-85, Decrypt: ASCII-85→text) across four dialects — Adobe, btoa/Usenet, Z85 and RFC 1924 —
/// with an auto-detect, an Adobe <c>&lt;~ ~&gt;</c> delimiter toggle, byte/char counters and the ~25%
/// expansion ratio, a hex view of decoded bytes, whitespace-tolerant decoding with clear illegal-character
/// reporting, and a "try all variants" panel. All transform logic lives in the pure
/// <see cref="Ascii85Codec"/> (thin-VM convention).
/// </summary>
[Tool("Ascii85", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["ascii85", "ascii-85", "base85", "base-85", "adobe", "btoa", "z85", "rfc1924", "kódování"])]
public sealed partial class Ascii85ViewModel : ToolViewModelBase
{
    private readonly Ascii85Codec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressRecompute;

    public Ascii85ViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Ascii85", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = Encrypt (text → ASCII-85), 1 = Decrypt (ASCII-85 → text).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>0 = Adobe, 1 = btoa, 2 = Z85, 3 = RFC 1924 (matches <see cref="Ascii85Variant"/> order).</summary>
    [ObservableProperty]
    public partial int VariantIndex { get; set; }

    /// <summary>Wrap Adobe output in <c>&lt;~ ~&gt;</c>; only meaningful for the Adobe variant.</summary>
    [ObservableProperty]
    public partial bool UseAdobeDelimiters { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>Hex dump of the decoded bytes (Decrypt direction only); empty otherwise.</summary>
    [ObservableProperty]
    public partial string HexView { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowHexView { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Input/output size summary, e.g. <c>"13 chars → 16 chars (+23%)"</c>.</summary>
    [ObservableProperty]
    public partial string Stats { get; set; } = string.Empty;

    /// <summary>True only for the Adobe variant, to surface the delimiter toggle.</summary>
    [ObservableProperty]
    public partial bool IsAdobeVariant { get; set; } = true;

    /// <summary>Decode results for every variant, shown side by side when decrypting.</summary>
    public ObservableCollection<Ascii85VariantRow> AllVariantResults { get; } = [];

    [ObservableProperty]
    public partial bool ShowAllVariants { get; set; }

    private bool IsEncrypt => DirectionIndex != 1;

    private Ascii85Variant Variant => VariantIndex switch
    {
        1 => Ascii85Variant.Btoa,
        2 => Ascii85Variant.Z85,
        3 => Ascii85Variant.Rfc1924,
        _ => Ascii85Variant.Adobe,
    };

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is not (0 or 1))
        {
            return;
        }

        // Switching direction carries the previous result into the input for a one-tap round-trip.
        _suppressRecompute = true;
        InputText = OutputText;
        _suppressRecompute = false;
        Recompute();
    }

    partial void OnVariantIndexChanged(int value)
    {
        if (value is < 0 or > 3)
        {
            return;
        }

        IsAdobeVariant = Variant == Ascii85Variant.Adobe;
        Recompute();
    }

    partial void OnUseAdobeDelimitersChanged(bool value) => Recompute();

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnShowAllVariantsChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Auto-detects the variant from the current input and switches to it (Decrypt only).</summary>
    [RelayCommand]
    private void AutoDetect()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            return;
        }

        var detected = _codec.DetectVariant(InputText);
        VariantIndex = (int)detected;
    }

    private void Recompute()
    {
        ResetOutputs();

        if (string.IsNullOrEmpty(InputText))
        {
            return;
        }

        if (IsEncrypt)
        {
            Encrypt();
        }
        else
        {
            Decrypt();
        }
    }

    private void Encrypt()
    {
        Ascii85Options options = new()
        {
            UseAdobeDelimiters = UseAdobeDelimiters && Variant == Ascii85Variant.Adobe,
        };

        OutputText = _codec.EncodeText(InputText, Variant, options);
        HasOutput = OutputText.Length > 0;

        var inputBytes = System.Text.Encoding.UTF8.GetByteCount(InputText);
        Stats = FormatStats(inputBytes, _localizer["Ascii85UnitBytes"].Value, OutputText.Length, _localizer["Ascii85UnitChars"].Value);
    }

    private void Decrypt()
    {
        if (!_codec.TryDecodeToText(InputText, Variant, out var text, out var error))
        {
            HasError = true;
            ErrorMessage = LocalizeError(error);
            HasOutput = false;
        }
        else
        {
            OutputText = text;
            HasOutput = true;

            _codec.TryDecodeToBytes(InputText, Variant, out var bytes, out _);
            HexView = Ascii85Codec.ToHex(bytes);
            ShowHexView = bytes.Length > 0;

            Stats = FormatStats(InputText.Length, _localizer["Ascii85UnitChars"].Value, bytes.Length, _localizer["Ascii85UnitBytes"].Value);
        }

        BuildAllVariants();
    }

    private void BuildAllVariants()
    {
        AllVariantResults.Clear();
        if (!ShowAllVariants)
        {
            return;
        }

        foreach (var result in _codec.TryAllVariants(InputText))
        {
            var status = result.Success
                ? _localizer["Ascii85DecodeOk"].Value
                : _localizer["Ascii85DecodeFailed"].Value;

            AllVariantResults.Add(new Ascii85VariantRow(
                VariantName(result.Variant),
                result.Success,
                result.Text,
                result.Hex,
                status));
        }
    }

    private void ResetOutputs()
    {
        OutputText = string.Empty;
        HasOutput = false;
        HexView = string.Empty;
        ShowHexView = false;
        HasError = false;
        ErrorMessage = string.Empty;
        Stats = string.Empty;
        AllVariantResults.Clear();
    }

    private string FormatStats(int inCount, string inUnit, int outCount, string outUnit)
    {
        var ratio = inCount > 0 ? (double)(outCount - inCount) / inCount : 0;
        var sign = ratio >= 0 ? "+" : string.Empty;
        var percent = (ratio * 100).ToString("0", CultureInfo.CurrentCulture);
        return $"{inCount} {inUnit} → {outCount} {outUnit} ({sign}{percent}%)";
    }

    private string LocalizeError(Ascii85DecodeError? error)
    {
        if (error is null)
        {
            return _localizer["Ascii85InvalidInput"].Value;
        }

        if (error.Character == '\0')
        {
            return _localizer["Ascii85MalformedGroup"].Value;
        }

        var template = _localizer["Ascii85IllegalChar"].Value;
        return string.Format(CultureInfo.CurrentCulture, template, error.Character, error.Position);
    }

    private string VariantName(Ascii85Variant variant) => variant switch
    {
        Ascii85Variant.Btoa => _localizer["Ascii85VariantBtoa"].Value,
        Ascii85Variant.Z85 => _localizer["Ascii85VariantZ85"].Value,
        Ascii85Variant.Rfc1924 => _localizer["Ascii85VariantRfc1924"].Value,
        _ => _localizer["Ascii85VariantAdobe"].Value,
    };

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(OutputText);

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, OutputText);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
