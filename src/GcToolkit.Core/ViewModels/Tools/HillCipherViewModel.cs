using System.Collections.ObjectModel;
using System.Text;
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
/// One editable cell of the key matrix. Holds its grid position so the VM can read the matrix back
/// row-major, and raises change notifications so the live preview recomputes on every keystroke.
/// </summary>
public sealed partial class HillMatrixCell : ObservableObject
{
    public HillMatrixCell(int row, int column, int value)
    {
        Row = row;
        Column = column;
        Text = value.ToString();
    }

    public int Row { get; }

    public int Column { get; }

    /// <summary>Accessible label, e.g. "Row 1, column 2".</summary>
    public string AutomationName { get; set; } = string.Empty;

    /// <summary>The raw text the user typed; parsed leniently into 0–25 mod 26.</summary>
    [ObservableProperty]
    public partial string Text { get; set; }
}

/// <summary>
/// Hill cipher tool (issue #200). Encrypts and decrypts polygraphic blocks live as the user edits the
/// key matrix (2×2 or 3×3), swaps direction in one tap, and shows the active key matrix and its mod-26
/// inverse. Beyond cachesleuth.com parity (2×2 numeric only): variable matrix size, keyword-derived
/// keys, a live inverse display, configurable pad character, up-front invertibility validation (never a
/// silent wrong answer), and copy/share. All math lives in the pure <see cref="HillCipher"/> (thin-VM convention).
/// </summary>
[Tool("HillCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["hill", "matrix", "linear algebra", "polygraphic", "cipher", "šifra", "matice"])]
public sealed partial class HillCipherViewModel : ToolViewModelBase
{
    private readonly HillCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressRecompute;

    public HillCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("HillCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        // Seed the grid with the Wikipedia 3×3 example so the tool is useful on first open.
        BuildMatrix(2);
        Recompute();
    }

    /// <summary>Selected matrix size: 0 = 2×2, 1 = 3×3.</summary>
    [ObservableProperty]
    public partial int SizeIndex { get; set; }

    /// <summary>0 = Encrypt, 1 = Decrypt.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>0 = numeric matrix, 1 = keyword-derived key.</summary>
    [ObservableProperty]
    public partial int KeyModeIndex { get; set; }

    [ObservableProperty]
    public partial string KeywordText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PadCharacter { get; set; } = "X";

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

    /// <summary>The active key matrix rendered as text rows (e.g. <c>6 24 1</c>) for display.</summary>
    [ObservableProperty]
    public partial string KeyMatrixDisplay { get; set; } = string.Empty;

    /// <summary>The mod-26 inverse of the key as text rows, or empty when the key is not invertible.</summary>
    [ObservableProperty]
    public partial string InverseMatrixDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasInverse { get; set; }

    /// <summary>The determinant of the key mod 26, shown alongside the matrices.</summary>
    [ObservableProperty]
    public partial string DeterminantText { get; set; } = string.Empty;

    /// <summary>The editable cells of the numeric key matrix (row-major).</summary>
    public ObservableCollection<HillMatrixCell> MatrixCells { get; } = [];

    /// <summary>Columns the grid lays the cells out in (matches the matrix size).</summary>
    [ObservableProperty]
    public partial int MatrixColumns { get; set; } = 2;

    /// <summary><see langword="true"/> when keyword mode is active (hides the numeric grid).</summary>
    public bool IsKeywordMode => KeyModeIndex == 1;

    public bool IsNumericMode => KeyModeIndex == 0;

    private int Size => SizeIndex == 1 ? 3 : 2;

    private bool IsDecrypt => DirectionIndex == 1;

    partial void OnSizeIndexChanged(int value)
    {
        if (value is not (0 or 1))
        {
            return;
        }

        BuildMatrix(Size);
        Recompute();
    }

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnKeyModeIndexChanged(int value)
    {
        if (value is not (0 or 1))
        {
            return;
        }

        OnPropertyChanged(nameof(IsKeywordMode));
        OnPropertyChanged(nameof(IsNumericMode));
        Recompute();
    }

    partial void OnKeywordTextChanged(string value) => Recompute();

    partial void OnPadCharacterChanged(string value) => Recompute();

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void BuildMatrix(int size)
    {
        _suppressRecompute = true;

        foreach (var cell in MatrixCells)
        {
            cell.PropertyChanged -= OnCellChanged;
        }

        MatrixCells.Clear();
        MatrixColumns = size;

        // A sensible invertible default per size (2×2 det 9; 3×3 is the Wikipedia key).
        int[,] seed = size == 3
            ? new[,] { { 6, 24, 1 }, { 13, 16, 10 }, { 20, 17, 15 } }
            : new[,] { { 3, 3 }, { 2, 5 } };

        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                HillMatrixCell cell = new(r, c, seed[r, c])
                {
                    AutomationName = string.Format(_localizer["HillCellLabel"].Value, r + 1, c + 1),
                };
                cell.PropertyChanged += OnCellChanged;
                MatrixCells.Add(cell);
            }
        }

        _suppressRecompute = false;
    }

    private void OnCellChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HillMatrixCell.Text))
        {
            Recompute();
        }
    }

    /// <summary>Reads the active key from either the numeric grid or the keyword, or null when incomplete.</summary>
    private int[,]? CurrentKey()
    {
        var size = Size;
        if (IsKeywordMode)
        {
            return _cipher.KeyFromKeyword(KeywordText, size);
        }

        var matrix = new int[size, size];
        foreach (var cell in MatrixCells)
        {
            if (!int.TryParse(cell.Text?.Trim(), out var value))
            {
                return null;
            }

            matrix[cell.Row, cell.Column] = ((value % HillCipher.Modulus) + HillCipher.Modulus) % HillCipher.Modulus;
        }

        return matrix;
    }

    private char PadChar()
    {
        var trimmed = PadCharacter?.Trim();
        return !string.IsNullOrEmpty(trimmed) && char.IsLetter(trimmed[0]) ? trimmed[0] : 'X';
    }

    private void Recompute()
    {
        if (_suppressRecompute)
        {
            return;
        }

        var key = CurrentKey();
        if (key is null)
        {
            ClearKeyDisplays();
            OutputText = string.Empty;
            HasOutput = false;
            SetWarning(IsKeywordMode ? "HillKeywordTooShort" : "HillInvalidKey");
            return;
        }

        KeyMatrixDisplay = FormatMatrix(key);
        var det = _cipher.DeterminantMod(key);
        DeterminantText = string.Format(_localizer["HillDeterminantFormat"].Value, det);

        var inverse = _cipher.InvertMatrix(key);
        if (inverse.Success)
        {
            HasInverse = true;
            InverseMatrixDisplay = FormatMatrix(inverse.Inverse);
        }
        else
        {
            HasInverse = false;
            InverseMatrixDisplay = string.Empty;
        }

        // A non-invertible key can never round-trip, so flag it regardless of direction.
        if (!inverse.Success)
        {
            OutputText = string.Empty;
            HasOutput = false;
            SetWarning("HillNotInvertible");
            return;
        }

        var result = IsDecrypt
            ? _cipher.Decrypt(InputText, key, PadChar())
            : _cipher.Encrypt(InputText, key, PadChar());

        if (result.Success)
        {
            OutputText = result.Text;
            HasOutput = !string.IsNullOrEmpty(result.Text);
            ClearWarning();
        }
        else
        {
            OutputText = string.Empty;
            HasOutput = false;
            SetWarning(result.Status switch
            {
                HillStatus.NotInvertible => "HillNotInvertible",
                HillStatus.NoLetters => "HillNoLetters",
                _ => "HillInvalidKey",
            });
        }
    }

    private void ClearKeyDisplays()
    {
        KeyMatrixDisplay = string.Empty;
        InverseMatrixDisplay = string.Empty;
        DeterminantText = string.Empty;
        HasInverse = false;
    }

    private void SetWarning(string key)
    {
        // An empty input is the expected initial state, not an error worth shouting about.
        if (key == "HillNoLetters" && string.IsNullOrWhiteSpace(InputText))
        {
            ClearWarning();
            return;
        }

        HasWarning = true;
        WarningMessage = _localizer[key].Value;
    }

    private void ClearWarning()
    {
        HasWarning = false;
        WarningMessage = string.Empty;
    }

    private static string FormatMatrix(int[,] matrix)
    {
        var n = matrix.GetLength(0);
        StringBuilder sb = new();
        for (var r = 0; r < n; r++)
        {
            for (var c = 0; c < n; c++)
            {
                if (c > 0)
                {
                    sb.Append("  ");
                }

                sb.Append(matrix[r, c].ToString().PadLeft(2));
            }

            if (r < n - 1)
            {
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>Swaps Encrypt/Decrypt and carries the current output into the input for a one-tap round-trip.</summary>
    [RelayCommand]
    private void Swap()
    {
        _suppressRecompute = true;
        if (HasOutput)
        {
            InputText = OutputText;
        }

        _suppressRecompute = false;
        DirectionIndex = IsDecrypt ? 0 : 1;
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
        _suppressRecompute = true;
        InputText = string.Empty;
        _suppressRecompute = false;
        Recompute();
    }
}
