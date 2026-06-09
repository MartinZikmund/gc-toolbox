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

/// <summary>
/// Euler's number (e) digits tool (issue #20). Looks up the embedded 1,000,000-decimal expansion of e:
/// the first N decimals, the decimal at a position, a position range, and all occurrences of a digit
/// sequence — matching geocachingtoolbox.com, and exceeding it with an occurrence count + context
/// digits per hit, grouped/numbered output formatting, and copy/share. All digit logic lives in the
/// pure <see cref="EulerNumberDigits"/>; the expansion loads lazily off the UI thread.
/// </summary>
[Tool("EulerNumber", ToolCategory.Numbers,
      Introduced = "2026-06-09", Updated = "2026-06-09",
      Keywords = ["euler", "e", "digits", "decimals", "constant", "2.718", "eulerovo číslo", "číslice", "desetinná místa", "konstanta"])]
public sealed partial class EulerNumberViewModel : ToolViewModelBase
{
    private const int MaxDisplayedMatches = 200;
    private const int MatchContextLength = 10;

    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    private int _computeVersion;

    public EulerNumberViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("EulerNumber", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;
        Recompute();
    }

    /// <summary>0 = first N decimals, 1 = decimal at position, 2 = position range, 3 = find sequence.</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial bool IsFirstMode { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPositionMode { get; set; }

    [ObservableProperty]
    public partial bool IsRangeMode { get; set; }

    [ObservableProperty]
    public partial bool IsSearchMode { get; set; }

    /// <summary>Grouping/numbering options only apply to multi-digit output (first N and range modes).</summary>
    [ObservableProperty]
    public partial bool ShowFormattingOptions { get; set; } = true;

    [ObservableProperty]
    public partial string FirstCountText { get; set; } = "100";

    [ObservableProperty]
    public partial string PositionText { get; set; } = "1";

    [ObservableProperty]
    public partial string RangeFromText { get; set; } = "1";

    [ObservableProperty]
    public partial string RangeToText { get; set; } = "100";

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Group the output in blocks of 10 digits, 50 per line.</summary>
    [ObservableProperty]
    public partial bool GroupDigits { get; set; } = true;

    /// <summary>Prefix each output line with the position of its first digit.</summary>
    [ObservableProperty]
    public partial bool ShowPositions { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>Context digits around the single looked-up decimal (position mode only).</summary>
    [ObservableProperty]
    public partial string PositionContextText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasPositionContext { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string OccurrenceSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOccurrenceSummary { get; set; }

    /// <summary>The found occurrences (search mode), capped at <see cref="MaxDisplayedMatches"/>.</summary>
    public ObservableCollection<EulerNumberOccurrenceItem> Occurrences { get; } = [];

    partial void OnModeIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit while re-templating.
        if (value is not (>= 0 and <= 3))
        {
            return;
        }

        IsFirstMode = value == 0;
        IsPositionMode = value == 1;
        IsRangeMode = value == 2;
        IsSearchMode = value == 3;
        ShowFormattingOptions = IsFirstMode || IsRangeMode;
        Recompute();
    }

    partial void OnFirstCountTextChanged(string value) => Recompute();

    partial void OnPositionTextChanged(string value) => Recompute();

    partial void OnRangeFromTextChanged(string value) => Recompute();

    partial void OnRangeToTextChanged(string value) => Recompute();

    partial void OnSearchTextChanged(string value) => Recompute();

    partial void OnGroupDigitsChanged(bool value) => Recompute();

    partial void OnShowPositionsChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value) => NotifyActionCommands();

    partial void OnHasOccurrenceSummaryChanged(bool value) => NotifyActionCommands();

    private void NotifyActionCommands()
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private bool CanCopyOrShare => HasOutput || HasOccurrenceSummary;

    private void Recompute() => _ = RecomputeAsync();

    private async Task RecomputeAsync()
    {
        var version = ++_computeVersion;
        try
        {
            var loadTask = EulerNumberDigitSource.GetDecimalsAsync();
            if (!loadTask.IsCompleted)
            {
                IsBusy = true;
            }

            var decimals = await loadTask;
            var result = await Task.Run(() => Compute(new EulerNumberDigits(decimals)));
            if (version != _computeVersion)
            {
                return;
            }

            Apply(result);
        }
        catch (Exception)
        {
            // A failed lookup must never crash the tool; surface a generic error instead.
            if (version == _computeVersion)
            {
                Apply(ComputeResult.Error(_localizer["EulerLoadError"].Value));
            }
        }
        finally
        {
            if (version == _computeVersion)
            {
                IsBusy = false;
            }
        }
    }

    private ComputeResult Compute(EulerNumberDigits digits) => ModeIndex switch
    {
        0 => ComputeFirst(digits),
        1 => ComputePosition(digits),
        2 => ComputeRange(digits),
        3 => ComputeSearch(digits),
        _ => ComputeResult.Empty,
    };

