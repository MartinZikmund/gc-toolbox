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
/// Fibonacci numbers tool (issue #32). Mirrors the five geocachingtoolbox.com functions — membership
/// check, value at a position, a range of positions, position of a value, and all members with a given
/// digit count — using zero-based indexing (F(0) = 0, F(1) = 1). Exceeds parity with positions up to
/// 100,000 (vs 10,000), nearest-neighbor hints for non-members, and copy/share. Computation runs off
/// the UI thread; results are version-stamped so stale completions are dropped.
/// </summary>
[Tool("Fibonacci", ToolCategory.Numbers,
      Introduced = "2026-06-10", Updated = "2026-06-10",
      Keywords = ["fibonacci", "sequence", "golden", "spiral", "Fibonacciho posloupnost", "posloupnost", "zlatý řez"])]
public sealed partial class FibonacciViewModel : ToolViewModelBase
{
    /// <summary>Largest from→to span the range mode renders, to keep the output box sane.</summary>
    public const int MaxRangeSpan = 1_000;

    private readonly FibonacciCalculator _calculator = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private int _computeVersion;

    public FibonacciViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Fibonacci", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = membership check, 1 = value at position, 2 = range of positions,
    /// 3 = position of value, 4 = members with a digit count.</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    /// <summary>The value to check/locate (modes 0 and 3).</summary>
    [ObservableProperty]
    public partial string ValueInput { get; set; } = string.Empty;

