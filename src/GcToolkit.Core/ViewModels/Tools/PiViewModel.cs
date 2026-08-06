using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Numbers.Pi;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// π digits lookup (issue #43). Matches geocachingtoolbox.com parity — first N decimals (up to
/// 1,000,000), digit at a position, position ranges, digit-sequence search with positions, and
/// block-of-10 / line-position formatting — and exceeds it with occurrence counts + truncation
/// summary, surrounding-context previews, and copy/share. Digits ship as an embedded resource
/// (fully offline) and all heavy work runs off the UI thread.
/// </summary>
[Tool("Pi", ToolCategory.Numbers,
      Introduced = "2026-06-09", Updated = "2026-06-09",
      Keywords = ["pi", "π", "digits", "circle", "3.14", "decimals", "pí", "Ludolfovo číslo", "číslice", "desetinná místa"])]
public sealed partial class PiViewModel : ToolViewModelBase
{
    private const int MaxPatternLength = 50;
    private const int MaxListedPositions = 1_000;
    private const int MaxContextItems = 25;
    private const int ContextRadius = 10;

    private readonly PiDigitsProvider _provider = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public PiViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Pi", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = first N decimals, 1 = digit at position, 2 = range, 3 = sequence search.</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial string CountText { get; set; } = "100";

    [ObservableProperty]
    public partial string PositionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FromText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ToText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool GroupDigits { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowLineNumbers { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial string ResultSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSummary { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>Mode flags for XAML visibility (kept in sync with <see cref="ModeIndex"/>).</summary>
    [ObservableProperty]
    public partial bool IsFirstMode { get; set; } = true;

    [ObservableProperty]
    public partial bool IsDigitAtMode { get; set; }

    [ObservableProperty]
    public partial bool IsRangeMode { get; set; }

    [ObservableProperty]
    public partial bool IsSearchMode { get; set; }

    /// <summary><see langword="true"/> when the formatting toggles apply (digit-run modes).</summary>
    [ObservableProperty]
    public partial bool ShowFormattingOptions { get; set; } = true;

    [ObservableProperty]
    public partial bool HasContextItems { get; set; }

    /// <summary>"Position — …before [match] after…" previews (digit-at and search modes).</summary>
    public ObservableCollection<string> ContextItems { get; } = [];

    partial void OnModeIndexChanged(int value)
    {
        IsFirstMode = value == 0;
        IsDigitAtMode = value == 1;
        IsRangeMode = value == 2;
        IsSearchMode = value == 3;
        ShowFormattingOptions = value is 0 or 2;
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    partial void OnGroupDigitsChanged(bool value) => _ = ReformatIfShownAsync();

    partial void OnShowLineNumbersChanged(bool value) => _ = ReformatIfShownAsync();

    private bool CanExecute => !IsBusy;

    partial void OnIsBusyChanged(bool value) => ExecuteCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync()
    {
        ClearResult();
        IsBusy = true;
        try
        {
            var decimals = await _provider.GetDecimalsAsync();
            switch (ModeIndex)
            {
                case 0:
                    await ShowFirstAsync(decimals);
                    break;
                case 1:
                    ShowDigitAt(decimals);
                    break;
                case 2:
                    await ShowRangeAsync(decimals);
                    break;
                case 3:
                    await SearchAsync(decimals);
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowFirstAsync(string decimals)
    {
        if (!TryParsePosition(CountText, out var count))
        {
            ShowError(_localizer["PiErrorCount"].Value);
            return;
        }

        var digits = PiDigitsEngine.First(decimals, count);
        OutputText = await FormatOffThreadAsync(digits, 1);
        HasOutput = true;
    }

    private void ShowDigitAt(string decimals)
    {
        if (!TryParsePosition(PositionText, out var position))
        {
            ShowError(_localizer["PiErrorPosition"].Value);
            return;
        }

        OutputText = PiDigitsEngine.DigitAt(decimals, position).ToString();
        HasOutput = true;
        ResultSummary = _localizer["PiDigitAtSummary", position.ToString("N0", CultureInfo.CurrentCulture)].Value;
        HasSummary = true;

        AddContextItem(decimals, position, 1);
        HasContextItems = ContextItems.Count > 0;
    }

    private async Task ShowRangeAsync(string decimals)
    {
        if (!TryParsePosition(FromText, out var from)
            || !TryParsePosition(ToText, out var to)
            || to < from)
        {
            ShowError(_localizer["PiErrorRange"].Value);
            return;
        }

        var digits = PiDigitsEngine.Range(decimals, from, to);
        OutputText = await FormatOffThreadAsync(digits, from);
        HasOutput = true;
    }

    private async Task SearchAsync(string decimals)
    {
        var pattern = SearchText.Trim();
        if (pattern.Length is 0 or > MaxPatternLength || !pattern.All(char.IsAsciiDigit))
        {
            ShowError(_localizer["PiErrorPattern"].Value);
            return;
        }

        var result = await Task.Run(() => PiDigitsEngine.Find(decimals, pattern, MaxListedPositions));
        if (result.TotalCount == 0)
        {
            ShowError(_localizer["PiNoOccurrences"].Value);
            return;
        }

        var totalText = result.TotalCount.ToString("N0", CultureInfo.CurrentCulture);
        ResultSummary = result.IsTruncated
            ? _localizer["PiFoundTruncated", totalText, result.Positions.Count.ToString("N0", CultureInfo.CurrentCulture)].Value
            : _localizer["PiFoundSummary", totalText].Value;
        HasSummary = true;

        OutputText = string.Join(", ", result.Positions);
        HasOutput = true;

        foreach (var position in result.Positions.Take(MaxContextItems))
        {
            AddContextItem(decimals, position, pattern.Length);
        }

        HasContextItems = ContextItems.Count > 0;
    }

    private void AddContextItem(string decimals, int position, int length)
    {
        var context = PiDigitsEngine.Context(decimals, position, length, ContextRadius);
        var before = context.HasMoreBefore ? $"…{context.Before}" : context.Before;
        var after = context.HasMoreAfter ? $"{context.After}…" : context.After;
        ContextItems.Add($"{position.ToString("N0", CultureInfo.CurrentCulture)}:  {before} [{context.Match}] {after}");
    }

    private Task<string> FormatOffThreadAsync(string digits, int startPosition)
    {
        var group = GroupDigits;
        var numbers = ShowLineNumbers;
        return Task.Run(() => PiDigitsFormatter.Format(digits, startPosition, group, numbers));
    }

    /// <summary>Re-renders a digit-run result live when a formatting toggle flips.</summary>
    private async Task ReformatIfShownAsync()
    {
        if (!HasOutput || ModeIndex is not (0 or 2))
        {
            return;
        }

        if (!IsBusy && ExecuteCommand.CanExecute(null))
        {
            await ExecuteAsync();
        }
    }

    private static bool TryParsePosition(string text, out int value)
        => int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value >= 1
            && value <= PiDigitsProvider.DecimalCount;

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    private void ClearResult()
    {
        OutputText = string.Empty;
        HasOutput = false;
        ResultSummary = string.Empty;
        HasSummary = false;
        ErrorMessage = string.Empty;
        HasError = false;
        ContextItems.Clear();
        HasContextItems = false;
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
}
