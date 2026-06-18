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

/// <summary>One cell of the rendered Polybius square: its letter plus the header pair that addresses it.</summary>
public sealed partial class AdfgvxSquareCell : ObservableObject
{
    public AdfgvxSquareCell(string rowHeader, string columnHeader, char value, string automationName)
    {
        RowHeader = rowHeader;
        ColumnHeader = columnHeader;
        AutomationName = automationName;
        Value = value.ToString();
    }

    public string RowHeader { get; }

    public string ColumnHeader { get; }

    public string AutomationName { get; }

    [ObservableProperty]
    public partial string Value { get; set; }
}

/// <summary>One step of the substitution preview, for the worked-pipeline display.</summary>
public sealed record AdfgvxSubstitutionItem(string PlainChar, string Pair)
{
    /// <summary>Screen-reader label, e.g. <c>"A maps to AF"</c>.</summary>
    public string AutomationName => $"{PlainChar} → {Pair}";
}

/// <summary>
/// ADFGX / ADFGVX fractionating-transposition cipher (issue #1). Picks a variant (5×5 letters or 6×6
/// letters+digits), edits the mixed-alphabet square via presets (ordered / keyword-seeded / reproducible
/// random), takes a transposition keyword, and encrypts or decrypts live. Beyond geocachingtoolbox.com
/// parity it shows the worked pipeline (square + per-character substitution), folds J→I forgivingly with
/// a surfaced notice, swaps direction in one tap, and copies/shares the result. The transform itself
/// lives in the pure <see cref="AdfgvxCipher"/> so it stays unit-testable and fully offline.
/// </summary>
[Tool("AdfgvxCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["adfgx", "adfgvx", "fractionation", "transposition", "polybius", "cipher", "šifra"])]
public sealed partial class AdfgvxCipherViewModel : ToolViewModelBase
{
    private readonly AdfgvxCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    // A fixed seed keeps the "random" square reproducible within a session and across the test boundary.
    private int _randomSeed = 1;
    private bool _suppressRun;

    public AdfgvxCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("AdfgvxCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        _suppressRun = true;
        SquareFill = _cipher.OrderedFill(Variant);
        _suppressRun = false;
        Run();
    }

    /// <summary>0 = ADFGX (5×5), 1 = ADFGVX (6×6).</summary>
    [ObservableProperty]
    public partial int VariantIndex { get; set; }

    /// <summary>0 = Encrypt, 1 = Decrypt.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>The flattened, row-major square fill (the source of truth the grid renders from).</summary>
    [ObservableProperty]
    public partial string SquareFill { get; set; } = string.Empty;

    /// <summary>The transposition keyword.</summary>
    [ObservableProperty]
    public partial string Keyword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when the substitution preview has steps to show.</summary>
    [ObservableProperty]
    public partial bool HasSubstitution { get; set; }

    /// <summary><see langword="true"/> when a notice (cleaning or error) should be shown.</summary>
    [ObservableProperty]
    public partial bool HasNotice { get; set; }

    [ObservableProperty]
    public partial string NoticeMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> for an error notice (red), <see langword="false"/> for an informational one.</summary>
    [ObservableProperty]
    public partial bool IsError { get; set; }

    /// <summary>The InfoBar severity matching the current notice (Error vs Informational).</summary>
    [ObservableProperty]
    public partial InfoBarSeverity NoticeSeverity { get; set; } = InfoBarSeverity.Informational;

    /// <summary>The cipher header letters of the current variant (for the grid's row/column labels).</summary>
    [ObservableProperty]
    public partial string Headers { get; set; } = AdfgvxCipher.Adfgx5Headers;

    /// <summary>The square side length (5 or 6) — drives the grid column count.</summary>
    [ObservableProperty]
    public partial int SquareSize { get; set; } = 5;

    public AdfgvxVariant Variant => VariantIndex == 1 ? AdfgvxVariant.Adfgvx : AdfgvxVariant.Adfgx;

    private bool IsEncrypt => DirectionIndex != 1;

    /// <summary>The rendered square cells, row-major (count = <see cref="SquareSize"/>²).</summary>
    public ObservableCollection<AdfgvxSquareCell> SquareCells { get; } = [];

    /// <summary>The per-character substitution steps for the live preview.</summary>
    public ObservableCollection<AdfgvxSubstitutionItem> SubstitutionSteps { get; } = [];

    partial void OnVariantIndexChanged(int value)
    {
        if (_suppressRun)
        {
            return;
        }

        // Switching variant re-seeds the square with the ordered fill of the new alphabet.
        _suppressRun = true;
        SquareFill = _cipher.OrderedFill(Variant);
        _suppressRun = false;
        Run();
    }

    partial void OnDirectionIndexChanged(int value)
    {
        if (_suppressRun || value is not (0 or 1))
        {
            return;
        }

        Run();
    }

    partial void OnSquareFillChanged(string value)
    {
        if (!_suppressRun)
        {
            Run();
        }
    }

    partial void OnKeywordChanged(string value)
    {
        if (!_suppressRun)
        {
            Run();
        }
    }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressRun)
        {
            Run();
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Runs the whole pipeline: rebuild the grid, recompute the substitution preview and the result.</summary>
    private void Run()
    {
        SquareSize = AdfgvxCipher.Size(Variant);
        Headers = AdfgvxCipher.Headers(Variant);

        string square;
        try
        {
            square = _cipher.BuildSquare(SquareFill, Variant);
        }
        catch (AdfgvxException ex)
        {
            RebuildGridFromRawFill();
            SubstitutionSteps.Clear();
            HasSubstitution = false;
            OutputText = string.Empty;
            HasOutput = false;
            ShowNotice(MessageFor(ex.Reason), isError: true);
            return;
        }

        RebuildGrid(square);
        UpdateSubstitutionPreview();
        Compute();
    }

    private void Compute()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            ClearNotice();
            return;
        }

        try
        {
            var result = IsEncrypt
                ? _cipher.Encrypt(InputText, SquareFill, Keyword, Variant)
                : _cipher.Decrypt(InputText, SquareFill, Keyword, Variant);

            OutputText = result.Text;
            HasOutput = result.Text.Length > 0;

            if (result.HasNotes)
            {
                ShowNotice(NoticeFor(result.Notes), isError: false);
            }
            else
            {
                ClearNotice();
            }
        }
        catch (AdfgvxException ex)
        {
            OutputText = string.Empty;
            HasOutput = false;
            ShowNotice(MessageFor(ex.Reason), isError: true);
        }
    }

    private void UpdateSubstitutionPreview()
    {
        SubstitutionSteps.Clear();
        if (string.IsNullOrEmpty(InputText) || !IsEncrypt)
        {
            HasSubstitution = false;
            return;
        }

        foreach (var step in _cipher.SubstitutionSteps(InputText, SquareFill, Variant))
        {
            SubstitutionSteps.Add(new AdfgvxSubstitutionItem(step.PlainChar.ToString(), step.Pair));
        }

        HasSubstitution = SubstitutionSteps.Count > 0;
    }

    private void RebuildGrid(string square)
    {
        SquareCells.Clear();
        var size = SquareSize;
        for (var i = 0; i < square.Length; i++)
        {
            var rowHeader = Headers[i / size].ToString();
            var colHeader = Headers[i % size].ToString();
            var name = string.Format(_localizer["AdfgvxCellLabel"].Value, rowHeader, colHeader, square[i]);
            SquareCells.Add(new AdfgvxSquareCell(rowHeader, colHeader, square[i], name));
        }
    }

    /// <summary>Renders whatever cells are available from an invalid fill, padding the rest blank.</summary>
    private void RebuildGridFromRawFill()
    {
        SquareCells.Clear();
        var size = SquareSize;
        var total = size * size;
        var cleaned = new string((SquareFill ?? string.Empty)
            .Where(c => !char.IsWhiteSpace(c))
            .Select(char.ToUpperInvariant)
            .ToArray());

        for (var i = 0; i < total; i++)
        {
            var rowHeader = Headers[i / size].ToString();
            var colHeader = Headers[i % size].ToString();
            var value = i < cleaned.Length ? cleaned[i] : ' ';
            var name = string.Format(_localizer["AdfgvxCellLabel"].Value, rowHeader, colHeader, value);
            SquareCells.Add(new AdfgvxSquareCell(rowHeader, colHeader, value, name));
        }
    }

    // ---- Square preset commands ----

    [RelayCommand]
    private void OrderedSquare() => SquareFill = _cipher.OrderedFill(Variant);

    [RelayCommand]
    private void KeywordSeededSquare() => SquareFill = _cipher.KeywordSeededFill(Keyword, Variant);

    [RelayCommand]
    private void RandomSquare()
    {
        // Reproducible within a session: bump the seed each press so it visibly changes yet stays testable.
        var rng = new Random(_randomSeed++);
        var chars = AdfgvxCipher.Alphabet(Variant).ToCharArray();
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        SquareFill = new string(chars);
    }

    // ---- Output commands ----

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

    /// <summary>Carries the result into the input and flips direction, so an encode→decode check is one tap.</summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void Swap()
    {
        _suppressRun = true;
        InputText = OutputText;
        DirectionIndex = IsEncrypt ? 1 : 0;
        _suppressRun = false;
        Run();
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;

    // ---- Notice helpers ----

    private void ShowNotice(string message, bool isError)
    {
        NoticeMessage = message;
        IsError = isError;
        NoticeSeverity = isError ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
        HasNotice = true;
    }

    private void ClearNotice()
    {
        HasNotice = false;
        IsError = false;
        NoticeMessage = string.Empty;
    }

    private string MessageFor(AdfgvxError error) => error switch
    {
        AdfgvxError.IncompleteSquare => _localizer["AdfgvxErrorIncompleteSquare"].Value,
        AdfgvxError.DuplicateCell => _localizer["AdfgvxErrorDuplicateCell"].Value,
        AdfgvxError.InvalidSquareChar => _localizer["AdfgvxErrorInvalidSquareChar"].Value,
        AdfgvxError.EmptyKeyword => _localizer["AdfgvxErrorEmptyKeyword"].Value,
        AdfgvxError.MalformedCiphertext => _localizer["AdfgvxErrorMalformedCiphertext"].Value,
        _ => _localizer["AdfgvxErrorIncompleteSquare"].Value,
    };

    private string NoticeFor(IReadOnlyList<AdfgvxNote> notes)
    {
        var parts = new List<string>(2);
        if (notes.Contains(AdfgvxNote.FoldedJToI))
        {
            parts.Add(_localizer["AdfgvxNoteFoldedJ"].Value);
        }

        if (notes.Contains(AdfgvxNote.DroppedUnsupportedChars))
        {
            parts.Add(_localizer["AdfgvxNoteDropped"].Value);
        }

        return string.Join(" ", parts);
    }
}
