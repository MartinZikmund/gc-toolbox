using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
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
/// Lucas numbers explorer (issue #40): the Fibonacci-style sequence <c>L(0)=2, L(1)=1,
/// L(n)=L(n-1)+L(n-2)</c>, zero-based. Four live modes — single position, inclusive range, reverse
/// lookup by value, and a base-10 digit-count filter — all driven by <see cref="LucasNumberSequence"/>.
/// The VM stays thin: it parses input, calls the sequence, formats results with the current culture's
/// thousands separators, and surfaces clear validation messages. Copy/Share emit the full result set.
/// </summary>
[Tool("LucasNumbers", ToolCategory.Numbers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["lucas", "sequence", "number", "fibonacci", "posloupnost", "číslo", "čísla", "lucasova", "lucasovo", "2 1 3 4 7"])]
public sealed partial class LucasNumbersViewModel : ToolViewModelBase
{
    /// <summary>Largest span returned for a range request — keeps the UI list bounded.</summary>
    public const int MaxRangeSpan = 1000;

    private readonly LucasNumberSequence _sequence = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public LucasNumbersViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("LucasNumbers", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Compute();
    }

    /// <summary>0 = position, 1 = range, 2 = by value (reverse), 3 = by digit count.</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    public bool IsPositionMode => ModeIndex == 0;

    public bool IsRangeMode => ModeIndex == 1;

    public bool IsByValueMode => ModeIndex == 2;

    public bool IsByDigitsMode => ModeIndex == 3;

    /// <summary>List-style modes (range, digit count) show the columns/options; single-value modes don't.</summary>
    public bool IsListMode => ModeIndex is 1 or 3;

    [ObservableProperty]
    public partial string PositionInput { get; set; } = "10";

    [ObservableProperty]
    public partial string RangeStartInput { get; set; } = "0";

    [ObservableProperty]
    public partial string RangeEndInput { get; set; } = "10";

    [ObservableProperty]
    public partial string ValueInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DigitCountInput { get; set; } = "1";

    /// <summary>When set, result rows show the index column.</summary>
    [ObservableProperty]
    public partial bool ShowPosition { get; set; } = true;

    /// <summary>When set, result rows show the digit-count column.</summary>
    [ObservableProperty]
    public partial bool ShowDigitCount { get; set; }

    /// <summary>Result rows for list-style modes (range, digit count).</summary>
    public ObservableCollection<LucasNumberItem> Results { get; } = [];

    /// <summary><see langword="true"/> for single-position / single-value modes, which use the big display.</summary>
    [ObservableProperty]
    public partial bool ShowSingleResult { get; set; }

    /// <summary>The big numeric display for single-value modes (position, reverse lookup).</summary>
    [ObservableProperty]
    public partial string SingleResultValue { get; set; } = string.Empty;

    /// <summary>Caption under the single-result display (e.g. <c>"L(10)"</c> or the resolved index).</summary>
    [ObservableProperty]
    public partial string SingleResultCaption { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResults { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    partial void OnModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsPositionMode));
        OnPropertyChanged(nameof(IsRangeMode));
        OnPropertyChanged(nameof(IsByValueMode));
        OnPropertyChanged(nameof(IsByDigitsMode));
        OnPropertyChanged(nameof(IsListMode));

