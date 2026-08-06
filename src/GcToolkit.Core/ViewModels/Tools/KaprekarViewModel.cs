using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Numbers;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.UI.Dispatching;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One row of the routine step table, formatted for display.</summary>
public sealed record KaprekarStepRow(int Index, string Descending, string Ascending, string Difference);

/// <summary>
/// Kaprekar's Constant tool (issue #214): runs Kaprekar's sort-and-subtract routine toward 6174
/// (4 digits) / 495 (3 digits) with a live step table and step count, plus a Kaprekar-number checker
/// (showing the square split) and a lister. All math lives in <see cref="KaprekarCalculator"/>; this VM
/// stays thin — formatting, validation, and Copy/Share only.
/// </summary>
[Tool("Kaprekar", ToolCategory.Numbers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords =
      [
          "kaprekar", "6174", "495", "routine", "constant", "konstanta", "rutina",
          "číslo", "self number", "digit", "puzzle"
      ])]
public sealed partial class KaprekarViewModel : ToolViewModelBase
{
    private readonly KaprekarCalculator _calculator = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;
    private readonly UiDebouncer _listDebouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    private int _listGeneration;

    public KaprekarViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Kaprekar", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    // ---- Routine section ----

    [ObservableProperty]
    public partial string RoutineInput { get; set; } = string.Empty;

    /// <summary>0 = 4 digits (6174), 1 = 3 digits (495).</summary>
    [ObservableProperty]
    public partial int DigitWidthIndex { get; set; }

    [ObservableProperty]
    public partial bool HasRoutine { get; set; }

    [ObservableProperty]
    public partial bool HasRoutineError { get; set; }

    [ObservableProperty]
    public partial string RoutineError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConstantText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StepCountText { get; set; } = string.Empty;

    public ObservableCollection<KaprekarStepRow> RoutineSteps { get; } = [];

    private int DigitWidth => DigitWidthIndex == 1 ? 3 : 4;

    // ---- Checker section ----

    [ObservableProperty]
    public partial string CheckerInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCheckerResult { get; set; }

    [ObservableProperty]
    public partial string CheckerResult { get; set; } = string.Empty;

    // ---- Lister section ----

