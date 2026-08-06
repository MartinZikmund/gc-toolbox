using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Numbers.GoldenRatio;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Golden ratio (φ) digits tool (issue #37). Looks up the first N decimals, the decimal at a
/// position, a position range, and all occurrences of a digit sequence within the first
/// 1,000,000 decimals — fully offline (geocachingtoolbox.com parity). Exceeds parity with
/// occurrence context digits, a context window around a looked-up position, optional line
/// position labels, and copy/share.
/// </summary>
[Tool("GoldenRatio", ToolCategory.Numbers,
      Introduced = "2026-06-10", Updated = "2026-06-10",
      Keywords = ["golden", "ratio", "phi", "fibonacci", "digits", "zlatý řez", "číslice", "konstanta"])]
public sealed partial class GoldenRatioViewModel : ToolViewModelBase
{
    private const int MaxDisplayedOccurrences = 200;
    private const int MaxSequenceLength = 100;
    private const int PositionContextRadius = 10;

    private readonly GoldenRatioDigitsProvider _provider = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    // Search scans the 1,000,000-digit string; debounce the typing path so it recomputes once
    // typing pauses instead of spawning a scan per keystroke. The other modes are cheap (RunNow).
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));

    /// <summary>Stamps each computation; results from a superseded run are dropped.</summary>
    private int _computeVersion;

    public GoldenRatioViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("GoldenRatio", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = first N decimals, 1 = decimal at position, 2 = position range, 3 = find sequence.</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial string CountText { get; set; } = "100";

    [ObservableProperty]
    public partial string PositionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RangeFromText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RangeToText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Group the digits into blocks of 10 (geocachingtoolbox.com convention).</summary>
    [ObservableProperty]
    public partial bool GroupDigits { get; set; } = true;

    /// <summary>Break output into 50-digit lines labeled with the position of their last digit.</summary>
    [ObservableProperty]
    public partial bool ShowLineNumbers { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>Localized headline above the output (digit-at result, range header, match count).</summary>
    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSummary { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>The occurrence list is shown instead of the output box in search mode.</summary>
    [ObservableProperty]
    public partial bool ShowOccurrences { get; set; }

    [ObservableProperty]
    public partial bool ShowOutputBox { get; set; }

    public ObservableCollection<GoldenRatioOccurrenceItem> Occurrences { get; } = [];

    public bool IsModeFirst => ModeIndex == 0;

    public bool IsModeDigitAt => ModeIndex == 1;

    public bool IsModeRange => ModeIndex == 2;

    public bool IsModeSearch => ModeIndex == 3;

    /// <summary>Block grouping / line labels only apply to the digit-run modes.</summary>
    public bool ShowFormattingOptions => ModeIndex is 0 or 2;

    public override void ViewCreated()
    {
        base.ViewCreated();
        ComputeCommand.Execute(null);
    }

    partial void OnModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsModeFirst));
        OnPropertyChanged(nameof(IsModeDigitAt));
        OnPropertyChanged(nameof(IsModeRange));
        OnPropertyChanged(nameof(IsModeSearch));
        OnPropertyChanged(nameof(ShowFormattingOptions));

        // Ignore the transient -1 a RadioButtons control can emit during template changes.
        if (value is >= 0 and <= 3)
        {
            _debouncer.RunNow(TriggerCompute);
        }
    }

    private void TriggerCompute() => ComputeCommand.Execute(null);

    partial void OnCountTextChanged(string value) => _debouncer.RunNow(TriggerCompute);

    partial void OnPositionTextChanged(string value) => _debouncer.RunNow(TriggerCompute);

    partial void OnRangeFromTextChanged(string value) => _debouncer.RunNow(TriggerCompute);

    partial void OnRangeToTextChanged(string value) => _debouncer.RunNow(TriggerCompute);

    // Debounced: each keystroke would otherwise brute-force-scan all 1,000,000 digits.
    partial void OnSearchTextChanged(string value) => _debouncer.Debounce(TriggerCompute);

    partial void OnGroupDigitsChanged(bool value) => _debouncer.RunNow(TriggerCompute);

    partial void OnShowLineNumbersChanged(bool value) => _debouncer.RunNow(TriggerCompute);

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task ComputeAsync()
    {
        var version = ++_computeVersion;
        if (!TryDescribeRequest(out var request, out var error))
        {
            ApplyError(error);
            return;
        }

        if (request is null)
        {
            ApplyCleared();
            return;
        }

        IsBusy = true;
        try
        {
            var decimals = await _provider.GetDecimalsAsync();
            var result = await Task.Run(() => Execute(request, decimals));
            if (version != _computeVersion)
            {
                return;
            }

            ApplyResult(result);
        }
        finally
        {
            if (version == _computeVersion)
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(BuildExportText());

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildExportText());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    // --- request parsing -------------------------------------------------------------------

    private abstract record Request;

    private sealed record FirstDecimalsRequest(int Count) : Request;

    private sealed record DigitAtRequest(int Position) : Request;

    private sealed record RangeRequest(int From, int To) : Request;

    private sealed record SearchRequest(string Pattern) : Request;

    /// <summary>Parses the active mode's inputs. A <see langword="null"/> request with no error
    /// means "inputs empty — clear the output silently".</summary>
    private bool TryDescribeRequest(out Request? request, out string error)
    {
        request = null;
        error = string.Empty;

        switch (ModeIndex)
        {
            case 0:
                if (IsBlank(CountText))
                {
                    return true;
                }

                if (!TryParsePosition(CountText, out var count))
                {
                    error = _localizer["GoldenRatioErrorCount"].Value;
                    return false;
                }

                request = new FirstDecimalsRequest(count);
                return true;

            case 1:
                if (IsBlank(PositionText))
                {
                    return true;
                }

                if (!TryParsePosition(PositionText, out var position))
                {
                    error = _localizer["GoldenRatioErrorPosition"].Value;
                    return false;
                }

                request = new DigitAtRequest(position);
                return true;

            case 2:
                if (IsBlank(RangeFromText) && IsBlank(RangeToText))
                {
                    return true;
                }

                if (!TryParsePosition(RangeFromText, out var from)
                    || !TryParsePosition(RangeToText, out var to)
                    || to < from)
                {
                    error = _localizer["GoldenRatioErrorRange"].Value;
                    return false;
                }

                request = new RangeRequest(from, to);
                return true;

            default:
                if (IsBlank(SearchText))
                {
                    return true;
                }

                var pattern = Normalize(SearchText);
                if (pattern.Length is 0 or > MaxSequenceLength || !pattern.All(char.IsAsciiDigit))
                {
                    error = _localizer["GoldenRatioErrorSequence"].Value;
                    return false;
                }

                request = new SearchRequest(pattern);
                return true;
        }
    }

    private static bool IsBlank(string text) => string.IsNullOrWhiteSpace(text);

    /// <summary>Strips spaces and thousands separators so "10 000" and "10,000" both parse.</summary>
    private static string Normalize(string text)
        => text.Replace(" ", string.Empty).Replace(",", string.Empty).Trim();

    private static bool TryParsePosition(string text, out int value)
        => int.TryParse(Normalize(text), NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value is >= 1 and <= GoldenRatioDigitsProvider.MaxDecimals;

    // --- computation (thread pool) ---------------------------------------------------------

    private sealed record ComputeResult(
        string Output,
        string Summary,
        bool ShowOutputBox,
        IReadOnlyList<GoldenRatioOccurrenceItem> Occurrences);

    private ComputeResult Execute(Request request, string decimals)
        => request switch
        {
            FirstDecimalsRequest first => ExecuteFirstDecimals(first, decimals),
            DigitAtRequest digitAt => ExecuteDigitAt(digitAt, decimals),
            RangeRequest range => ExecuteRange(range, decimals),
            SearchRequest search => ExecuteSearch(search, decimals),
            _ => throw new InvalidOperationException(),
        };

    private ComputeResult ExecuteFirstDecimals(FirstDecimalsRequest request, string decimals)
    {
        var body = GoldenRatioDigitFormatter.Format(
            GoldenRatioDigitOperations.Slice(decimals, 1, request.Count), 1, GroupDigits, ShowLineNumbers);
        var output = ShowLineNumbers
            ? $"φ = 1.\n{body}"
            : $"φ = 1.{body} ({FormatNumber(request.Count)})";
        return new ComputeResult(output, string.Empty, ShowOutputBox: true, []);
    }

    private ComputeResult ExecuteDigitAt(DigitAtRequest request, string decimals)
    {
        var digit = GoldenRatioDigitOperations.DigitAt(decimals, request.Position);
        var (before, after) = GoldenRatioDigitOperations.Context(
            decimals, request.Position, 1, PositionContextRadius);
        var prefix = request.Position - 1 > PositionContextRadius ? "…" : string.Empty;
        var suffix = request.Position + PositionContextRadius < decimals.Length ? "…" : string.Empty;
        var summary = string.Format(
            CultureInfo.CurrentCulture, _localizer["GoldenRatioDigitAtSummary"].Value,
            FormatNumber(request.Position), digit);
        return new ComputeResult($"{prefix}{before}[{digit}]{after}{suffix}", summary, ShowOutputBox: true, []);
    }

    private ComputeResult ExecuteRange(RangeRequest request, string decimals)
    {
        var slice = GoldenRatioDigitOperations.Slice(decimals, request.From, request.To);
        var body = GoldenRatioDigitFormatter.Format(slice, request.From, GroupDigits, ShowLineNumbers);
        var summary = string.Format(
            CultureInfo.CurrentCulture, _localizer["GoldenRatioRangeSummary"].Value,
            FormatNumber(request.From), FormatNumber(request.To), FormatNumber(slice.Length));
        return new ComputeResult(body, summary, ShowOutputBox: true, []);
    }

    private ComputeResult ExecuteSearch(SearchRequest request, string decimals)
    {
        var result = GoldenRatioDigitOperations.FindOccurrences(decimals, request.Pattern, MaxDisplayedOccurrences);
        var summary = result.TotalCount == 0
            ? _localizer["GoldenRatioSearchNone"].Value
            : string.Format(
                CultureInfo.CurrentCulture, _localizer["GoldenRatioSearchSummary"].Value,
                FormatNumber(result.TotalCount));
        if (result.IsTruncated)
        {
            summary += " " + string.Format(
                CultureInfo.CurrentCulture, _localizer["GoldenRatioSearchTruncated"].Value,
                FormatNumber(result.Occurrences.Count));
        }

        List<GoldenRatioOccurrenceItem> items = [.. result.Occurrences.Select(o => new GoldenRatioOccurrenceItem(
            FormatNumber(o.Position),
            (o.Position - 1 > GoldenRatioDigitOperations.ContextRadius ? "…" : string.Empty) + o.Before,
            o.Match,
            o.After + "…"))];
        return new ComputeResult(string.Empty, summary, ShowOutputBox: false, items);
    }

    private static string FormatNumber(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    // --- applying results ------------------------------------------------------------------

    private void ApplyResult(ComputeResult result)
    {
        OutputText = result.Output;
        SummaryText = result.Summary;
        HasSummary = result.Summary.Length > 0;
        ShowOutputBox = result.ShowOutputBox && result.Output.Length > 0;
        Occurrences.Clear();
        foreach (var item in result.Occurrences)
        {
            Occurrences.Add(item);
        }

        ShowOccurrences = Occurrences.Count > 0;
        HasOutput = result.Output.Length > 0 || Occurrences.Count > 0;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private void ApplyError(string message)
    {
        ApplyCleared();
        HasError = true;
        ErrorMessage = message;
    }

    private void ApplyCleared()
    {
        OutputText = string.Empty;
        SummaryText = string.Empty;
        HasSummary = false;
        ShowOutputBox = false;
        Occurrences.Clear();
        ShowOccurrences = false;
        HasOutput = false;
        HasError = false;
        ErrorMessage = string.Empty;
        IsBusy = false;
    }

    /// <summary>Copy/share text: the summary plus either the output or the occurrence list.</summary>
    private string BuildExportText()
    {
        List<string> parts = [];
        if (HasSummary)
        {
            parts.Add(SummaryText);
        }

        if (OutputText.Length > 0)
        {
            parts.Add(OutputText);
        }

        if (ShowOccurrences)
        {
            parts.AddRange(Occurrences.Select(o => $"{o.Position}: {o.Before}[{o.Match}]{o.After}"));
        }

        return string.Join("\n", parts);
    }
}

/// <summary>One displayed search match; plain strings so the item template binds directly.</summary>
public sealed record GoldenRatioOccurrenceItem(string Position, string Before, string Match, string After)
{
    // ListView rows with no AutomationProperties.Name announce the item's ToString to screen
    // readers; return the match content instead of the generated record form.
    public override string ToString() => $"{Position}: {Before}{Match}{After}";
}
