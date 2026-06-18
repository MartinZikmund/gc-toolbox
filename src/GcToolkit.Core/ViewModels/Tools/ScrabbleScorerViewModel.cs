using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Text;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One selectable letter-value system: its <see cref="System"/> and a localized <see cref="DisplayName"/>.</summary>
public sealed record LetterValueSystemOption(LetterValueSystem System, string DisplayName);

/// <summary>One cell of the active-table reference panel: the <see cref="Letter"/> and its <see cref="Value"/>.</summary>
public sealed record ReferenceCell(string Letter, int Value);

/// <summary>
/// Word-value / Scrabble scorer (issue #271). Scores letters by tile values and sums them — per word
/// and over the whole text — live as the user types, across six Scrabble languages, the alphabet-
/// position families, the phone-keypad vanity table, and a user-defined custom table. Shows a
/// per-letter breakdown, the usual geocaching reductions (digital root, digit sum, mod-26/mod-10,
/// reversed total), a value-reference panel, and a batch (per-line) mode. Copy total / per-word and
/// share. Fully offline. The scoring engine lives in the testable <see cref="ScrabbleScorer"/>.
/// </summary>
[Tool("ScrabbleScorer", ToolCategory.Text,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["scrabble", "word value", "tile", "points", "score", "skóre", "slovo", "hodnota"])]
public sealed partial class ScrabbleScorerViewModel : ToolViewModelBase
{
    private readonly ScrabbleScorer _scorer = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public ScrabbleScorerViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("ScrabbleScorer", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        Systems =
        [
            new(LetterValueSystem.ScrabbleEnglish, localizer["ScrabbleSystemEnglish"].Value),
            new(LetterValueSystem.ScrabbleDutch, localizer["ScrabbleSystemDutch"].Value),
            new(LetterValueSystem.ScrabbleGerman, localizer["ScrabbleSystemGerman"].Value),
            new(LetterValueSystem.ScrabbleFrench, localizer["ScrabbleSystemFrench"].Value),
            new(LetterValueSystem.ScrabbleSpanish, localizer["ScrabbleSystemSpanish"].Value),
            new(LetterValueSystem.ScrabbleItalian, localizer["ScrabbleSystemItalian"].Value),
            new(LetterValueSystem.A1Z26, localizer["ScrabbleSystemA1Z26"].Value),
            new(LetterValueSystem.A0Z25, localizer["ScrabbleSystemA0Z25"].Value),
            new(LetterValueSystem.ReversedA26Z1, localizer["ScrabbleSystemReversedA26Z1"].Value),
            new(LetterValueSystem.ReversedA25Z0, localizer["ScrabbleSystemReversedA25Z0"].Value),
            new(LetterValueSystem.PhoneKeypad, localizer["ScrabbleSystemPhoneKeypad"].Value),
            new(LetterValueSystem.Custom, localizer["ScrabbleSystemCustom"].Value),
        ];

        BuildReference(); // Show the default (English) table immediately, before any input.
    }

    /// <summary>The selectable letter-value systems shown in the picker.</summary>
    public IReadOnlyList<LetterValueSystemOption> Systems { get; }

    /// <summary>The per-word results (one row per whitespace-separated token).</summary>
    public ObservableCollection<ScrabbleWordItem> WordResults { get; } = [];

    /// <summary>The active table's letter→value mapping, for the reference panel.</summary>
    public ObservableCollection<ReferenceCell> ReferenceCells { get; } = [];

    [ObservableProperty]
    public partial int SelectedSystemIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>A comma- or space-separated A–Z custom table (only used when the custom system is active).</summary>
    [ObservableProperty]
    public partial string CustomTableText { get; set; } = "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1";