    private ComputeResult ComputeFirst(EulerNumberDigits digits)
    {
        if (string.IsNullOrWhiteSpace(FirstCountText))
        {
            return ComputeResult.Empty;
        }

        if (!TryParsePosition(FirstCountText, digits.Count, out var count))
        {
            return ComputeResult.Error(_localizer["EulerCountError"].Value);
        }

        return ComputeResult.Output(EulerNumberDigits.FormatGrouped(digits.First(count), 1, GroupDigits, ShowPositions));
    }

    private ComputeResult ComputePosition(EulerNumberDigits digits)
    {
        if (string.IsNullOrWhiteSpace(PositionText))
        {
            return ComputeResult.Empty;
        }

        if (!TryParsePosition(PositionText, digits.Count, out var position))
        {
            return ComputeResult.Error(_localizer["EulerPositionError"].Value);
        }

        // Exceeds parity: show the neighborhood with the looked-up decimal bracketed.
        var beforeStart = Math.Max(1, position - MatchContextLength);
        var afterEnd = Math.Min(digits.Count, position + MatchContextLength);
        var before = position == 1 ? string.Empty : digits.Range(beforeStart, position - 1);
        var after = position == digits.Count ? string.Empty : digits.Range(position + 1, afterEnd);
        var prefix = beforeStart > 1 ? "…" : string.Empty;
        var suffix = afterEnd < digits.Count ? "…" : string.Empty;
        var context = $"{prefix}{before}[{digits.DigitAt(position)}]{after}{suffix}";

        return ComputeResult.Output(digits.DigitAt(position).ToString(), context);
    }

    private ComputeResult ComputeRange(EulerNumberDigits digits)
    {
        if (string.IsNullOrWhiteSpace(RangeFromText) && string.IsNullOrWhiteSpace(RangeToText))
        {
            return ComputeResult.Empty;
        }

        if (!TryParsePosition(RangeFromText, digits.Count, out var from)
            || !TryParsePosition(RangeToText, digits.Count, out var to)
            || to < from)
        {
            return ComputeResult.Error(_localizer["EulerRangeError"].Value);
        }

        return ComputeResult.Output(EulerNumberDigits.FormatGrouped(digits.Range(from, to), from, GroupDigits, ShowPositions));
    }

    private ComputeResult ComputeSearch(EulerNumberDigits digits)
    {
        var pattern = SearchText.Trim();
        if (pattern.Length == 0)
        {
            return ComputeResult.Empty;
        }

        if (!pattern.All(char.IsAsciiDigit))
        {
            return ComputeResult.Error(_localizer["EulerSearchError"].Value);
        }

        var result = digits.Find(pattern, MaxDisplayedMatches, MatchContextLength);
        var summary = result.TotalCount switch
        {
            0 => _localizer["EulerSearchNoMatch"].Value,
            > MaxDisplayedMatches => _localizer["EulerSearchTruncated", result.TotalCount, MaxDisplayedMatches].Value,
            _ => _localizer["EulerSearchCount", result.TotalCount].Value,
        };

        return ComputeResult.Search(summary, result.Matches);
    }

    private static bool TryParsePosition(string text, int max, out int value)
        => int.TryParse(text.Trim(), out value) && value >= 1 && value <= max;

    private void Apply(ComputeResult result)
    {
        ErrorText = result.ErrorText;
        HasError = result.ErrorText.Length > 0;
        OutputText = result.OutputText;
        HasOutput = result.OutputText.Length > 0;
        PositionContextText = result.PositionContextText;
        HasPositionContext = result.PositionContextText.Length > 0;
        OccurrenceSummary = result.OccurrenceSummary;
        HasOccurrenceSummary = result.OccurrenceSummary.Length > 0;

        Occurrences.Clear();
        foreach (var match in result.Matches)
        {
            Occurrences.Add(new EulerNumberOccurrenceItem(match, _clipboard.SetText));
        }
    }

    /// <summary>The text the Copy/Share actions emit for the active mode.</summary>
    private string BuildResultText()
    {
        if (IsSearchMode)
        {
            var lines = Occurrences.Select(o => $"{o.Position}: {o.ContextText}");
            return string.Join(Environment.NewLine, lines.Prepend(OccurrenceSummary));
        }

        if (IsPositionMode && HasPositionContext)
        {
            return $"{OutputText} ({PositionContextText})";
        }

        return OutputText;
    }

    [RelayCommand(CanExecute = nameof(CanCopyOrShare))]
    private void CopyOutput() => _clipboard.SetText(BuildResultText());

    [RelayCommand(CanExecute = nameof(CanCopyOrShare))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildResultText());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    private sealed record ComputeResult(
        string OutputText,
        string PositionContextText,
        string ErrorText,
        string OccurrenceSummary,
        IReadOnlyList<EulerNumberMatch> Matches)
    {
        public static readonly ComputeResult Empty = new(string.Empty, string.Empty, string.Empty, string.Empty, []);

        public static ComputeResult Output(string output, string positionContext = "")
            => Empty with { OutputText = output, PositionContextText = positionContext };

        public static ComputeResult Error(string error) => Empty with { ErrorText = error };

        public static ComputeResult Search(string summary, IReadOnlyList<EulerNumberMatch> matches)
            => Empty with { OccurrenceSummary = summary, Matches = matches };
    }
}
