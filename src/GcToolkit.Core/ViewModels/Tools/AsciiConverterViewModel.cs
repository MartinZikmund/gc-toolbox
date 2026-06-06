using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Text;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// ASCII / Unicode code converter (issue #7). Converts live as the user types in either direction:
/// text → character codes (shown in decimal, hexadecimal, octal and binary at once) and codes → text.
/// Goes beyond geocachingtoolbox.com parity by covering full Unicode code points (not just 0–255), all
/// four bases simultaneously, a configurable separator, and base auto-detection when decoding. All
/// conversion logic lives in the pure <see cref="AsciiConverter"/> (thin-VM convention).
/// </summary>
[Tool("AsciiConverter", ToolCategory.Text,
      Introduced = "2026-06-06", Updated = "2026-06-06",
      Keywords = ["ascii", "unicode", "code", "codes", "character", "decimal", "hex", "hexadecimal", "octal", "binary", "kód", "kódy", "znak", "převod", "ascii kód"])]
public sealed partial class AsciiConverterViewModel : ToolViewModelBase
{
    private readonly AsciiConverter _converter = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressConvert;

    public AsciiConverterViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("AsciiConverter", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = Text → codes, 1 = codes → Text.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>The decode base: 0 = Auto-detect, 1 = Decimal, 2 = Hex, 3 = Octal, 4 = Binary.</summary>
    [ObservableProperty]
    public partial int DecodeBaseIndex { get; set; }

    /// <summary>The separator inserted between encoded codes (and tolerated, with any whitespace, when decoding).</summary>
    [ObservableProperty]
    public partial string Separator { get; set; } = AsciiConverter.DefaultSeparator;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The primary result shown in the output box (codes when encoding, text when decoding).</summary>
    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when decoding could not parse the input under the chosen base.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when encoding (drives visibility of the per-base breakdown table).</summary>
    [ObservableProperty]
    public partial bool IsEncoding { get; set; } = true;

    /// <summary><see langword="true"/> when the decode base selector is shown (decoding only).</summary>
    [ObservableProperty]
    public partial bool ShowDecodeBase { get; set; }

    /// <summary>When auto-detecting on decode, the base that was actually used — surfaced as a hint.</summary>
    [ObservableProperty]
    public partial string DetectedBaseName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowDetectedBase { get; set; }

    /// <summary>The per-code-point breakdown shown while encoding: each character in all four bases.</summary>
    public ObservableCollection<AsciiCodeRow> Breakdown { get; } = [];

    private bool IsDecoding => DirectionIndex == 1;

    private AsciiNumberBase SelectedDecodeBase => DecodeBaseIndex switch
    {
        2 => AsciiNumberBase.Hexadecimal,
        3 => AsciiNumberBase.Octal,
        4 => AsciiNumberBase.Binary,
        _ => AsciiNumberBase.Decimal,
    };

    private bool IsAutoDetect => DecodeBaseIndex == 0;

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit while re-templating.
        if (value is not (0 or 1))
        {
            return;
        }

        // Carry the previous result into the input so a round-trip is one tap.
        _suppressConvert = true;
        InputText = OutputText;
        _suppressConvert = false;
        Convert();
    }

    partial void OnDecodeBaseIndexChanged(int value)
    {
        if (value is >= 0 and <= 4)
        {
            Convert();
        }
    }

    partial void OnSeparatorChanged(string value) => Convert();

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        IsEncoding = !IsDecoding;
        ShowDecodeBase = IsDecoding;
        Breakdown.Clear();
        ShowDetectedBase = false;
        DetectedBaseName = string.Empty;

        if (string.IsNullOrEmpty(InputText))
        {
            ResetOutput();
            return;
        }

        if (IsDecoding)
        {
            Decode();
        }
        else
        {
            Encode();
        }
    }

    private void Encode()
    {
        var separator = string.IsNullOrEmpty(Separator) ? string.Empty : Separator;
        OutputText = _converter.Encode(InputText, AsciiNumberBase.Decimal, separator);

        foreach (var row in _converter.Describe(InputText))
        {
            Breakdown.Add(row);
        }

        HasError = false;
        ErrorMessage = string.Empty;
        HasOutput = OutputText.Length > 0;
    }

    private void Decode()
    {
        bool ok;
        string text;
        if (IsAutoDetect)
        {
            ok = _converter.TryDecodeAuto(InputText, out text, out var detected);
            if (ok)
            {
                DetectedBaseName = BaseDisplayName(detected);
                ShowDetectedBase = true;
            }
        }
        else
        {
            ok = _converter.TryDecode(InputText, SelectedDecodeBase, out text);
        }

        if (ok)
        {
            OutputText = text;
            HasError = false;
            ErrorMessage = string.Empty;
            HasOutput = text.Length > 0;
        }
        else
        {
            OutputText = string.Empty;
            HasOutput = false;
            ShowDetectedBase = false;
            HasError = true;
            ErrorMessage = _localizer["AsciiDecodeError"].Value;
        }
    }

    private void ResetOutput()
    {
        OutputText = string.Empty;
        HasOutput = false;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private string BaseDisplayName(AsciiNumberBase numberBase) => numberBase switch
    {
        AsciiNumberBase.Hexadecimal => _localizer["AsciiBaseHex"].Value,
        AsciiNumberBase.Octal => _localizer["AsciiBaseOctal"].Value,
        AsciiNumberBase.Binary => _localizer["AsciiBaseBinary"].Value,
        _ => _localizer["AsciiBaseDecimal"].Value,
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
