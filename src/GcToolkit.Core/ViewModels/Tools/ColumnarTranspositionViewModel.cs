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
/// Columnar transposition cipher tool (issue #154). Encrypts/decrypts with a keyword <em>or</em> a
/// numeric column order, swaps direction in one tap, supports a configurable pad character with
/// "strip padding on decode", optional double transposition via a second key, and renders a live grid
/// preview (column letters + resolved read order). Beyond the reference site it handles irregular
/// (unpadded) grids correctly and works fully offline.
/// </summary>
[Tool("ColumnarTransposition", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["columnar", "transposition", "keyword", "grid", "cipher", "sloupcová", "transpozice", "šifra"])]
public sealed partial class ColumnarTranspositionViewModel : ToolViewModelBase
{
    private readonly ColumnarTransposition _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public ColumnarTranspositionViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("ColumnarTransposition", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = encrypt (plaintext → ciphertext), 1 = decrypt.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KeyText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SecondKeyText { get; set; } = string.Empty;

    /// <summary><see langword="true"/> enables a second transposition pass with <see cref="SecondKeyText"/>.</summary>
    [ObservableProperty]
    public partial bool UseDoubleTransposition { get; set; }

    [ObservableProperty]
    public partial bool PadToRectangle { get; set; }

    [ObservableProperty]
    public partial string PadCharText { get; set; } = "X";

    [ObservableProperty]
    public partial bool StripPaddingOnDecode { get; set; } = true;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasGrid { get; set; }

    [ObservableProperty]
    public partial int ColumnCount { get; set; }

    /// <summary>Per-column headers (keyword letter + read order) for the preview.</summary>
    public ObservableCollection<ColumnarHeaderCell> Headers { get; } = [];

    /// <summary>The grid cells, row-major, for an <c>ItemsRepeater</c> with a uniform column layout.</summary>
    public ObservableCollection<ColumnarGridCell> GridCells { get; } = [];

    private bool IsEncrypt => DirectionIndex != 1;

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnKeyTextChanged(string value) => Recompute();

    partial void OnSecondKeyTextChanged(string value) => Recompute();

    partial void OnUseDoubleTranspositionChanged(bool value) => Recompute();

    partial void OnPadToRectangleChanged(bool value) => Recompute();

    partial void OnPadCharTextChanged(string value) => Recompute();

    partial void OnStripPaddingOnDecodeChanged(bool value) => Recompute();

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The current options derived from the toggles. Public for testability.</summary>
    public ColumnarOptions BuildOptions() => new()
    {
        Pad = PadToRectangle,
        PadChar = string.IsNullOrEmpty(PadCharText) ? 'X' : PadCharText[0],
        StripPadding = StripPaddingOnDecode,
        SecondKey = UseDoubleTransposition && !string.IsNullOrWhiteSpace(SecondKeyText) ? SecondKeyText : null,
    };

    private void Recompute()
    {
        ClearOutput();

        if (string.IsNullOrEmpty(KeyText) || string.IsNullOrWhiteSpace(KeyText))
        {
            // Empty key isn't an error until there's text to act on; just show nothing.
            if (!string.IsNullOrEmpty(InputText))
            {
                ShowError(_localizer["ColumnarKeyEmptyError"].Value);
            }

            return;
        }

        if (!_cipher.TryValidateKey(KeyText, out _, out var keyError))
        {
            ShowError(keyError);
            return;
        }

        if (UseDoubleTransposition
            && !string.IsNullOrWhiteSpace(SecondKeyText)
            && !_cipher.TryValidateKey(SecondKeyText, out _, out var secondError))
        {
            ShowError(secondError);
            return;
        }

        if (string.IsNullOrEmpty(InputText))
        {
            return;
        }

        var options = BuildOptions();
        try
        {
            var result = IsEncrypt
                ? _cipher.Encrypt(InputText, KeyText, options)
                : _cipher.Decrypt(InputText, KeyText, options);

            OutputText = result.Text;
            HasOutput = !string.IsNullOrEmpty(result.Text);
            BuildGridPreview(options);
        }
        catch (ColumnarException ex)
        {
            ShowError(ex.Message);
        }
    }

    private void BuildGridPreview(ColumnarOptions options)
    {
        // Preview the grid the first key writes into (the text as laid out before reading columns).
        var preview = IsEncrypt
            ? (options.Pad ? PadForPreview(InputText, options) : InputText)
            : InputText;

        var grid = _cipher.BuildGrid(preview, KeyText);

        Headers.Clear();
        for (var col = 0; col < grid.ColumnCount; col++)
        {
            Headers.Add(new ColumnarHeaderCell(grid.ColumnLetters[col], grid.ColumnOrder[col]));
        }

        GridCells.Clear();
        for (var row = 0; row < grid.RowCount; row++)
        {
            for (var col = 0; col < grid.ColumnCount; col++)
            {
                GridCells.Add(new ColumnarGridCell(row, col, grid.Cell(row, col)));
            }
        }

        ColumnCount = grid.ColumnCount;
        HasGrid = grid.RowCount > 0 && grid.ColumnCount > 0;
    }

    private string PadForPreview(string text, ColumnarOptions options)
    {
        if (!_cipher.TryValidateKey(KeyText, out var cols, out _) || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var remainder = text.Length % cols;
        return remainder == 0 ? text : text + new string(options.PadChar, cols - remainder);
    }

    private void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        OutputText = string.Empty;
        HasOutput = false;
        HasGrid = false;
        Headers.Clear();
        GridCells.Clear();
    }

    private void ClearOutput()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        OutputText = string.Empty;
        HasOutput = false;
        HasGrid = false;
        Headers.Clear();
        GridCells.Clear();
        ColumnCount = 0;
    }

    [RelayCommand]
    private void SwapDirection()
    {
        // Carry the result into the input so a round-trip is one tap, then flip direction.
        if (HasOutput)
        {
            InputText = OutputText;
        }

        DirectionIndex = IsEncrypt ? 1 : 0;
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
    private void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
    }
}
