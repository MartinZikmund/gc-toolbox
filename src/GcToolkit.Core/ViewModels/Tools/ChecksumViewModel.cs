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

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// The geocaching checksum: the cross sum of the digits, the A=1…Z=26 value of the letters, and the
/// iterated digital root of each. Shows the working — "1 + 2 + 3 + 4 + 5 = 15", then "15 = 6" — because
/// a cacher checks the intermediate sum, not just the answer. Accents fold to plain letters by default
/// (Ž → Z) and everything else is skipped; an optional part-by-part list scores each group of a
/// coordinate line on its own. All arithmetic lives in the pure <see cref="ChecksumCalculator"/>.
/// </summary>
[Tool("Checksum", ToolCategory.Numbers,
      Introduced = "2026-02-10", Updated = "2026-09-03",
      Keywords = ["checksum", "cross sum", "digit sum", "digital root", "letter value", "reduce",
                  "kontrolní součet", "ciferný součet", "ciferný kořen", "součet číslic",
                  "hodnota písmen",
                  // Nothing else in the catalogue claims these; without them the search dead-ends.
                  "crc", "hash"])]
public sealed partial class ChecksumViewModel : ToolViewModelBase
{
    /// <summary>A coordinate line: digits, a letter and punctuation, so every section has something to show.</summary>
    private const string ExampleInput = "N 49 12.345 E 016 30.678";

    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(150));
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public ChecksumViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Checksum", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Recompute();
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>Fold accented letters onto their base letter (Ž → Z) instead of skipping them.</summary>
    [ObservableProperty]
    public partial bool FoldDiacritics { get; set; } = true;

    /// <summary>Show the per-character sums, not only the totals.</summary>
    [ObservableProperty]
    public partial bool ShowWorking { get; set; } = true;

    /// <summary>Also score each whitespace/punctuation-delimited group on its own.</summary>
    [ObservableProperty]
    public partial bool ShowParts { get; set; }

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>Digits and letters added together — the headline number.</summary>
    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    /// <summary>The single digit the total reduces to.</summary>
    [ObservableProperty]
    public partial string RootText { get; set; } = string.Empty;

    /// <summary>The total's reduction chain, e.g. <c>42 = 6</c>. Hidden when the total is already one digit.</summary>
    [ObservableProperty]
    public partial string TotalReductionLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowTotalReduction { get; set; }

    [ObservableProperty]
    public partial bool ShowDigits { get; set; }

    [ObservableProperty]
    public partial string DigitWorkingLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DigitReductionLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowLetters { get; set; }

    [ObservableProperty]
    public partial string LetterWorkingLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LetterReductionLine { get; set; } = string.Empty;

    /// <summary>Assigned wholesale so a long paste replaces the list in one notification.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ChecksumPart> Parts { get; set; } = [];

    [ObservableProperty]
    public partial bool ShowPartList { get; set; }

    /// <summary>"Skipped N characters" — reassures the user that the spaces and symbols were meant to be dropped.</summary>
    [ObservableProperty]
    public partial string SkippedNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSkippedNote { get; set; }

    /// <summary>Only raised for input long enough to hit the working/part caps.</summary>
    [ObservableProperty]
    public partial string TruncationNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTruncationNote { get; set; }

    partial void OnFoldDiacriticsChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnShowWorkingChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnShowPartsChanged(bool value) => _debouncer.RunNow(Recompute);

    // The working rebuilds a term per character, so a pasted cache description is expensive —
    // coalesce keystrokes then, but keep ordinary short input instant.
    partial void OnInputTextChanged(string value)
    {
        if (value.Length > 200)
        {
            _debouncer.Debounce(Recompute);
        }
        else
        {
            _debouncer.RunNow(Recompute);
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        var analysis = ChecksumCalculator.Analyze(InputText, FoldDiacritics);

        HasOutput = analysis.HasContent;

        TotalText = Format(analysis.Total);
        RootText = Format(analysis.TotalRoot);
        TotalReductionLine = FormatChain(analysis.TotalReduction);
        ShowTotalReduction = analysis.HasContent && analysis.TotalReduction.Count > 1;

        ShowDigits = analysis.Digits.HasContent;
        DigitWorkingLine = ShowWorking ? FormatWorking(analysis.Digits, withCharacter: false) : string.Empty;
        DigitReductionLine = FormatChain(analysis.Digits.Reduction);

        ShowLetters = analysis.Letters.HasContent;
        LetterWorkingLine = ShowWorking ? FormatWorking(analysis.Letters, withCharacter: true) : string.Empty;
        LetterReductionLine = FormatChain(analysis.Letters.Reduction);

        // One part carries exactly the headline numbers, so listing it would just repeat them.
        ShowPartList = ShowParts && analysis.Parts.Count > 1;
        Parts = ShowPartList ? analysis.Parts : [];

        HasSkippedNote = analysis.HasContent && analysis.IgnoredCount > 0;
        SkippedNote = HasSkippedNote ? Localize("ChecksumSkippedNote", analysis.IgnoredCount) : string.Empty;

        TruncationNote = BuildTruncationNote(analysis);
        HasTruncationNote = TruncationNote.Length > 0;
    }

    private string BuildTruncationNote(ChecksumAnalysis analysis)
    {
        if (ShowWorking && (analysis.Digits.TermsTruncated || analysis.Letters.TermsTruncated))
        {
            return Localize("ChecksumWorkingTruncatedNote", ChecksumCalculator.MaxDetailTerms);
        }

        return ShowPartList && analysis.PartsTruncated
            ? Localize("ChecksumPartsTruncatedNote", ChecksumCalculator.MaxParts)
            : string.Empty;
    }

    /// <summary>"1 + 2 + 3 = 6" for digits, "G(7) + C(3) = 10" for letters.</summary>
    private static string FormatWorking(ChecksumSum sum, bool withCharacter)
    {
        if (sum.Terms.Count == 0)
        {
            return string.Empty;
        }

        var terms = sum.Terms.Select(term => withCharacter
            ? $"{term.Character}({Format(term.Value)})"
            : Format(term.Value));

        var body = string.Join(" + ", terms);
        if (sum.TermsTruncated)
        {
            body += " + …";
        }

        return $"{body} = {Format(sum.Value)}";
    }

    /// <summary>Renders a reduction as <c>"782 = 17 = 8"</c>; a single step shows just itself.</summary>
    private static string FormatChain(IReadOnlyList<long> steps)
        => string.Join(" = ", steps.Select(Format));

    private static string Format(long value) => value.ToString(CultureInfo.CurrentCulture);

    private string Localize(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, args);

    /// <summary>The plain-text summary Copy and Share emit.</summary>
    private string BuildResultText()
    {
        StringBuilder builder = new();
        builder.Append(_localizer["ChecksumTotalLabel"].Value).Append(": ").AppendLine(TotalReductionLine);
        builder.Append(_localizer["ChecksumRootLabel"].Value).Append(": ").AppendLine(RootText);

        if (ShowDigits)
        {
            builder.Append(_localizer["ChecksumDigitsHeader"].Value).Append(": ").AppendLine(DigitReductionLine);
        }

        if (ShowLetters)
        {
            builder.Append(_localizer["ChecksumLettersHeader"].Value).Append(": ").AppendLine(LetterReductionLine);
        }

        if (ShowPartList)
        {
            builder.AppendLine().AppendLine(_localizer["ChecksumPartsHeader"].Value);
            foreach (var part in Parts)
            {
                builder.Append(part.Text).Append(": ").Append(Format(part.Total))
                    .Append(" = ").AppendLine(Format(part.Root));
            }
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

    /// <summary>Drops a coordinate line in so the tool explains itself on first open.</summary>
    [RelayCommand]
    private void UseExample() => InputText = ExampleInput;

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
