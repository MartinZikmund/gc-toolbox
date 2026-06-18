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

/// <summary>
/// Bifid (Delastelle) cipher tool (issue #9). Encrypts/decrypts against a live 5×5 Polybius square
/// built from a keyword or a manual square, with the alphabet trimmed to 25 letters by merge
/// (default J→I) or skip. Beyond geocachingtoolbox.com's single-block variant it adds an adjustable
/// period, a one-tap encode/decode swap, the rendered square, and copy/share — all fully offline.
/// </summary>
[Tool("BifidCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["bifid", "delastelle", "fractionation", "polybius", "cipher", "šifra"])]
public sealed partial class BifidCipherViewModel : ToolViewModelBase
{
    private static readonly string[] AlphabetLetters =
        [.. "ABCDEFGHIJKLMNOPQRSTUVWXYZ".Select(c => c.ToString())];

    private readonly BifidCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressConvert;

    public BifidCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("BifidCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        RebuildSquare();
        Convert();
    }

    /// <summary>The A–Z letters offered in the merge/skip pickers.</summary>
    public IReadOnlyList<string> AlphabetChoices => AlphabetLetters;

    /// <summary>0 = encrypt, 1 = decrypt.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>0 = keyword-seeded square, 1 = manual / explicit 25-letter square.</summary>
    [ObservableProperty]
    public partial int SquareModeIndex { get; set; }

    /// <summary>0 = merge a letter into another (J→I), 1 = skip a letter entirely.</summary>
    [ObservableProperty]
    public partial int FitModeIndex { get; set; }

    [ObservableProperty]
    public partial string Keyword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ManualSquare { get; set; } = "ABCDEFGHIKLMNOPQRSTUVWXYZ";

    /// <summary>The letter removed from the square — merged away (default <c>J</c>) or skipped.</summary>
    [ObservableProperty]
    public partial string FitFromLetter { get; set; } = "J";

    /// <summary>The surviving letter that <see cref="FitFromLetter"/> folds into when merging (default <c>I</c>).</summary>
    [ObservableProperty]
    public partial string FitIntoLetter { get; set; } = "I";

    /// <summary>Block length for periodic fractionation; 0 (or empty) means the whole message at once.</summary>
    [ObservableProperty]
    public partial int Period { get; set; }

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

    /// <summary><see langword="true"/> when the merge target picker applies (merge mode only).</summary>
    [ObservableProperty]
    public partial bool IsMergeMode { get; set; } = true;

    [ObservableProperty]
    public partial bool IsKeywordMode { get; set; } = true;

    /// <summary>The rendered square, row-major (25 cells), for the grid display.</summary>
    public ObservableCollection<BifidSquareCell> SquareCells { get; } = [];

    private bool IsDecrypt => DirectionIndex == 1;

    partial void OnDirectionIndexChanged(int value) => Convert();

    partial void OnSquareModeIndexChanged(int value)
    {
        IsKeywordMode = value != 1;
        RebuildSquare();
    }

    partial void OnFitModeIndexChanged(int value)
    {
        IsMergeMode = value != 1;
        RebuildSquare();
    }

    partial void OnKeywordChanged(string value) => RebuildSquare();

    partial void OnManualSquareChanged(string value) => RebuildSquare();

    partial void OnFitFromLetterChanged(string value) => RebuildSquare();

    partial void OnFitIntoLetterChanged(string value) => RebuildSquare();

    partial void OnPeriodChanged(int value) => Convert();

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

    private BifidLetterFit CurrentFit()
    {
        var from = FirstLetterOr(FitFromLetter, 'J');
        if (FitModeIndex == 1)
        {
            return BifidLetterFit.Skip(from);
        }

        var into = FirstLetterOr(FitIntoLetter, 'I');
        return new BifidLetterFit(BifidFitKind.Merge, from, into);
    }

    /// <summary>Rebuilds the active square from the current inputs, surfacing a clear error when it is invalid.</summary>
    private void RebuildSquare()
    {
        var fit = CurrentFit();

        try
        {
            var square = SquareModeIndex == 1
                ? BifidSquare.FromText(ManualSquare, fit)
                : BifidSquare.FromKeyword(Keyword, fit);

            RenderSquare(square);
            Convert(square);
        }
        catch (ArgumentException)
        {
            SquareCells.Clear();
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = true;
            WarningMessage = _localizer["BifidInvalidSquareNotice"].Value;
        }
    }

    private void RenderSquare(BifidSquare square)
    {
        SquareCells.Clear();
        for (var i = 0; i < square.Letters.Length; i++)
        {
            SquareCells.Add(new BifidSquareCell(square.Letters[i], (i / 5) + 1, (i % 5) + 1));
        }
    }

    private void Convert()
    {
        var fit = CurrentFit();
        BifidSquare square;
        try
        {
            square = SquareModeIndex == 1 ? BifidSquare.FromText(ManualSquare, fit) : BifidSquare.FromKeyword(Keyword, fit);
        }
        catch (ArgumentException)
        {
            return; // RebuildSquare already surfaced the invalid-square state.
        }

        Convert(square);
    }

    private void Convert(BifidSquare square)
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = false;
            WarningMessage = string.Empty;
            return;
        }

        var period = Period > 0 ? Period : 0;
        var result = IsDecrypt
            ? _cipher.Decrypt(InputText, square, period)
            : _cipher.Encrypt(InputText, square, period);

        OutputText = result.Text;
        HasOutput = result.Text.Length > 0;

        if (!HasOutput)
        {
            HasWarning = true;
            WarningMessage = _localizer["BifidNoLettersNotice"].Value;
        }
        else if (result.HasIgnored)
        {
            HasWarning = true;
            WarningMessage = _localizer["BifidIgnoredNotice"].Value;
        }
        else
        {
            HasWarning = false;
            WarningMessage = string.Empty;
        }
    }

    private static char FirstLetterOr(string value, char fallback)
        => !string.IsNullOrEmpty(value) && char.IsLetter(value[0]) ? char.ToUpperInvariant(value[0]) : fallback;

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

    /// <summary>Flips encrypt/decrypt and carries the result into the input, so a round-trip is one tap.</summary>
    [RelayCommand]
    private void Swap()
    {
        _suppressConvert = true;
        InputText = OutputText;
        _suppressConvert = false;
        DirectionIndex = IsDecrypt ? 0 : 1; // OnDirectionIndexChanged re-converts.
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;

    /// <summary>Restores the default keyword-seeded square (no keyword, J→I merge).</summary>
    [RelayCommand]
    private void ResetSquare()
    {
        _suppressConvert = true;
        SquareModeIndex = 0;
        Keyword = string.Empty;
        FitModeIndex = 0;
        FitFromLetter = "J";
        FitIntoLetter = "I";
        ManualSquare = "ABCDEFGHIKLMNOPQRSTUVWXYZ";
        _suppressConvert = false;
        RebuildSquare();
    }
}
