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
/// Numbers ↔ words converter (issue #243). Auto-detects direction from a single field (digits → words,
/// words → number), with a one-tap swap for instant round-trips. Goes beyond cachesleuth.com with
/// ordinals, year reading, a British "and" style, a Czech spelling mode, batch (per-line) conversion,
/// and puzzle-solver helpers (per-word/total letter counts and first-letter extraction). Pure logic
/// lives in <see cref="NumberWordsCodec"/>; this VM only orchestrates options, live preview and IO.
/// </summary>
[Tool("NumbersToWords", ToolCategory.Numbers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["numbers to words", "spell", "cardinal", "ordinal", "číslo slovy", "slovem", "year", "letters"])]
public sealed partial class NumbersToWordsViewModel : ToolViewModelBase
{
    private const string InvalidToken = "?";

    private readonly NumberWordsCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public NumbersToWordsViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("NumbersToWords", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

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

    /// <summary>0 = cardinal, 1 = ordinal, 2 = year (matches the RadioButtons order in the view).</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    /// <summary>British-style "and" (ONE HUNDRED AND ONE) — English only.</summary>
    [ObservableProperty]
    public partial bool UseBritishAnd { get; set; }

    /// <summary>Spell in Czech instead of English.</summary>
    [ObservableProperty]
    public partial bool UseCzech { get; set; }

    /// <summary>Convert each input line independently, results re-joined line-by-line.</summary>
    [ObservableProperty]
    public partial bool IsBatchMode { get; set; }

    // ---- Solver helpers (shown beside a single result) ----

    [ObservableProperty]
    public partial bool ShowSolver { get; set; }

    [ObservableProperty]
    public partial int TotalLetterCount { get; set; }

    [ObservableProperty]
    public partial string LetterCountsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FirstLetters { get; set; } = string.Empty;

    private NumberWordsOptions Options => new(
        ModeIndex switch
        {
            1 => NumberWordsMode.Ordinal,
            2 => NumberWordsMode.Year,
            _ => NumberWordsMode.Cardinal,
        },
        UseCzech ? NumberWordsLanguage.Czech : NumberWordsLanguage.English,
        UseBritishAnd);

    partial void OnInputTextChanged(string value) => Convert();

    partial void OnModeIndexChanged(int value)
    {
        if (value is 0 or 1 or 2)
        {
            Convert();
        }
    }

    partial void OnUseBritishAndChanged(bool value) => Convert();

    partial void OnUseCzechChanged(bool value) => Convert();

    partial void OnIsBatchModeChanged(bool value) => Convert();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = false;
            WarningMessage = string.Empty;
            ClearSolver();
            return;
        }

        if (IsBatchMode)
        {
            ConvertBatch();
        }
        else
        {
            ConvertSingle();
        }
    }

    private void ConvertSingle()
    {
        var result = _codec.Convert(InputText, Options);
        if (result.IsValid)
        {
            OutputText = result.Text;
            HasOutput = true;
            HasWarning = false;
            WarningMessage = string.Empty;
            UpdateSolver(result);
        }
        else
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = true;
            WarningMessage = _localizer["NumbersToWordsInvalidNotice"].Value;
            ClearSolver();
        }
    }

    private void ConvertBatch()
    {
        var results = _codec.ConvertBatch(InputText, Options);
        var builder = new StringBuilder();
        var anyValid = false;
        var anyInvalid = false;

        for (var i = 0; i < results.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            var result = results[i];
            if (result.IsValid)
            {
                builder.Append(result.Text);
                anyValid = true;
            }
            else
            {
                builder.Append(InvalidToken);
                anyInvalid = true;
            }
        }

        OutputText = builder.ToString();
        HasOutput = anyValid;
        HasWarning = anyInvalid;
        WarningMessage = anyInvalid ? _localizer["NumbersToWordsInvalidNotice"].Value : string.Empty;
        ClearSolver();
    }

    /// <summary>Letter counts / first letters only make sense when the result is words (not digits).</summary>
    private void UpdateSolver(NumberWordsResult result)
    {
        if (result.Direction != NumberWordsDirection.NumberToWords)
        {
            ClearSolver();
            return;
        }

        TotalLetterCount = NumberWordsCodec.TotalLetterCount(result.Text);
        LetterCountsText = string.Join(" ", NumberWordsCodec.LetterCountsPerWord(result.Text));
        FirstLetters = NumberWordsCodec.FirstLetters(result.Text);
        ShowSolver = TotalLetterCount > 0;
    }

    private void ClearSolver()
    {
        ShowSolver = false;
        TotalLetterCount = 0;
        LetterCountsText = string.Empty;
        FirstLetters = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void Swap()
    {
        // Carry the result into the input so a round-trip is one tap; re-converts via the change hook.
        InputText = OutputText;
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