        if (value is 0 or 1 or 2 or 3)
        {
            Compute();
        }
    }

    partial void OnPositionInputChanged(string value) => Compute();

    partial void OnRangeStartInputChanged(string value) => Compute();

    partial void OnRangeEndInputChanged(string value) => Compute();

    partial void OnValueInputChanged(string value) => Compute();

    partial void OnDigitCountInputChanged(string value) => Compute();

    partial void OnShowPositionChanged(bool value) => Compute();

    partial void OnShowDigitCountChanged(bool value) => Compute();

    partial void OnHasResultsChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Compute()
    {
        Results.Clear();
        ShowSingleResult = false;
        SingleResultValue = string.Empty;
        SingleResultCaption = string.Empty;
        HasResults = false;
        HasError = false;
        ErrorMessage = string.Empty;

        switch (ModeIndex)
        {
            case 0:
                ComputePosition();
                break;
            case 1:
                ComputeRange();
                break;
            case 2:
                ComputeByValue();
                break;
            case 3:
                ComputeByDigitCount();
                break;
        }
    }

    private void ComputePosition()
    {
        if (string.IsNullOrWhiteSpace(PositionInput))
        {
            return;
        }

        if (!TryParseIndex(PositionInput, out var n))
        {
            ShowError("LucasErrorInvalidIndex");
            return;
        }

        var value = _sequence.At(n);
        ShowSingleResult = true;
        SingleResultValue = Format(value);
        SingleResultCaption = _localizer["LucasPositionCaption"].Value.Replace("{0}", n.ToString(CultureInfo.CurrentCulture));
        HasResults = true;
    }

    private void ComputeRange()
    {
        if (string.IsNullOrWhiteSpace(RangeStartInput) || string.IsNullOrWhiteSpace(RangeEndInput))
        {
            return;
        }

        if (!TryParseIndex(RangeStartInput, out var start) || !TryParseIndex(RangeEndInput, out var end))
        {
            ShowError("LucasErrorInvalidIndex");
            return;
        }

        if (start > end)
        {
            ShowError("LucasErrorStartAfterEnd");
            return;
        }

        if (end - start + 1 > MaxRangeSpan)
        {
            ShowError("LucasErrorRangeTooLarge");
            return;
        }

        foreach (var (index, value) in _sequence.Range(start, end))
        {
            Results.Add(ToItem(index, value));
        }

        HasResults = Results.Count > 0;
    }

    private void ComputeByValue()
    {
        if (string.IsNullOrWhiteSpace(ValueInput))
        {
            return;
        }

        if (!TryParseBigInteger(ValueInput, out var value))
        {
            ShowError("LucasErrorInvalidValue");
            return;
        }

        if (value <= BigInteger.Zero)
        {
            // Lucas values are all positive; a non-positive input is invalid rather than "not a Lucas number".
            ShowError("LucasErrorInvalidValue");
            return;
        }

        var index = _sequence.IndexOf(value);
        if (index is null)
        {
            ShowError("LucasErrorNotLucas");
            return;
        }

        ShowSingleResult = true;
        SingleResultValue = index.Value.ToString(CultureInfo.CurrentCulture);
        SingleResultCaption = _localizer["LucasValueCaption"].Value.Replace("{0}", Format(value));
        HasResults = true;
    }

    private void ComputeByDigitCount()
    {
        if (string.IsNullOrWhiteSpace(DigitCountInput))
        {
            return;
        }

        if (!int.TryParse(DigitCountInput.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out var digits) || digits < 1)
        {
            ShowError("LucasErrorInvalidDigitCount");
            return;
        }

        foreach (var (index, value) in _sequence.WithDigitCount(digits))
        {
            Results.Add(ToItem(index, value));
        }

        if (Results.Count == 0)
        {
            // Defensive: every positive digit count has at least one Lucas number, but guard anyway.
            ShowError("LucasErrorNoMatches");
            return;
        }

        HasResults = true;
    }

    private LucasNumberItem ToItem(int index, BigInteger value)
        => new(index, Format(value), value.ToString().Length);

    private static string Format(BigInteger value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static bool TryParseIndex(string text, out int index)
    {
        if (int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out index)
            && index >= 0
            && index <= LucasNumberSequence.MaxIndex)
        {
            return true;
        }

        index = 0;
        return false;
    }

    private static bool TryParseBigInteger(string text, out BigInteger value)
        => BigInteger.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out value);

    private void ShowError(string key)
    {
        HasError = true;
        ErrorMessage = _localizer[key].Value;
    }

    private string BuildOutputText()
    {
        if (ShowSingleResult)
        {
            return $"{SingleResultCaption} = {SingleResultValue}";
        }

        var builder = new StringBuilder();
        foreach (var item in Results)
        {
            if (ShowPosition)
            {
                builder.Append("L(").Append(item.Index).Append(") = ");
            }

            builder.Append(item.Value);
            if (ShowDigitCount)
            {
                builder.Append(" (").Append(item.DigitCount).Append(')');
            }

            builder.Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }

    [RelayCommand(CanExecute = nameof(HasResults))]
    private void CopyOutput() => _clipboard.SetText(BuildOutputText());

    [RelayCommand(CanExecute = nameof(HasResults))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildOutputText());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }
}
