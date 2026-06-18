using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Numbers;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Reflected binary (Gray) code converter (issue #194). Converts live as the user types in any of four
/// directions, handles multi-line batch input, an A–Z letter mode, configurable bit width and output base,
/// and a step-by-step XOR-cascade view for a single value. Pure bit math via <see cref="GrayCodeCodec"/>,
/// so it works fully offline; copies/shares the aligned result.
/// </summary>
[Tool("GrayCode", ToolCategory.Numbers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["gray", "graycode", "reflected binary", "reflected", "xor", "bits", "binary", "šedý kód", "reflexní binární"])]
public sealed partial class GrayCodeViewModel : ToolViewModelBase
{
    private const string InvalidToken = "?";

    private readonly GrayCodeCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public GrayCodeViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("GrayCode", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        BuildReferenceTable();
    }

    /// <summary>0 = Value→Gray (encode), 1 = Gray→Value (decode).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Index into <see cref="NumberRadix"/> choices: 0 Decimal, 1 Binary, 2 Octal, 3 Hex.</summary>
    [ObservableProperty]
    public partial int InputRadixIndex { get; set; }

    [ObservableProperty]
    public partial int OutputRadixIndex { get; set; }

    /// <summary>0 auto, 1 → 4 bits, 2 → 5, 3 → 8, 4 → 16.</summary>
    [ObservableProperty]
    public partial int BitWidthIndex { get; set; }

    /// <summary>When on, each input line is a letter (A–Z) mapped to/from 1–26 through Gray code.</summary>
    [ObservableProperty]
    public partial bool LetterMode { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when a single value is shown with its XOR-cascade <see cref="CascadeLines"/>.</summary>
    [ObservableProperty]
    public partial bool ShowCascade { get; set; }

    /// <summary>The encode/decode label shown above the result (changes with direction).</summary>
    [ObservableProperty]
    public partial string ResultLabel { get; set; } = string.Empty;

    /// <summary>The per-step XOR cascade lines for a single value (value bits, value&gt;&gt;1 bits, gray bits).</summary>
    public ObservableCollection<string> CascadeLines { get; } = [];

    /// <summary>The first 16 Gray codes (decimal / 4-bit binary / 4-bit gray) for the inline reference table.</summary>
    public ObservableCollection<GrayReferenceRow> ReferenceRows { get; } = [];

    private bool IsEncode => DirectionIndex != 1;

    private NumberRadix InputRadix => RadixFor(InputRadixIndex);

    private NumberRadix OutputRadix => RadixFor(OutputRadixIndex);

    private int BitWidth => BitWidthIndex switch
    {
        1 => 4,
        2 => 5,
        3 => 8,
        4 => 16,
        _ => 0,
    };

    partial void OnInputTextChanged(string value) => Convert();

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is not (0 or 1))
        {
            return;
        }

        Convert();
    }

    partial void OnInputRadixIndexChanged(int value) => Convert();

    partial void OnOutputRadixIndexChanged(int value) => Convert();

    partial void OnBitWidthIndexChanged(int value) => Convert();

    partial void OnLetterModeChanged(bool value) => Convert();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        CascadeLines.Clear();
        ShowCascade = false;
        ResultLabel = _localizer[IsEncode ? "GrayResultGrayLabel" : "GrayResultValueLabel"].Value;

        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = false;
            WarningMessage = string.Empty;
            return;
        }

        // One value per line; blank lines pass through so columns stay aligned with the input.
        var lines = InputText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var results = new List<string>(lines.Length);
        var anyValid = false;
        var anyInvalid = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                results.Add(string.Empty);
                continue;
            }

            if (TryConvertLine(line, out var result))
            {
                results.Add(result);
                anyValid = true;
            }
            else
            {
                results.Add(InvalidToken);
                anyInvalid = true;
            }
        }

        OutputText = string.Join('\n', results);
        HasOutput = anyValid;
        HasWarning = anyInvalid;
        WarningMessage = anyInvalid ? _localizer["GrayInvalidNotice"].Value : string.Empty;

        // The XOR cascade only makes sense for a single, valid, numeric value (not letter mode).
        if (!LetterMode && anyValid && !anyInvalid && CountNonEmpty(lines) == 1)
        {
            BuildCascade(lines);
        }
    }

    private bool TryConvertLine(string line, out string result)
    {
        result = string.Empty;

        if (LetterMode)
        {
            return TryConvertLetters(line, out result);
        }

        if (!_codec.TryParseAuto(line, InputRadix, out var value, out _))
        {
            return false;
        }

        var output = IsEncode ? _codec.Encode(value) : _codec.Decode(value);
        result = _codec.Format(output, OutputRadix, BitWidth);
        return true;
    }

    /// <summary>Letter mode: each letter is Gray-en/decoded through its A=1..Z=26 position; other characters pass through.</summary>
    private bool TryConvertLetters(string line, out string result)
    {
        var builder = new StringBuilder(line.Length);
        var anyLetter = false;

        foreach (var c in line)
        {
            if (char.IsLetter(c))
            {
                anyLetter = true;
                var converted = IsEncode
                    ? _codec.TryEncodeLetterToLetter(c, out var encoded) ? encoded : (char?)null
                    : _codec.TryDecodeLetterToLetter(c, out var decoded) ? decoded : (char?)null;

                builder.Append(converted ?? c);
            }
            else
            {
                builder.Append(c);
            }
        }

        result = builder.ToString();
        return anyLetter;
    }

    private void BuildCascade(IEnumerable<string> lines)
    {
        var only = lines.First(l => !string.IsNullOrWhiteSpace(l)).Trim();
        if (!_codec.TryParseAuto(only, InputRadix, out var value, out _))
        {
            return;
        }

        // Always explain the encode transform for the underlying value, regardless of direction.
        var source = IsEncode ? value : _codec.Decode(value);
        var steps = _codec.ExplainEncode(source, BitWidth);

        CascadeLines.Add($"{_localizer["GrayCascadeValue"].Value}  {steps.ValueBits}");
        CascadeLines.Add($"{_localizer["GrayCascadeShift"].Value}  {steps.ShiftedBits}");
        CascadeLines.Add($"{_localizer["GrayCascadeGray"].Value}  {steps.GrayBits}");
        ShowCascade = true;
    }

    private void BuildReferenceTable()
    {
        for (ulong v = 0; v < 16; v++)
        {
            ReferenceRows.Add(new GrayReferenceRow(
                v.ToString(),
                _codec.Format(v, NumberRadix.Binary, 4),
                _codec.EncodeToBinary(v, 4)));
        }
    }

    private static int CountNonEmpty(IEnumerable<string> lines)
        => lines.Count(l => !string.IsNullOrWhiteSpace(l));

    private static NumberRadix RadixFor(int index) => index switch
    {
        1 => NumberRadix.Binary,
        2 => NumberRadix.Octal,
        3 => NumberRadix.Hexadecimal,
        _ => NumberRadix.Decimal,
    };

    [RelayCommand]
    private void LoadExample()
    {
        InputRadixIndex = 0;
        OutputRadixIndex = 1;
        DirectionIndex = 0;
        LetterMode = false;
        InputText = "0\n1\n2\n3\n4\n5\n6\n7";
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

/// <summary>One row of the inline Gray-code reference table.</summary>
public readonly record struct GrayReferenceRow(string Decimal, string Binary, string Gray);