    [ObservableProperty]
    public partial string ListerInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ListerResult { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasListerResult { get; set; }

    partial void OnRoutineInputChanged(string value) => RunRoutine();

    partial void OnDigitWidthIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            RunRoutine();
        }
    }

    partial void OnCheckerInputChanged(string value) => RunCheck();

    // Listing brute-forces up to 1,000,000 Kaprekar candidates; debounce so it fires once typing
    // pauses rather than on every keystroke, then compute off the UI thread (see RunList).
    partial void OnListerInputChanged(string value) => _listDebouncer.Debounce(RunList);

    partial void OnHasRoutineChanged(bool value)
    {
        CopyRoutineCommand.NotifyCanExecuteChanged();
        ShareRoutineCommand.NotifyCanExecuteChanged();
    }

    private void RunRoutine()
    {
        RoutineSteps.Clear();
        HasRoutine = false;
        HasRoutineError = false;
        RoutineError = string.Empty;
        ConstantText = string.Empty;
        StepCountText = string.Empty;

        if (string.IsNullOrWhiteSpace(RoutineInput))
        {
            return;
        }

        var width = DigitWidth;
        if (!TryParseNonNegative(RoutineInput, out var number))
        {
            HasRoutineError = true;
            RoutineError = _localizer["KaprekarErrorNumeric"].Value;
            return;
        }

        var max = (long)Math.Pow(10, width) - 1;
        if (number > max)
        {
            HasRoutineError = true;
            RoutineError = string.Format(CultureInfo.CurrentCulture, _localizer["KaprekarErrorRange"].Value, width, max);
            return;
        }

        var result = _calculator.Routine(number, width);

        var index = 1;
        foreach (var step in result.Steps)
        {
            RoutineSteps.Add(new KaprekarStepRow(
                index++,
                Pad(step.Descending, width),
                Pad(step.Ascending, width),
                Pad(step.Difference, width)));
        }

        if (result.IsRepdigit)
        {
            HasRoutineError = true;
            RoutineError = _localizer["KaprekarErrorRepdigit"].Value;
            return;
        }

        ConstantText = result.Constant.ToString(CultureInfo.CurrentCulture);
        StepCountText = FormatStepCount(result.StepCount);
        HasRoutine = true;
    }

    private void RunCheck()
    {
        HasCheckerResult = false;
        CheckerResult = string.Empty;

        if (string.IsNullOrWhiteSpace(CheckerInput))
        {
            return;
        }

        if (!TryParseNonNegative(CheckerInput, out var number) || number < 1)
        {
            CheckerResult = _localizer["KaprekarErrorPositive"].Value;
            HasCheckerResult = true;
            return;
        }

        if (_calculator.TryDescribeKaprekar(number, out var description))
        {
            // e.g. "45 is a Kaprekar number: 45² = 2025 → 20 + 25 = 45"
            CheckerResult = string.Format(
                CultureInfo.CurrentCulture,
                _localizer["KaprekarCheckYes"].Value,
                number,
                description.Square,
                description.Left,
                description.Right);
        }
        else
        {
            CheckerResult = string.Format(CultureInfo.CurrentCulture, _localizer["KaprekarCheckNo"].Value, number);
        }

        HasCheckerResult = true;
    }

    private void RunList()
    {
        HasListerResult = false;
        ListerResult = string.Empty;

        if (string.IsNullOrWhiteSpace(ListerInput))
        {
            _listGeneration++; // discard any in-flight enumeration
            return;
        }

        if (!TryParseNonNegative(ListerInput, out var limit) || limit < 1)
        {
            _listGeneration++;
            ListerResult = _localizer["KaprekarErrorPositive"].Value;
            HasListerResult = true;
            return;
        }

        // Enumerating up to 1,000,000 would freeze the UI thread; run it on a background thread and
        // publish back on the UI thread, dropping results a newer input has already superseded.
        // Runs synchronously when there is no dispatcher (unit tests).
        var generation = ++_listGeneration;
        if (_dispatcher is null)
        {
            PublishList(_calculator.ListKaprekar(limit));
            return;
        }

        _ = Task.Run(() =>
        {
            var list = _calculator.ListKaprekar(limit);
            _dispatcher.TryEnqueue(() =>
            {
                if (generation == _listGeneration)
                {
                    PublishList(list);
                }
            });
        });
    }

    private void PublishList(IReadOnlyList<long> list)
    {
        ListerResult = string.Join(", ", list.Select(n => n.ToString(CultureInfo.CurrentCulture)));
        HasListerResult = true;
    }

    private static bool TryParseNonNegative(string text, out long number)
        => long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out number);

    private static string Pad(long value, int width)
        => value.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');

    /// <summary>
    /// Picks the correct plural form for the step count. Czech needs three forms (1 = one,
    /// 2-4 = few, 0 and 5+ = many); English collapses few/many onto the same "{0} steps" string.
    /// </summary>
    private string FormatStepCount(int stepCount)
    {
        var key = stepCount switch
        {
            1 => "KaprekarStepCountOne",
            >= 2 and <= 4 => "KaprekarStepCountFew",
            _ => "KaprekarStepCountMany",
        };

        return string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, stepCount);
    }

    private string BuildRoutineTrace()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{RoutineInput.Trim()} ({DigitWidth} {_localizer["KaprekarDigitsWord"].Value})");
        foreach (var row in RoutineSteps)
        {
            builder.AppendLine($"{row.Index}. {row.Descending} - {row.Ascending} = {row.Difference}");
        }

        builder.Append(StepCountText);
        return builder.ToString();
    }

    [RelayCommand(CanExecute = nameof(HasRoutine))]
    private void CopyRoutine() => _clipboard.SetText(BuildRoutineTrace());

    [RelayCommand(CanExecute = nameof(HasRoutine))]
    private async Task ShareRoutineAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildRoutineTrace());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => RoutineInput = string.Empty;

    /// <summary>Pre-fills a sample seed (parity with cachesleuth's "Example" button) that reaches 6174.</summary>
    [RelayCommand]
    private void Example()
    {
        DigitWidthIndex = 0;
        RoutineInput = "3524";
    }
}
