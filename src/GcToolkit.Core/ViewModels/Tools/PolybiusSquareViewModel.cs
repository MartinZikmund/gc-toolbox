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
/// Polybius Square cipher (issue #253) — the substitution scheme behind Bifid/Nihilist/ADFGX, one of the
/// most common in geocache puzzles. Converts text to coordinate pairs and back <b>live in both
/// directions</b> through the pure <see cref="PolybiusSquare"/> codec, with cachesleuth.com parity
/// (5×5 / 6×6 grids, a keyed alphabet, a single-letter merge control, group-every-N, Example/Reset, Copy)
/// and beyond it: a rendered, tappable square that highlights the cell for each input letter, selectable
/// coordinate labels (digits / ADFGX / custom), separator and read-order controls, a forgiving decode
/// that flags invalid pairs, Share, and explicit empty/error states. Fully offline and deterministic.
/// </summary>
[Tool("PolybiusSquare", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["polybius", "square", "grid", "cipher", "adfgx", "adfgvx", "coordinates", "šifra", "mřížka"])]
public sealed partial class PolybiusSquareViewModel : ToolViewModelBase
{
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private PolybiusSquare _square = new(new PolybiusOptions());
    private bool _suppressSync;
    private bool _activated;

    public PolybiusSquareViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("PolybiusSquare", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = 5×5 (I/J merged), 1 = 6×6 (letters + digits).</summary>
    [ObservableProperty]
    public partial int GridSizeIndex { get; set; }

    /// <summary>0 = digit labels, 1 = ADFGX/ADFGVX, 2 = custom labels.</summary>
    [ObservableProperty]
    public partial int LabelSchemeIndex { get; set; }

    /// <summary>The custom row/column label string (used when <see cref="LabelSchemeIndex"/> is 2).</summary>
    [ObservableProperty]
    public partial string CustomLabels { get; set; } = "12345";

    /// <summary>Optional keyword/phrase that seeds the keyed alphabet.</summary>
    [ObservableProperty]
    public partial string Keyword { get; set; } = string.Empty;

    /// <summary>Reverse the keyword before seeding the square.</summary>
    [ObservableProperty]
    public partial bool ReverseKeyword { get; set; }

    /// <summary>Keep the last instead of the first instance of repeated keyword letters.</summary>
    [ObservableProperty]
    public partial bool UseLastInstance { get; set; }

    /// <summary>The 5×5 merge pair shown as two letters (e.g. <c>"IJ"</c> folds J onto I).</summary>
    [ObservableProperty]
    public partial string MergePair { get; set; } = "IJ";

    /// <summary>Read the column label before the row label.</summary>
    [ObservableProperty]
    public partial bool ColumnThenRow { get; set; }

    /// <summary>0 = space, 1 = comma, 2 = none, 3 = custom separator.</summary>
    [ObservableProperty]
    public partial int SeparatorIndex { get; set; }

    /// <summary>The custom separator used when <see cref="SeparatorIndex"/> is 3.</summary>
    [ObservableProperty]
    public partial string CustomSeparator { get; set; } = "-";

    [ObservableProperty]
    public partial string PlainText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CipherText { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when there is ciphertext output to copy/share.</summary>
    [ObservableProperty]
    public partial bool HasCipher { get; set; }

    /// <summary><see langword="true"/> when the square couldn't be built (bad labels/merge) — disables the grid.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the last decode flagged invalid/leftover coordinate pairs.</summary>
    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> only for the 5×5 grid, where the merge pair control applies.</summary>
    [ObservableProperty]
    public partial bool IsFiveByFive { get; set; } = true;

    /// <summary><see langword="true"/> when the custom label / separator fields should be shown.</summary>
    [ObservableProperty]
    public partial bool IsCustomLabels { get; set; }

    [ObservableProperty]
    public partial bool IsCustomSeparator { get; set; }

    /// <summary>The rendered square as flat row-major cells for a UniformGrid of fixed column count <see cref="Side"/>.</summary>
    public ObservableCollection<PolybiusCellItem> Cells { get; } = [];

    /// <summary>The side length of the current square (5 or 6) — the column count for the rendered grid.</summary>
    [ObservableProperty]
    public partial int Side { get; set; } = 5;

    private PolybiusGridSize Size => GridSizeIndex == 1 ? PolybiusGridSize.SixBySix : PolybiusGridSize.FiveByFive;

    private PolybiusLabelScheme LabelScheme => LabelSchemeIndex switch
    {
        1 => PolybiusLabelScheme.Adfgx,
        2 => PolybiusLabelScheme.Custom,
        _ => PolybiusLabelScheme.Digits,
    };

    private string Separator => SeparatorIndex switch
    {
        1 => ", ",
        2 => "",
        3 => CustomSeparator,
        _ => " ",
    };

    // ---- Option changes rebuild the square, then re-encode from the current plaintext ----

    partial void OnGridSizeIndexChanged(int value)
    {
        if (value is not (0 or 1))
        {
            return;
        }

        IsFiveByFive = value == 0;
        RebuildAndEncode();
    }

    partial void OnLabelSchemeIndexChanged(int value)
    {
        if (value is not (0 or 1 or 2))
        {
            return;
        }

        IsCustomLabels = value == 2;
        RebuildAndEncode();
    }

    partial void OnCustomLabelsChanged(string value) => RebuildAndEncode();

    partial void OnKeywordChanged(string value) => RebuildAndEncode();

    partial void OnReverseKeywordChanged(bool value) => RebuildAndEncode();

    partial void OnUseLastInstanceChanged(bool value) => RebuildAndEncode();

    partial void OnMergePairChanged(string value) => RebuildAndEncode();

    partial void OnColumnThenRowChanged(bool value) => RebuildAndEncode();

    partial void OnSeparatorIndexChanged(int value)
    {
        if (value is not (0 or 1 or 2 or 3))
        {
            return;
        }

        IsCustomSeparator = value == 3;
        RebuildAndEncode();
    }

    partial void OnCustomSeparatorChanged(string value) => RebuildAndEncode();

    // ---- Live two-way text sync ----

    partial void OnPlainTextChanged(string value)
    {
        if (_suppressSync)
        {
            return;
        }

        Encode();
        HighlightFor(value);
    }

    partial void OnCipherTextChanged(string value)
    {
        if (_suppressSync)
        {
            return;
        }

        Decode();
    }

    partial void OnHasCipherChanged(bool value)
    {
        CopyCipherCommand.NotifyCanExecuteChanged();
        ShareCipherCommand.NotifyCanExecuteChanged();
    }

    public override void ViewCreated()
    {
        base.ViewCreated();
        EnsureInitialized();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_activated)
        {
            return;
        }

        _activated = true;
        RebuildAndEncode();
    }

    /// <summary>Rebuilds the square from the current options, renders the grid, and re-encodes the plaintext.</summary>
    private void RebuildAndEncode()
    {
        if (!TryBuildSquare())
        {
            return;
        }

        RenderGrid();
        Encode();
        HighlightFor(PlainText);
    }

    private bool TryBuildSquare()
    {
        try
        {
            _square = new PolybiusSquare(BuildOptions());
            Side = _square.Side;
            HasError = false;
            ErrorMessage = string.Empty;
            return true;
        }
        catch (ArgumentException)
        {
            HasError = true;
            ErrorMessage = _localizer["PolybiusSquareInvalidOptions"].Value;
            Cells.Clear();
            CipherText = string.Empty;
            HasCipher = false;
            return false;
        }
    }

    private PolybiusOptions BuildOptions()
    {
        var (from, into) = ParseMergePair(MergePair);
        return new PolybiusOptions(
            Size: Size,
            Keyword: Keyword,
            LabelScheme: LabelScheme,
            CustomLabels: LabelScheme == PolybiusLabelScheme.Custom ? CustomLabels : null,
            MergeFrom: from,
            MergeInto: into,
            ReverseKeyword: ReverseKeyword,
            KeywordPlacement: UseLastInstance ? PolybiusKeywordPlacement.LastInstance : PolybiusKeywordPlacement.FirstInstance,
            Separator: Separator,
            ColumnThenRow: ColumnThenRow);
    }

    private static (char From, char Into) ParseMergePair(string pair)
    {
        // Expect two letters "<into><from>" (e.g. "IJ" = J folds onto I); fall back to the I/J default.
        var letters = new string([.. pair.Where(char.IsLetter)]);
        return letters.Length >= 2 ? (letters[1], letters[0]) : ('J', 'I');
    }

    private void RenderGrid()
    {
        Cells.Clear();
        foreach (var row in _square.GetCells())
        {
            foreach (var cell in row)
            {
                Cells.Add(new PolybiusCellItem(cell.Letter, cell.Coordinate, AppendToPlainText));
            }
        }
    }

    private void Encode()
    {
        if (HasError)
        {
            return;
        }

        _suppressSync = true;
        CipherText = _square.Encrypt(PlainText);
        _suppressSync = false;

        HasCipher = CipherText.Length > 0;
        HasWarning = false;
        WarningMessage = string.Empty;
    }

    private void Decode()
    {
        if (HasError)
        {
            return;
        }

        var result = _square.Decrypt(CipherText);

        _suppressSync = true;
        PlainText = result.Text;
        _suppressSync = false;

        HasCipher = CipherText.Length > 0;
        HighlightFor(PlainText);

        if (result.IsClean)
        {
            HasWarning = false;
            WarningMessage = string.Empty;
        }
        else
        {
            HasWarning = true;
            WarningMessage = BuildWarning(result);
        }
    }

    private string BuildWarning(PolybiusDecodeResult result)
    {
        if (result.HasLeftover && result.InvalidPairs.Count > 0)
        {
            return _localizer["PolybiusDecodeMixedWarning"].Value;
        }

        if (result.HasLeftover)
        {
            return _localizer["PolybiusDecodeLeftoverWarning"].Value;
        }

        var joined = string.Join(", ", result.InvalidPairs);
        return string.Format(_localizer["PolybiusDecodeInvalidWarning"].Value, joined);
    }

    /// <summary>Lights up the cells whose letter appears in <paramref name="text"/> (the last letter wins focus).</summary>
    private void HighlightFor(string? text)
    {
        var active = LastSquareLetter(text);
        foreach (var cell in Cells)
        {
            cell.IsHighlighted = active is char c && CellHoldsLetter(cell, c);
        }
    }

    private char? LastSquareLetter(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (_square.CoordinateFor(text[i]).Length > 0)
            {
                return char.ToUpperInvariant(text[i]);
            }
        }

        return null;
    }

