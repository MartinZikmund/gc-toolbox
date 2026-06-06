using System.Collections.ObjectModel;
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
/// Number base converter (issue #14). Parses an integer written in a chosen source base (2–36, digits
/// 0–9 then A–Z, case-insensitive) and shows it live in binary, octal, decimal and hexadecimal at once,
/// plus an arbitrary target base (2–36). Matches the geocachingtoolbox.com base-conversion tool and
/// goes beyond it: arbitrary-precision <see cref="BigInteger"/> values (no overflow), every common base
/// shown simultaneously, a leading "-" for negatives, a clear invalid-digit error state, and
/// copy/share/clear. All parsing/formatting lives in the pure <see cref="NumberBaseConverter"/>.
/// </summary>
[Tool("BaseConverter", ToolCategory.Numbers,
      Introduced = "2026-02-10", Updated = "2026-06-06",
      Keywords = ["binary", "hex", "hexadecimal", "octal", "decimal", "base", "radix", "number", "soustava", "převod", "číslo", "dvojková", "šestnáctková"])]
public sealed partial class BaseConverterViewModel : ToolViewModelBase
{
    private readonly NumberBaseConverter _converter = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public BaseConverterViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("BaseConverter", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>The value to convert, written in <see cref="FromBase"/>.</summary>
    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The radix the input is written in (2–36).</summary>
    [ObservableProperty]
    public partial int FromBase { get; set; } = 10;

    /// <summary>The arbitrary "convert to" radix (2–36) shown alongside the four common bases.</summary>
    [ObservableProperty]
    public partial int ToBase { get; set; } = 16;

    /// <summary>The value rendered in <see cref="ToBase"/>.</summary>
    [ObservableProperty]
    public partial string TargetOutput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when the input has content that is not a valid number in <see cref="FromBase"/>.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>The four conventional bases (binary, octal, decimal, hex), each individually copyable.</summary>
    public ObservableCollection<BaseResultItem> CommonResults { get; } = [];

    public int MinBase => NumberBaseConverter.MinBase;

    public int MaxBase => NumberBaseConverter.MaxBase;

    partial void OnInputTextChanged(string value) => Convert();

    partial void OnFromBaseChanged(int value) => Convert();

    partial void OnToBaseChanged(int value) => Convert();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        CommonResults.Clear();

        if (string.IsNullOrWhiteSpace(InputText))
        {
            ResetOutputs();
            HasError = false;
            ErrorMessage = string.Empty;
            return;
        }

        if (!_converter.TryParse(InputText, FromBase, out var value))
        {
            ResetOutputs();
            HasError = true;
            ErrorMessage = string.Format(
                CultureInfo.CurrentCulture,
                _localizer["BaseConverterInvalidNotice"].Value,
                FromBase);
            return;
        }

        HasError = false;
        ErrorMessage = string.Empty;

        var common = _converter.ToCommonBases(value);
        CommonResults.Add(new BaseResultItem(_localizer["BaseConverterBinary"].Value, common.Binary, _clipboard.SetText));
        CommonResults.Add(new BaseResultItem(_localizer["BaseConverterOctal"].Value, common.Octal, _clipboard.SetText));
        CommonResults.Add(new BaseResultItem(_localizer["BaseConverterDecimal"].Value, common.Decimal, _clipboard.SetText));
        CommonResults.Add(new BaseResultItem(_localizer["BaseConverterHex"].Value, common.Hexadecimal, _clipboard.SetText));

        TargetOutput = _converter.Format(value, ToBase);
        HasOutput = true;
    }

    private void ResetOutputs()
    {
        TargetOutput = string.Empty;
        HasOutput = false;
    }

    /// <summary>The Copy/Share payload: every common base plus the arbitrary target, one labelled line each.</summary>
    private string BuildResultText()
    {
        var lines = CommonResults.Select(r => $"{r.Label}: {r.Value}").ToList();
        lines.Add(string.Format(CultureInfo.CurrentCulture, _localizer["BaseConverterTargetLine"].Value, ToBase, TargetOutput));
        return string.Join(Environment.NewLine, lines);
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(BuildResultText());

    [RelayCommand(CanExecute = nameof(HasOutput))]
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

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