    /// <summary>The position n (mode 1).</summary>
    [ObservableProperty]
    public partial string IndexInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FromInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ToInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DigitsInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowPositions { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowDigitCounts { get; set; } = true;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool IsValueInputVisible => ModeIndex is 0 or 3;

    public bool IsIndexInputVisible => ModeIndex is 1;

    public bool IsRangeInputVisible => ModeIndex is 2;

    public bool IsDigitsInputVisible => ModeIndex is 4;

    partial void OnModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsValueInputVisible));
        OnPropertyChanged(nameof(IsIndexInputVisible));
        OnPropertyChanged(nameof(IsRangeInputVisible));
        OnPropertyChanged(nameof(IsDigitsInputVisible));
        Recompute();
    }

    partial void OnValueInputChanged(string value) => Recompute();

    partial void OnIndexInputChanged(string value) => Recompute();

    partial void OnFromInputChanged(string value) => Recompute();

    partial void OnToInputChanged(string value) => Recompute();

    partial void OnDigitsInputChanged(string value) => Recompute();

    partial void OnShowPositionsChanged(bool value) => Recompute();

    partial void OnShowDigitCountsChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute() => _ = RecomputeAsync();

    private async Task RecomputeAsync()
    {
        var version = ++_computeVersion;

        if (!TryValidate(out var compute, out var error))
        {
            SetError(error);
            return;
        }

        if (compute is null)
        {
            SetIdle();
            return;
        }

        IsBusy = true;
        string output;
        try
        {
            output = await Task.Run(compute);
        }
        catch (Exception)
        {
            // Inputs are pre-validated; any residual failure surfaces as a generic invalid-input error.
            if (version == _computeVersion)
            {
                SetError(_localizer["FibonacciErrorNotInteger"].Value);
            }

            return;
        }

        if (version != _computeVersion)
        {
            return;
        }

        OutputText = output;
        HasOutput = output.Length > 0;
        HasError = false;
        ErrorMessage = string.Empty;
        IsBusy = false;
    }

    /// <summary>
    /// Validates the active mode's inputs. Returns a deferred computation (run off the UI thread)
    /// for valid input, <see langword="null"/> compute for empty input, or <see langword="false"/>
    /// with a localized error.
    /// </summary>
    private bool TryValidate(out Func<string>? compute, out string error)
    {
        compute = null;
        error = string.Empty;

        switch (ModeIndex)
        {
            case 0 or 3:
                if (string.IsNullOrWhiteSpace(ValueInput))
                {
                    return true;
                }

                if (!TryParseBigInteger(ValueInput, out var value))
                {
                    error = _localizer["FibonacciErrorNotInteger"].Value;
                    return false;
                }

                if (value.Sign < 0)
                {
                    error = _localizer["FibonacciErrorNegative"].Value;
                    return false;
                }

                compute = () => FormatLookup(value);
                return true;

            case 1:
                if (string.IsNullOrWhiteSpace(IndexInput))
                {
                    return true;
                }

                if (!TryParseIndex(IndexInput, out var index, ref error))
                {
                    return false;
                }

                compute = () => FormatEntries(_calculator.Range(index, index));
                return true;

            case 2:
                if (string.IsNullOrWhiteSpace(FromInput) && string.IsNullOrWhiteSpace(ToInput))
                {
                    return true;
                }

                if (!TryParseIndex(FromInput, out var from, ref error) || !TryParseIndex(ToInput, out var to, ref error))
                {
                    return false;
                }

                if (from > to)
                {
                    error = _localizer["FibonacciErrorRangeOrder"].Value;
                    return false;
                }

                if (to - from + 1 > MaxRangeSpan)
                {
                    error = string.Format(CultureInfo.CurrentCulture, _localizer["FibonacciErrorRangeSpan"].Value, MaxRangeSpan);
                    return false;
                }

                compute = () => FormatEntries(_calculator.Range(from, to));
                return true;

            default:
                if (string.IsNullOrWhiteSpace(DigitsInput))
                {
                    return true;
                }

                if (!int.TryParse(DigitsInput.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var digits)
                    || digits < 1
                    || digits > FibonacciCalculator.MaxDigitCount)
                {
                    error = string.Format(CultureInfo.CurrentCulture, _localizer["FibonacciErrorDigitsRange"].Value, FibonacciCalculator.MaxDigitCount);
                    return false;
                }

                compute = () => FormatEntries(_calculator.WithDigitCount(digits));
                return true;
        }
    }

    private static bool TryParseBigInteger(string text, out BigInteger value)
        => BigInteger.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

    private bool TryParseIndex(string text, out int index, ref string error)
    {
        if (int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out index)
            && index <= FibonacciCalculator.MaxIndex)
        {
            return true;
        }

        index = 0;
        error = string.Format(CultureInfo.CurrentCulture, _localizer["FibonacciErrorIndexRange"].Value, FibonacciCalculator.MaxIndex);
        return false;
    }

    private string FormatLookup(BigInteger value)
    {
        var lookup = _calculator.Locate(value);
        if (lookup is null)
        {
            return _localizer["FibonacciValueBeyondRange"].Value;
        }

        if (lookup.IsMember)
        {
            return string.Format(CultureInfo.CurrentCulture, _localizer["FibonacciIsMember"].Value, value, lookup.Index);
        }

        StringBuilder builder = new();
        builder.AppendLine(string.Format(CultureInfo.CurrentCulture, _localizer["FibonacciIsNotMember"].Value, value));
        if (lookup.Below is { } below)
        {
            builder.AppendLine(string.Format(CultureInfo.CurrentCulture, _localizer["FibonacciNearestBelow"].Value, below.Index, below.Value));
        }

        if (lookup.Above is { } above)
        {
            builder.Append(string.Format(CultureInfo.CurrentCulture, _localizer["FibonacciNearestAbove"].Value, above.Index, above.Value));
        }

        return builder.ToString();
    }

    private string FormatEntries(IReadOnlyList<FibonacciEntry> entries)
    {
        if (entries.Count == 0)
        {
            return _localizer["FibonacciNoResults"].Value;
        }

        StringBuilder builder = new();
        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            AppendEntry(builder, entries[i]);
        }

        return builder.ToString();
    }

    private void AppendEntry(StringBuilder builder, FibonacciEntry entry)
    {
        if (ShowPositions)
        {
            builder.Append(CultureInfo.InvariantCulture, $"F({entry.Index}) = ");
        }

        builder.Append(entry.Value);

        if (ShowDigitCounts)
        {
            builder.Append(' ').Append(FormatDigitCount(FibonacciCalculator.GetDigitCount(entry.Value)));
        }
    }

    /// <summary>Digit-count annotation with simple plural selection (1 / 2–4 / 5+), which covers Czech and English.</summary>
    private string FormatDigitCount(int count)
    {
        var key = count switch
        {
            1 => "FibonacciDigitsOne",
            >= 2 and <= 4 => "FibonacciDigitsFew",
            _ => "FibonacciDigitsMany",
        };

        return string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, count);
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
        OutputText = string.Empty;
        HasOutput = false;
        IsBusy = false;
    }

    private void SetIdle()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        OutputText = string.Empty;
        HasOutput = false;
        IsBusy = false;
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
    private void Clear()
    {
        ValueInput = string.Empty;
        IndexInput = string.Empty;
        FromInput = string.Empty;
        ToInput = string.Empty;
        DigitsInput = string.Empty;
    }
}