    /// <summary>Whole-text grand total.</summary>
    [ObservableProperty]
    public partial int GrandTotal { get; set; }

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial string DigitalRootText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DigitSumText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Mod26Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Mod10Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReversedTotalText { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the input held a letter the active table doesn't define.</summary>
    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the custom system is active (reveals the table editor).</summary>
    [ObservableProperty]
    public partial bool IsCustomSystem { get; set; }

    private LetterValueSystem SelectedSystem =>
        SelectedSystemIndex >= 0 && SelectedSystemIndex < Systems.Count
            ? Systems[SelectedSystemIndex].System
            : LetterValueSystem.ScrabbleEnglish;

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnSelectedSystemIndexChanged(int value)
    {
        IsCustomSystem = SelectedSystem == LetterValueSystem.Custom;
        Recompute();
    }

    partial void OnCustomTableTextChanged(string value)
    {
        if (IsCustomSystem)
        {
            Recompute();
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyTotalCommand.NotifyCanExecuteChanged();
        CopyWordsCommand.NotifyCanExecuteChanged();
        ShareCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        WordResults.Clear();
        BuildReference();

        if (string.IsNullOrWhiteSpace(InputText))
        {
            ResetTotals();
            return;
        }

        TextScore score;
        if (IsCustomSystem)
        {
            if (!TryParseCustomTable(out var table))
            {
                ResetTotals();
                HasWarning = true;
                WarningMessage = _localizer["ScrabbleCustomTableInvalid"].Value;
                return;
            }

            score = _scorer.Score(InputText, table);
        }
        else
        {
            score = _scorer.Score(InputText, SelectedSystem);
        }

        foreach (var word in score.Words)
        {
            WordResults.Add(new ScrabbleWordItem(word, _clipboard.SetText));
        }

        GrandTotal = score.GrandTotal;
        HasOutput = score.Words.Count > 0;

        var r = score.Reductions;
        DigitalRootText = r.DigitalRoot.ToString(System.Globalization.CultureInfo.CurrentCulture);
        DigitSumText = r.DigitSum.ToString(System.Globalization.CultureInfo.CurrentCulture);
        Mod26Text = r.Mod26.ToString(System.Globalization.CultureInfo.CurrentCulture);
        Mod10Text = r.Mod10.ToString(System.Globalization.CultureInfo.CurrentCulture);
        ReversedTotalText = r.ReversedTotal.ToString(System.Globalization.CultureInfo.CurrentCulture);

        HasWarning = score.HasUnknown;
        WarningMessage = score.HasUnknown ? _localizer["ScrabbleUnknownLetterNotice"].Value : string.Empty;
    }

    private void ResetTotals()
    {
        GrandTotal = 0;
        HasOutput = false;
        DigitalRootText = string.Empty;
        DigitSumText = string.Empty;
        Mod26Text = string.Empty;
        Mod10Text = string.Empty;
        ReversedTotalText = string.Empty;
        HasWarning = false;
        WarningMessage = string.Empty;
    }

    private void BuildReference()
    {
        ReferenceCells.Clear();
        IReadOnlyList<int> table;
        if (IsCustomSystem)
        {
            if (!TryParseCustomTable(out table))
            {
                return; // No reference until the custom table parses.
            }
        }
        else
        {
            table = ScrabbleScorer.GetTable(SelectedSystem);
        }

        for (var i = 0; i < 26; i++)
        {
            ReferenceCells.Add(new ReferenceCell(((char)('A' + i)).ToString(), table[i]));
        }
    }

    private bool TryParseCustomTable(out IReadOnlyList<int> table)
    {
        var parts = CustomTableText.Split([',', ' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 26)
        {
            table = [];
            return false;
        }

        var values = new int[26];
        for (var i = 0; i < 26; i++)
        {
            if (!int.TryParse(parts[i], System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out values[i]))
            {
                table = [];
                return false;
            }
        }

        table = values;
        return true;
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyTotal() => _clipboard.SetText(GrandTotal.ToString(System.Globalization.CultureInfo.CurrentCulture));

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyWords()
    {
        StringBuilder builder = new();
        foreach (var item in WordResults)
        {
            builder.Append(item.Word).Append(" = ").Append(item.Total).AppendLine();
        }

        builder.Append(_localizer["ScrabbleTotalLabel"].Value).Append(' ').Append(GrandTotal);
        _clipboard.SetText(builder.ToString());
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task ShareAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, $"{InputText} = {GrandTotal}");
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
