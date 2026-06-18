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
/// Frequency analysis (issue #182) — a codebreaking aid that counts letters, digits, symbols, words
/// and characters in a block of puzzle text and surfaces the statistical fingerprints (most-common
/// letter, n-grams, Index of Coincidence, key-length estimate, suggested substitution) that crack
/// classical ciphers. Live-recomputes as the user types. All math lives in the pure
/// <see cref="FrequencyAnalyzer"/> (thin-VM convention); goes well beyond cachesleuth.com's
/// single-character counts.
/// </summary>
[Tool("FrequencyAnalysis", ToolCategory.Text,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["frequency", "analysis", "letter frequency", "ngram", "n-gram", "bigram", "trigram",
                  "index of coincidence", "ioc", "cryptanalysis", "substitution", "codebreaking",
                  "kryptoanalýza", "četnost", "frekvence", "analýza"])]
public sealed partial class FrequencyAnalysisViewModel : ToolViewModelBase
{
    private const string ExampleText =
        "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG. THE FIVE BOXING WIZARDS JUMP QUICKLY.";

    private readonly FrequencyAnalyzer _analyzer = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private FrequencyResult _result;

    public FrequencyAnalysisViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("FrequencyAnalysis", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        _result = _analyzer.Analyze(string.Empty);
        Recompute();
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary><see langword="true"/> once any countable character has been entered.</summary>
    [ObservableProperty]
    public partial bool HasContent { get; set; }

    // ---- Options ----

    /// <summary>When <see langword="true"/>, upper- and lower-case are counted separately.</summary>
    [ObservableProperty]
    public partial bool CaseSensitive { get; set; }

    /// <summary>Fold accented letters to their base letter before counting.</summary>
    [ObservableProperty]
    public partial bool StripAccents { get; set; }

    /// <summary>0 = most common first, 1 = least common first, 2 = alphabetical.</summary>
    [ObservableProperty]
    public partial int SortIndex { get; set; }

    /// <summary>0 = all, 1 = letters only, 2 = digits only, 3 = symbols only.</summary>
    [ObservableProperty]
    public partial int ScopeIndex { get; set; }

    /// <summary>0 = compare against English, 1 = compare against Czech.</summary>
    [ObservableProperty]
    public partial int ReferenceIndex { get; set; }

    // ---- Totals (bound individually so the View can label each) ----

    [ObservableProperty]
    public partial int CharacterCount { get; set; }

    [ObservableProperty]
    public partial int LetterCount { get; set; }

    [ObservableProperty]
    public partial int DigitCount { get; set; }

    [ObservableProperty]
    public partial int SymbolCount { get; set; }

    [ObservableProperty]
    public partial int SpaceCount { get; set; }

    [ObservableProperty]
    public partial int WordCount { get; set; }

    [ObservableProperty]
    public partial int LineCount { get; set; }

    [ObservableProperty]
    public partial int UniqueCount { get; set; }

    // ---- Cryptanalysis readouts ----

    [ObservableProperty]
    public partial string IndexOfCoincidenceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KeyLengthText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CipherHintText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuggestedMappingText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasMapping { get; set; }

    // ---- Bindable collections ----

    public ObservableCollection<FrequencyBarItem> CharacterBars { get; } = [];

    public ObservableCollection<FrequencyBarItem> Bigrams { get; } = [];

    public ObservableCollection<FrequencyBarItem> Trigrams { get; } = [];

    public ObservableCollection<FrequencyBarItem> TopWords { get; } = [];

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnCaseSensitiveChanged(bool value) => Recompute();

    partial void OnStripAccentsChanged(bool value) => Recompute();

    partial void OnSortIndexChanged(int value)
    {
        if (value is < 0 or > 2)
        {
            return;
        }

        Recompute();
    }

    partial void OnScopeIndexChanged(int value)
    {
        if (value is < 0 or > 3)
        {
            return;
        }

        Recompute();
    }

    partial void OnReferenceIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnHasContentChanged(bool value)
    {
        CopyTableCommand.NotifyCanExecuteChanged();
        CopyCsvCommand.NotifyCanExecuteChanged();
        ShareSummaryCommand.NotifyCanExecuteChanged();
    }

    private FrequencyOptions BuildOptions() => new()
    {
        CaseSensitive = CaseSensitive,
        StripAccents = StripAccents,
        Sort = SortIndex switch
        {
            1 => FrequencySort.LeastCommonFirst,
            2 => FrequencySort.Alphabetical,
            _ => FrequencySort.MostCommonFirst,
        },
        Scope = ScopeIndex switch
        {
            1 => FrequencyScope.LettersOnly,
            2 => FrequencyScope.DigitsOnly,
            3 => FrequencyScope.SymbolsOnly,
            _ => FrequencyScope.All,
        },
    };

    private void Recompute()
    {
        _result = _analyzer.Analyze(InputText, BuildOptions());
        var totals = _result.Totals;

        CharacterCount = totals.Characters;
        LetterCount = totals.Letters;
        DigitCount = totals.Digits;
        SymbolCount = totals.Symbols;
        SpaceCount = totals.Spaces;
        WordCount = totals.Words;
        LineCount = totals.Lines;
        UniqueCount = totals.UniqueCharacters;

        HasContent = totals.Characters > 0;

        RebuildBars(CharacterBars, _result.CharacterFrequencies, withReference: ScopeIndex is 0 or 1);
        RebuildBars(Bigrams, Take(_result.Bigrams, 15));
        RebuildBars(Trigrams, Take(_result.Trigrams, 15));
        RebuildBars(TopWords, Take(_result.Words, 15));

        UpdateCryptanalysis();
    }

    private void UpdateCryptanalysis()
    {
        if (LetterCount == 0)
        {
            IndexOfCoincidenceText = string.Empty;
            KeyLengthText = string.Empty;
            CipherHintText = string.Empty;
            SuggestedMappingText = string.Empty;
            HasMapping = false;
            return;
        }

        var ioc = _result.IndexOfCoincidence;
        IndexOfCoincidenceText = ioc.ToString("0.0000", CultureInfo.CurrentCulture);
        KeyLengthText = _result.EstimatedKeyLength.ToString(CultureInfo.CurrentCulture);

        // IoC near 0.067 ≈ monoalphabetic/plain; near 0.038 ≈ polyalphabetic (Vigenère-like).
        CipherHintText = ioc >= 0.06
            ? _localizer["FrequencyHintMono"].Value
            : ioc <= 0.045
                ? _localizer["FrequencyHintPoly"].Value
                : _localizer["FrequencyHintMixed"].Value;

        var pairs = _result.SuggestedMapping
            .OrderBy(m => m.Cipher)
            .Select(m => $"{m.Cipher}→{m.Plain}");
        SuggestedMappingText = string.Join("  ", pairs);
        HasMapping = _result.SuggestedMapping.Count > 0;
    }

    private void RebuildBars(
        ObservableCollection<FrequencyBarItem> target,
        IReadOnlyList<FrequencyEntry> source,
        bool withReference = false)
    {
        target.Clear();
        if (source.Count == 0)
        {
            return;
        }

        var max = source.Max(e => e.Count);
        var reference = withReference ? CurrentReference : null;

        foreach (var entry in source)
        {
            var fill = max == 0 ? 0 : (double)entry.Count / max;
            double? expected = null;
            if (reference is not null
                && entry.Display.Length == 1
                && char.IsLetter(entry.Display[0])
                && reference.TryGetValue(char.ToUpperInvariant(entry.Display[0]), out var pct))
            {
                expected = pct;
            }

            target.Add(new FrequencyBarItem(entry, fill, expected));
        }
    }

    private IReadOnlyDictionary<char, double> CurrentReference
        => ReferenceIndex == 1 ? FrequencyTables.Czech : FrequencyTables.English;

    private static IReadOnlyList<FrequencyEntry> Take(IReadOnlyList<FrequencyEntry> source, int count)
        => source.Count <= count ? source : [.. source.Take(count)];

    // ---- Commands ----

    [RelayCommand]
    private void LoadExample() => InputText = ExampleText;

    [RelayCommand]
    private void Clear() => InputText = string.Empty;

    [RelayCommand(CanExecute = nameof(HasContent))]
    private void CopyTable() => _clipboard.SetText(BuildSummary());

    [RelayCommand(CanExecute = nameof(HasContent))]
    private void CopyCsv() => _clipboard.SetText(_result.ToCsv());

    [RelayCommand(CanExecute = nameof(HasContent))]
    private async Task ShareSummaryAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildSummary());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    /// <summary>A plain-text, shareable summary of the totals, IoC, key length and frequency table.</summary>
    private string BuildSummary()
    {
        var sb = new StringBuilder();
        sb.Append(_localizer["FrequencyTotalsHeader"].Value).Append('\n');
        sb.Append(_localizer["FrequencyWords"].Value).Append(": ").Append(WordCount).Append('\n');
        sb.Append(_localizer["FrequencyCharacters"].Value).Append(": ").Append(CharacterCount).Append('\n');
        sb.Append(_localizer["FrequencyLetters"].Value).Append(": ").Append(LetterCount).Append('\n');
        sb.Append(_localizer["FrequencyDigits"].Value).Append(": ").Append(DigitCount).Append('\n');
        sb.Append(_localizer["FrequencySymbols"].Value).Append(": ").Append(SymbolCount).Append('\n');

        if (LetterCount > 0)
        {
            sb.Append(_localizer["FrequencyIoC"].Value).Append(": ").Append(IndexOfCoincidenceText).Append('\n');
            sb.Append(_localizer["FrequencyKeyLength"].Value).Append(": ").Append(KeyLengthText).Append('\n');
        }

        sb.Append('\n').Append(_result.ToCsv());
        return sb.ToString();
    }
}
