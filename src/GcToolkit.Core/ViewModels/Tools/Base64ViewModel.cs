using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Numbers;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One row of the "try every variant" helper, shown in the UI.</summary>
public sealed partial class Base64VariantRow : ObservableObject
{
    public required string VariantName { get; init; }

    public required bool Success { get; init; }

    public required bool IsPrintable { get; init; }

    public required string Text { get; init; }

    public required int ByteCount { get; init; }
}

/// <summary>
/// Base64 converter (issue #122) — the most common encoding in geocaching mystery caches. Encodes and
/// decodes live with <b>auto-direction</b> detection (valid Base64 → decode, otherwise → encode),
/// across the Standard (RFC 4648), URL-safe, and MIME variants with a padding toggle and a selectable
/// plaintext encoding (UTF-8 / ASCII / Latin-1). Decoding is forgiving (whitespace, missing padding,
/// and URL-safe input "just work"), decoded bytes render as UTF-8 / Latin-1 / hex + byte-count, a
/// try-all-variants helper highlights which alphabet yields printable text, and a batch mode
/// encodes/decodes each line independently. All logic lives in the pure <see cref="Base64Codec"/>.
/// </summary>
[Tool("Base64", ToolCategory.Numbers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["base64", "base-64", "rfc 4648", "url-safe", "mime", "encode", "decode",
                  "kódování", "dekódování"])]
public sealed partial class Base64ViewModel : ToolViewModelBase
{
    private readonly Base64Codec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public Base64ViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Base64", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = Auto, 1 = Encode, 2 = Decode.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>0 = Standard, 1 = URL-safe, 2 = MIME (maps to <see cref="Base64Variant"/>).</summary>
    [ObservableProperty]
    public partial int VariantIndex { get; set; }

    /// <summary>0 = UTF-8, 1 = ASCII, 2 = Latin-1 (maps to <see cref="Base64TextEncoding"/>).</summary>
    [ObservableProperty]
    public partial int TextEncodingIndex { get; set; }

    /// <summary>0 = UTF-8, 1 = Latin-1, 2 = Hex (how decoded bytes are rendered).</summary>
    [ObservableProperty]
    public partial int DecodeRenderIndex { get; set; }

    [ObservableProperty]
    public partial bool UsePadding { get; set; } = true;

    /// <summary>When on, each input line is encoded/decoded independently.</summary>
    [ObservableProperty]
    public partial bool BatchMode { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The direction actually chosen for the current input (drives the "Encoded"/"Decoded" badge).</summary>
    [ObservableProperty]
    public partial string DirectionLabel { get; set; } = string.Empty;

    /// <summary>Byte count of the decoded payload (shown only when decoding succeeds).</summary>
    [ObservableProperty]
    public partial string ByteCountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowByteCount { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Rows of the "try all variants" helper (populated on demand).</summary>
    public ObservableCollection<Base64VariantRow> VariantResults { get; } = [];

    [ObservableProperty]
    public partial bool ShowVariantResults { get; set; }

    private Base64Variant Variant => (Base64Variant)VariantIndex;

    private Base64TextEncoding TextEncoding => (Base64TextEncoding)TextEncodingIndex;

    private Base64TextEncoding DecodeRender => DecodeRenderIndex switch
    {
        1 => Base64TextEncoding.Latin1,
        _ => Base64TextEncoding.Utf8,
    };

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is >= 0 and <= 2)
        {
            Recompute();
        }
    }

    partial void OnVariantIndexChanged(int value)
    {
        if (value is >= 0 and <= 2)
        {
            Recompute();
        }
    }

    partial void OnTextEncodingIndexChanged(int value)
    {
        if (value is >= 0 and <= 2)
        {
            Recompute();
        }
    }

    partial void OnDecodeRenderIndexChanged(int value)
    {
        if (value is >= 0 and <= 2)
        {
            Recompute();
        }
    }

    partial void OnUsePaddingChanged(bool value) => Recompute();

    partial void OnBatchModeChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        ResetResultState();

        if (string.IsNullOrEmpty(InputText))
        {
            return;
        }

        if (BatchMode)
        {
            RecomputeBatch();
            return;
        }

