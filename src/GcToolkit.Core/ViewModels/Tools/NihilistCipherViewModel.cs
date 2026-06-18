using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Nihilist cipher (issue #235). Encodes text into a number stream via a Polybius-keyword-seeded square
/// plus a repeating additive keyword, and decodes the stream back. Recomputes live as the user types or
/// toggles the grid size (5×5 / 6×6), orientation (row-column / column-row) and cell base (1 / 0). Renders
/// the generated square, shows the plain+key=cipher breakdown, and offers encode/decode swap, copy, share
/// and clear. All transform logic lives in the pure <see cref="NihilistCipher"/> (thin-VM convention);
/// fully offline.
/// </summary>
[Tool("NihilistCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["nihilist", "polybius", "additive", "number", "cipher", "šifra", "mřížka"])]
public sealed partial class NihilistCipherViewModel : ToolViewModelBase
{
    private readonly NihilistCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public NihilistCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("NihilistCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Recompute();
    }

    /// <summary>0 = Encode (text → numbers), 1 = Decode (numbers → text).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string PolybiusKeyword { get; set; } = "ZEBRAS";

    [ObservableProperty]
    public partial string AdditiveKeyword { get; set; } = "RUSSIAN";

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>0 = 5×5 (I/J merged), 1 = 6×6 (letters + digits).</summary>
    [ObservableProperty]
    public partial int GridSizeIndex { get; set; }

    /// <summary>0 = row-column (classic), 1 = column-row.</summary>
    [ObservableProperty]
    public partial int OrientationIndex { get; set; }

    /// <summary><see langword="true"/> counts cells from 0; default counts from 1.</summary>
    [ObservableProperty]
    public partial bool IsZeroBased { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>The rendered Polybius square cells, in reading order (row by row).</summary>
    public ObservableCollection<NihilistSquareCell> SquareCells { get; } = [];

    /// <summary>The square's axis labels (row/column headers).</summary>
    public ObservableCollection<int> AxisLabels { get; } = [];

    /// <summary>The plain + key = cipher breakdown lines for the current transform.</summary>
    public ObservableCollection<string> BreakdownLines { get; } = [];

    [ObservableProperty]
    public partial bool ShowBreakdown { get; set; }

    /// <summary>The side length of the active square (5 or 6) — drives the grid column count.</summary>
    [ObservableProperty]
    public partial int SquareSize { get; set; } = 5;

    /// <summary>Localized header for the input field, reflecting the current direction.</summary>
    public string InputLabel => IsDecode
        ? _localizer["NihilistNumbersLabel"].Value
        : _localizer["NihilistTextLabel"].Value;

    /// <summary>Localized header for the output field, reflecting the current direction.</summary>
    public string OutputLabel => IsDecode
        ? _localizer["NihilistTextLabel"].Value
        : _localizer["NihilistNumbersLabel"].Value;

    private bool IsDecode => DirectionIndex == 1;

    private NihilistOptions Options => new(
        Size: GridSizeIndex == 1 ? 6 : 5,
        Orientation: OrientationIndex == 1 ? NihilistOrientation.ColumnRow : NihilistOrientation.RowColumn,
        ZeroBased: IsZeroBased);

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            OnPropertyChanged(nameof(InputLabel));
            OnPropertyChanged(nameof(OutputLabel));
            Recompute();
        }
    }

    partial void OnPolybiusKeywordChanged(string value) => Recompute();

    partial void OnAdditiveKeywordChanged(string value) => Recompute();

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnGridSizeIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnOrientationIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnIsZeroBasedChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        var options = Options;
        SquareSize = options.Size;

        RenderSquare(options);

        BreakdownLines.Clear();
        ShowBreakdown = false;

        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasError = false;
            ErrorMessage = string.Empty;
            return;
        }

        try
        {
            var result = IsDecode
                ? _cipher.Decrypt(InputText, PolybiusKeyword, AdditiveKeyword, options)
                : _cipher.Encrypt(InputText, PolybiusKeyword, AdditiveKeyword, options);

            OutputText = result.Text;
            HasOutput = !string.IsNullOrEmpty(result.Text);
            HasError = false;
            ErrorMessage = string.Empty;

            foreach (var step in result.Steps)
            {
                var sign = step.CipherValue >= step.PlainValue ? "+" : "−";
                BreakdownLines.Add(
                    $"{step.Symbol} ({step.PlainValue}) {sign} {step.KeyValue} = {step.CipherValue}");
            }

            ShowBreakdown = BreakdownLines.Count > 0;
        }
        catch (NihilistCipherException ex)
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }

    private void RenderSquare(NihilistOptions options)
    {
        SquareCells.Clear();
        AxisLabels.Clear();

        try
        {
            foreach (var label in _cipher.AxisLabels(options))
            {
                AxisLabels.Add(label);
            }

            var rows = _cipher.SquareRows(PolybiusKeyword, options);
            var start = options.Base;
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < rows[r].Length; c++)
                {
                    var coordinate = options.Orientation == NihilistOrientation.ColumnRow
                        ? (c + start) * 10 + (r + start)
                        : (r + start) * 10 + (c + start);
                    SquareCells.Add(new NihilistSquareCell(rows[r][c].ToString(), coordinate.ToString()));
                }
            }
        }
        catch (NihilistCipherException)
        {
            // A bad Polybius keyword surfaces through Recompute's error path; leave the square empty.
        }
        catch (ArgumentException)
        {
            // Invalid options (e.g. custom alphabet length) — leave the square empty.
        }
    }

    [RelayCommand]
    private void SwapDirection()
    {
        // Carry the current output into the input so a round-trip is one tap.
        if (HasOutput)
        {
            var carried = OutputText;
            DirectionIndex = IsDecode ? 0 : 1;
            InputText = carried;
        }
        else
        {
            DirectionIndex = IsDecode ? 0 : 1;
        }
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
