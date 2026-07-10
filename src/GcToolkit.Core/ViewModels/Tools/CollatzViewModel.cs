using System.Globalization;
using System.Numerics;
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
/// Collatz conjecture (3n+1) explorer (issue #152). A live "Trace" section turns a starting number into
/// its full hailstone sequence plus stopping time, peak and length, and a "Most stubborn number" search
/// finds the longest sequence up to a limit (cachesleuth.com parity, capped at 1,000,000). Goes beyond
/// parity with arbitrary-precision <see cref="BigInteger"/> starters, an even/odd step split, and a
/// parity bit-string. All math lives in the pure <see cref="CollatzCalculator"/> (thin-VM convention).
/// </summary>
[Tool("Collatz", ToolCategory.Numbers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["collatz", "3n+1", "hailstone", "conjecture", "sequence", "stopping time", "kroupy", "domněnka", "posloupnost", "číslo"])]
public sealed partial class CollatzViewModel : ToolViewModelBase
{
    private readonly CollatzCalculator _calculator = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public CollatzViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Collatz", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        StartInput = "27";
    }

    // ---- Trace section ----

    [ObservableProperty]
    public partial string StartInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SequenceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTrace { get; set; }

    [ObservableProperty]
    public partial bool HasTraceError { get; set; }

    [ObservableProperty]
    public partial string Steps { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Peak { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Length { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EvenOddSplit { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ParityBits { get; set; } = string.Empty;

    // ---- Most-stubborn search section ----

    [ObservableProperty]
    public partial string LimitInput { get; set; } = "1000";

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial bool HasSearchResult { get; set; }

    [ObservableProperty]
    public partial bool HasSearchError { get; set; }

    [ObservableProperty]
    public partial string SearchResult { get; set; } = string.Empty;

    partial void OnStartInputChanged(string value) => RecomputeTrace();

    partial void OnHasTraceChanged(bool value)
    {
        CopyTraceCommand.NotifyCanExecuteChanged();
        ShareTraceCommand.NotifyCanExecuteChanged();
    }

    public override void ViewCreated()
    {
        base.ViewCreated();
        RecomputeTrace();
    }

    private void RecomputeTrace()
    {
        if (string.IsNullOrWhiteSpace(StartInput))
        {
            ClearTraceState();
            return;
        }

        if (!TryParseBigInteger(StartInput, out var start) || start < CollatzCalculator.MinValue)
        {
            ClearTraceState();
            HasTraceError = true;
            return;
        }

        var trace = _calculator.Trace(start);

        SequenceText = string.Join(" → ", trace.Sequence.Select(FormatNumber));
        Steps = FormatCount(trace.Steps);
        Peak = FormatNumber(trace.Peak);
        Length = FormatCount(trace.Length);
        EvenOddSplit = string.Format(
            CultureInfo.CurrentCulture,
            _localizer["CollatzEvenOddSplit"].Value,
            trace.EvenSteps,
            trace.OddSteps);
        ParityBits = trace.ParityBits;

        HasTrace = true;
        HasTraceError = false;
    }

    private void ClearTraceState()
    {
        SequenceText = string.Empty;
        Steps = string.Empty;
        Peak = string.Empty;
        Length = string.Empty;
        EvenOddSplit = string.Empty;
        ParityBits = string.Empty;
        HasTrace = false;
        HasTraceError = false;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        HasSearchResult = false;
        HasSearchError = false;

        if (!int.TryParse(LimitInput, NumberStyles.None, CultureInfo.InvariantCulture, out var limit)
            || limit < CollatzCalculator.MinValue
            || limit > CollatzCalculator.MaxSearchLimit)
        {
            HasSearchError = true;
            return;
        }

        IsSearching = true;
        try
        {
            // The search is CPU-bound and can run up to a million starters; keep the UI thread free.
            var result = await Task.Run(() => _calculator.MostStubborn(limit));

            SearchResult = string.Format(
                CultureInfo.CurrentCulture,
                _localizer["CollatzSearchResult"].Value,
                FormatNumber(result.Starter),
                FormatCount(result.Steps),
                FormatCount(limit));
            HasSearchResult = true;
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>Reset (cachesleuth.com parity): clears the starter and the search, restoring defaults.</summary>
    [RelayCommand]
    private void Reset()
    {
        StartInput = string.Empty;
        LimitInput = "1000";
        SearchResult = string.Empty;
        HasSearchResult = false;
        HasSearchError = false;
    }

    [RelayCommand(CanExecute = nameof(HasTrace))]
    private void CopyTrace() => _clipboard.SetText(BuildShareText());

    [RelayCommand(CanExecute = nameof(HasTrace))]
    private async Task ShareTraceAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildShareText());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    /// <summary>The text Copy/Share emit: the stats summary followed by the full sequence.</summary>
    private string BuildShareText()
    {
        var stats = string.Format(
            CultureInfo.CurrentCulture,
            _localizer["CollatzShareSummary"].Value,
            FormatNumber(ParseStartOrDefault()),
            Steps,
            Peak,
            Length);

        return $"{stats}{Environment.NewLine}{SequenceText}";
    }

    private BigInteger ParseStartOrDefault()
        => TryParseBigInteger(StartInput, out var start) ? start : BigInteger.Zero;

    private static bool TryParseBigInteger(string text, out BigInteger value)
        => BigInteger.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static string FormatNumber(BigInteger value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static string FormatCount(int value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