        RecomputeSingle(InputText);
    }

    private void RecomputeSingle(string input)
    {
        switch (DirectionIndex)
        {
            case 1: // Encode
                OutputText = _codec.Encode(input, Variant, UsePadding, TextEncoding);
                DirectionLabel = _localizer["Base64Encoded"].Value;
                HasOutput = OutputText.Length > 0;
                break;

            case 2: // Decode
                Decode(input);
                break;

            default: // Auto
                Auto(input);
                break;
        }
    }

    private void Auto(string input)
    {
        var op = _codec.AutoDirection(input, Variant, UsePadding, TextEncoding);
        OutputText = op.Output;
        HasOutput = op.Output.Length > 0;
        DirectionLabel = op.Direction switch
        {
            Base64Direction.Decoded => _localizer["Base64Decoded"].Value,
            Base64Direction.Encoded => _localizer["Base64Encoded"].Value,
            _ => string.Empty,
        };

        if (op.Direction == Base64Direction.Decoded && _codec.TryDecode(input, Variant, out var decoded))
        {
            SetByteCount(decoded.ByteCount);
        }
    }

    private void Decode(string input)
    {
        if (_codec.TryDecode(input, Variant, out var decoded, out var error))
        {
            OutputText = DecodeRenderIndex == 2 ? decoded.Hex : Base64Codec.Render(decoded, DecodeRender);
            DirectionLabel = _localizer["Base64Decoded"].Value;
            HasOutput = decoded.ByteCount > 0;
            SetByteCount(decoded.ByteCount);
        }
        else
        {
            HasError = true;
            ErrorMessage = string.IsNullOrEmpty(error) ? _localizer["Base64InvalidNotice"].Value : error;
        }
    }

    private void RecomputeBatch()
    {
        var lines = InputText.Replace("\r\n", "\n").Split('\n');
        var results = new List<string>(lines.Length);
        var anyError = false;

        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                results.Add(string.Empty);
                continue;
            }

            if (DirectionIndex == 2)
            {
                if (_codec.TryDecode(line, Variant, out var decoded))
                {
                    results.Add(DecodeRenderIndex == 2 ? decoded.Hex : Base64Codec.Render(decoded, DecodeRender));
                }
                else
                {
                    results.Add("?");
                    anyError = true;
                }
            }
            else if (DirectionIndex == 1)
            {
                results.Add(_codec.Encode(line, Variant, UsePadding, TextEncoding));
            }
            else
            {
                results.Add(_codec.AutoDirection(line, Variant, UsePadding, TextEncoding).Output);
            }
        }

        OutputText = string.Join("\n", results);
        HasOutput = results.Exists(static r => r.Length > 0);
        DirectionLabel = _localizer["Base64Batch"].Value;

        if (anyError)
        {
            HasError = true;
            ErrorMessage = _localizer["Base64BatchInvalidNotice"].Value;
        }
    }

    private void SetByteCount(int count)
    {
        ByteCountText = string.Format(_localizer["Base64ByteCount"].Value, count);
        ShowByteCount = true;
    }

    private void ResetResultState()
    {
        OutputText = string.Empty;
        HasOutput = false;
        HasError = false;
        ErrorMessage = string.Empty;
        DirectionLabel = string.Empty;
        ByteCountText = string.Empty;
        ShowByteCount = false;
        ShowVariantResults = false;
        VariantResults.Clear();
    }

    /// <summary>Decodes the input under every variant and lists which yields printable text.</summary>
    [RelayCommand]
    private void TryAllVariants()
    {
        VariantResults.Clear();

        if (string.IsNullOrWhiteSpace(InputText))
        {
            ShowVariantResults = false;
            return;
        }

        string[] names = [_localizer["Base64VariantStandard"].Value, _localizer["Base64VariantUrlSafe"].Value, _localizer["Base64VariantMime"].Value];

        foreach (var attempt in _codec.TryAllVariants(InputText))
        {
            VariantResults.Add(new Base64VariantRow
            {
                VariantName = names[(int)attempt.Variant],
                Success = attempt.Success,
                IsPrintable = attempt.IsPrintable,
                Text = attempt.Text,
                ByteCount = attempt.ByteCount,
            });
        }

        ShowVariantResults = VariantResults.Count > 0;
    }

    /// <summary>Loads a worked example (parity with cachesleuth's "Example" button).</summary>
    [RelayCommand]
    private void Example()
    {
        DirectionIndex = 1;
        VariantIndex = 0;
        UsePadding = true;
        BatchMode = false;
        InputText = "N 49 12.345 E 016 34.567";
    }

    /// <summary>Resets every option and the input to defaults (parity with cachesleuth's "Reset").</summary>
    [RelayCommand]
    private void Reset()
    {
        DirectionIndex = 0;
        VariantIndex = 0;
        TextEncodingIndex = 0;
        DecodeRenderIndex = 0;
        UsePadding = true;
        BatchMode = false;
        InputText = string.Empty;
    }

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
