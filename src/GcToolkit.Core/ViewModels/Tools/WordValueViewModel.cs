using System.Collections.ObjectModel;
using System.Globalization;
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

/// <summary>
/// Word value / digital root (issue #30). Computes the letter-sum word value (A=1 … Z=26) for each word
/// and the grand total live as the user types, then reduces that total to its cross-sum and digital root —
/// the well-known geocaching "turn words into numbers" method (geocachingtoolbox.com parity). Goes beyond
/// parity with a per-word breakdown, the full reduction chain ("show your work"), and an optional pass that
/// also sums and reduces any plain numbers in the input. All arithmetic lives in the pure
/// <see cref="WordValueCalculator"/> (thin-VM convention).
/// </summary>
[Tool("WordValue", ToolCategory.Text,
      Introduced = "2026-06-06", Updated = "2026-06-06",
      Keywords = ["word", "value", "digital", "root", "sum", "letters", "cross sum", "checksum",
                  "hodnota", "slova", "ciferný", "kořen", "součet", "písmena"])]
public sealed partial class WordValueViewModel : ToolViewModelBase
{
    private readonly WordValueCalculator _calculator = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public WordValueViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("WordValue", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Recompute();
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>Whether plain numbers in the input are summed and reduced alongside the letter value.</summary>
    [ObservableProperty]
    public partial bool CountNumbers { get; set; } = true;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The headline letter-sum total of every word.</summary>
    [ObservableProperty]
    public partial long LetterTotal { get; set; }

    /// <summary>Cross-sum (one digit-sum pass) of <see cref="LetterTotal"/>.</summary>
    [ObservableProperty]
    public partial int LetterCrossSum { get; set; }

    /// <summary>Digital root of <see cref="LetterTotal"/> (1–9, or 0 for an empty input).</summary>
    [ObservableProperty]
    public partial int LetterDigitalRoot { get; set; }

    /// <summary>The reduction chain for the letter total rendered as <c>"123 → 6 → 6"</c>.</summary>
    [ObservableProperty]
    public partial string LetterReduction { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when numbers were found and counted (drives the numeric section).</summary>
    [ObservableProperty]
    public partial bool HasNumbers { get; set; }

    [ObservableProperty]
    public partial long NumberTotal { get; set; }

    [ObservableProperty]
    public partial int NumberDigitalRoot { get; set; }

    /// <summary>The reduction chain for the number total, same format as <see cref="LetterReduction"/>.</summary>
    [ObservableProperty]
    public partial string NumberReduction { get; set; } = string.Empty;

    /// <summary>The numbers found in the input, joined for display (e.g. <c>"49 + 13 + 456"</c>).</summary>
    [ObservableProperty]
    public partial string NumbersSummary { get; set; } = string.Empty;

    /// <summary>Per-word rows shown in the breakdown table.</summary>
    public ObservableCollection<WordValueItem> Words { get; } = [];

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnCountNumbersChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        var analysis = _calculator.Analyze(InputText, CountNumbers);

        Words.Clear();
        foreach (var word in analysis.Words)
        {
            Words.Add(new WordValueItem(word.Text, word.Value));
        }

        LetterTotal = analysis.LetterTotal;
        LetterCrossSum = analysis.LetterCrossSum;
        LetterDigitalRoot = analysis.LetterDigitalRoot;
        LetterReduction = FormatReduction(analysis.LetterReductionSteps);

        HasNumbers = analysis.HasNumbers;
        NumberTotal = analysis.NumberTotal;
        NumberDigitalRoot = analysis.NumberDigitalRoot;
        NumberReduction = FormatReduction(analysis.NumberReductionSteps);
        NumbersSummary = string.Join(" + ", analysis.Numbers.Select(n => n.ToString(CultureInfo.CurrentCulture)));

        HasOutput = analysis.HasContent || analysis.HasNumbers;
    }

    /// <summary>Renders a reduction chain as <c>"a → b → c"</c> (a single value shows just itself).</summary>
    private static string FormatReduction(IReadOnlyList<long> steps)
        => string.Join(" → ", steps.Select(s => s.ToString(CultureInfo.CurrentCulture)));

    /// <summary>The plain-text summary the Copy/Share actions emit.</summary>
    private string BuildResultText()
    {
        var builder = new StringBuilder();

        if (Words.Count > 0)
        {
            foreach (var word in Words)
            {
                builder.Append(word.Word)
                    .Append(" = ")
                    .Append(word.Value.ToString(CultureInfo.CurrentCulture))
                    .AppendLine();
            }

            builder.Append(_localizer["WordValueTotalLabel"].Value)
                .Append(": ")
                .Append(LetterTotal.ToString(CultureInfo.CurrentCulture))
                .AppendLine();
            builder.Append(_localizer["WordValueDigitalRootLabel"].Value)
                .Append(": ")
                .Append(LetterDigitalRoot.ToString(CultureInfo.CurrentCulture))
                .Append("  (")
                .Append(LetterReduction)
                .Append(')')
                .AppendLine();
        }

        if (HasNumbers)
        {
            builder.Append(_localizer["WordValueNumbersLabel"].Value)
                .Append(": ")
                .Append(NumbersSummary)
                .Append(" = ")
                .Append(NumberTotal.ToString(CultureInfo.CurrentCulture))
                .Append("  (")
                .Append(_localizer["WordValueDigitalRootLabel"].Value)
                .Append(' ')
                .Append(NumberDigitalRoot.ToString(CultureInfo.CurrentCulture))
                .Append(')')
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
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
