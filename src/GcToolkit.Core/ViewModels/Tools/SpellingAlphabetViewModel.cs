using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One selectable spelling-alphabet variant with its localized display name.</summary>
public sealed record SpellingAlphabetVariantOption(SpellingAlphabetVariant Variant, string DisplayName);

/// <summary>
/// Bidirectional spelling-alphabet converter (issue #281, cachesleuth.com parity). Encodes text into
/// spoken code words and decodes them back across eleven variants (NATO/ICAO default, ITU 1932, Western
/// Union, Able Baker, RAF 1924, APCO/Police, Dutch, German, Swedish, and the two Russian alphabets),
/// with a live code-word chart that doubles as a tap keyboard plus copy/share. Goes beyond parity by
/// auto-detecting the direction and offering a multi-variant brute force when the variant is unknown.
/// All conversion logic lives in the pure <see cref="SpellingAlphabetCodec"/> (thin-VM convention).
/// </summary>
[Tool("SpellingAlphabet", ToolCategory.Alphabets,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords =
      [
          "spelling", "alphabet", "phonetic", "nato", "icao", "alfa", "bravo", "charlie", "radio",
          "callsign", "hláskovací", "abeceda", "fonetická", "telefonní", "alfa bravo",
      ])]
public sealed partial class SpellingAlphabetViewModel : ToolViewModelBase
{
    private readonly SpellingAlphabetCodec _codec = new();
    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public SpellingAlphabetViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("SpellingAlphabet", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;

        Variants =
        [
            .. SpellingAlphabets.All.Select(v =>
                new SpellingAlphabetVariantOption(v, _localizer[$"SpellingAlphabetVariant_{v.Id}"].Value)),
        ];
        SelectedVariant = Variants[0];
    }

    public IReadOnlyList<SpellingAlphabetVariantOption> Variants { get; }

    [ObservableProperty]
    public partial SpellingAlphabetVariantOption SelectedVariant { get; set; } = null!;

    /// <summary>0 = Encode (text → code words), 1 = Decode (code words → text).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>When on, the direction is inferred from the input each time it changes (beyond parity).</summary>
    [ObservableProperty]
    public partial bool AutoDetectDirection { get; set; } = true;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when decoding hit code words with no match.</summary>
    [ObservableProperty]
    public partial bool HasUnknown { get; set; }

    [ObservableProperty]
    public partial string UnknownNotice { get; set; } = string.Empty;

    /// <summary>The code-word chart for the selected variant (also a tappable keyboard for encoding).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SpellingAlphabetChartItem> Chart { get; set; } = [];

    /// <summary>Per-variant best-guess decodings of the current input (beyond-parity brute force).</summary>
    public ObservableCollection<SpellingAlphabetGuess> VariantGuesses { get; } = [];

    [ObservableProperty]
    public partial bool ShowAllVariants { get; set; }

    private bool IsDecode => DirectionIndex == 1;

    private bool HasInput => !string.IsNullOrEmpty(InputText);

    partial void OnSelectedVariantChanged(SpellingAlphabetVariantOption value)
    {
        RebuildChart();
        Convert();
    }

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Convert();
        }
    }

    partial void OnAutoDetectDirectionChanged(bool value) => Convert();

    partial void OnInputTextChanged(string value)
    {
        Convert();
        // Copy/Share re-evaluate via OnHasOutputChanged; only Clear keys off the input directly.
        ClearCommand.NotifyCanExecuteChanged();
    }

    partial void OnShowAllVariantsChanged(bool value) => Convert();

    private void RebuildChart()
        => Chart = [.. SelectedVariant.Variant.Chart.Select(e => new SpellingAlphabetChartItem(e.Letter, e.Word, AppendLetter))];

    private void AppendLetter(char letter)
    {
        // Tapping the chart is an encoding aid; switch out of decode so the appended letter converts.
        if (IsDecode && !AutoDetectDirection)
        {
            DirectionIndex = 0;
        }

        InputText += letter;
    }

    private void Convert()
    {
        var variant = SelectedVariant.Variant;

        VariantGuesses.Clear();

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasUnknown = false;
            UnknownNotice = string.Empty;
            return;
        }

        // Auto-detect flips the direction when the input already reads as code words.
        if (AutoDetectDirection)
        {
            DirectionIndex = _codec.LooksLikeCodeWords(InputText, variant) ? 1 : 0;
        }

        if (IsDecode)
        {
            var result = _codec.Decode(InputText, variant);
            OutputText = result.Text;
            HasUnknown = result.HasUnknown;
            UnknownNotice = result.HasUnknown
                ? string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localizer["SpellingAlphabetUnknownNotice"].Value,
                    string.Join(", ", result.UnknownWords))
                : string.Empty;

            if (ShowAllVariants)
            {
                BuildVariantGuesses(decode: true);
            }
        }
        else
        {
            OutputText = _codec.Encode(InputText, variant);
            HasUnknown = false;
            UnknownNotice = string.Empty;

            if (ShowAllVariants)
            {
                BuildVariantGuesses(decode: false);
            }
        }

        HasOutput = !string.IsNullOrEmpty(OutputText);
    }

    private void BuildVariantGuesses(bool decode)
    {
        foreach (var option in Variants)
        {
            var text = decode
                ? _codec.Decode(InputText, option.Variant).Text
                : _codec.Encode(InputText, option.Variant);

            if (!string.IsNullOrEmpty(text))
            {
                VariantGuesses.Add(new(option.DisplayName, text, _clipboard.SetText));
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasInput))]
    private void Clear() => InputText = string.Empty;

    [RelayCommand]
    private void Swap()
    {
        // Feed the current output back as input and flip direction (turn off auto so it sticks).
        AutoDetectDirection = false;
        var output = OutputText;
        DirectionIndex = IsDecode ? 0 : 1;
        InputText = output;
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

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }
}