    private static bool CellHoldsLetter(PolybiusCellItem cell, char letter)
        => cell.Letter.Contains(char.ToUpperInvariant(letter));

    private void AppendToPlainText(string letter) => PlainText += letter;

    // ---- Commands ----

    [RelayCommand(CanExecute = nameof(HasCipher))]
    private void CopyCipher() => _clipboard.SetText(CipherText);

    [RelayCommand(CanExecute = nameof(HasCipher))]
    private async Task ShareCipherAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, CipherText);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _suppressSync = true;
        PlainText = string.Empty;
        CipherText = string.Empty;
        _suppressSync = false;
        HasCipher = false;
        HasWarning = false;
        WarningMessage = string.Empty;
        HighlightFor(null);
    }

    /// <summary>Loads a worked example so the tool is self-explanatory on first open.</summary>
    [RelayCommand]
    private void Example()
    {
        _suppressSync = true;
        GridSizeIndex = 0;
        LabelSchemeIndex = 0;
        Keyword = string.Empty;
        ReverseKeyword = false;
        UseLastInstance = false;
        MergePair = "IJ";
        ColumnThenRow = false;
        SeparatorIndex = 0;
        PlainText = "HELLO GEOCACHER";
        _suppressSync = false;
        RebuildAndEncode();
    }

    /// <summary>Resets every option and both text panes to the default empty 5×5 square.</summary>
    [RelayCommand]
    private void Reset()
    {
        _suppressSync = true;
        GridSizeIndex = 0;
        LabelSchemeIndex = 0;
        CustomLabels = "12345";
        Keyword = string.Empty;
        ReverseKeyword = false;
        UseLastInstance = false;
        MergePair = "IJ";
        ColumnThenRow = false;
        SeparatorIndex = 0;
        CustomSeparator = "-";
        PlainText = string.Empty;
        CipherText = string.Empty;
        _suppressSync = false;
        RebuildAndEncode();
    }
}
