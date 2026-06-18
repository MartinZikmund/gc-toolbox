using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Book cipher (Ottendorf) decoder/encoder (issue #132). Resolves numeric reference codes against a
/// pasted source "book" — up to three ordered parts (Page/Line/Word/Character) with configurable
/// numbering base, ignore-symbols/spaces, and whole-word / first-letter / Nth-letter extraction —
/// and goes beyond CacheSleuth with a full encode direction (plaintext → references), a one-tap
/// swap, and inline flagging of out-of-range / non-numeric references. All logic lives in the pure
/// <see cref="BookCipherCodec"/>; this VM stays thin and offline.
/// </summary>
[Tool("BookCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["book", "ottendorf", "book cipher", "reference", "page line word", "knižní šifra", "kniha"])]
public sealed partial class BookCipherViewModel : ToolViewModelBase
{
    private readonly BookCipherCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public BookCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("BookCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = decode (references → text), 1 = encode (text → references).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>The source/key text ("book") references are resolved against.</summary>
    [ObservableProperty]
    public partial string BookText { get; set; } = string.Empty;

    /// <summary>In decode mode the reference codes; in encode mode the plaintext to encode.</summary>
    [ObservableProperty]
    public partial string CodesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    // ---- Reference format (three ordered parts) ----

    /// <summary>Part index: 0 None, 1 Page, 2 Line, 3 Word, 4 Character.</summary>
    [ObservableProperty]
    public partial int Part1Index { get; set; } = (int)BookReferencePart.Word;

    [ObservableProperty]
    public partial int Part2Index { get; set; } = (int)BookReferencePart.None;

    [ObservableProperty]
    public partial int Part3Index { get; set; } = (int)BookReferencePart.None;

    /// <summary>0 = 1-based numbering (default), 1 = 0-based.</summary>
    [ObservableProperty]
    public partial int NumberingBaseIndex { get; set; }

    /// <summary>0 = whole word, 1 = first letter, 2 = Nth letter.</summary>
    [ObservableProperty]
    public partial int ExtractionIndex { get; set; }

    /// <summary>The 1-based letter to take in Nth-letter mode.</summary>
    [ObservableProperty]
    public partial int LetterIndex { get; set; } = 1;

    /// <summary>Whether the Nth-letter field is shown (only in Nth-letter extraction).</summary>
    [ObservableProperty]
    public partial bool ShowLetterIndex { get; set; }

    [ObservableProperty]
    public partial string IgnoreSymbols { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IgnoreSpaces { get; set; }

    public bool IsEncoding => DirectionIndex == 1;

    private BookCipherFormat BuildFormat() => new()
    {
        Part1 = (BookReferencePart)Part1Index,
        Part2 = (BookReferencePart)Part2Index,
        Part3 = (BookReferencePart)Part3Index,
        NumberingStart = NumberingBaseIndex == 1 ? 0 : 1,
        Extraction = (BookCipherExtraction)ExtractionIndex,
        LetterIndex = LetterIndex < 1 ? 1 : LetterIndex,
        IgnoreSymbols = IgnoreSymbols,
        IgnoreSpaces = IgnoreSpaces,
    };

    partial void OnBookTextChanged(string value) => Convert();

    partial void OnCodesTextChanged(string value) => Convert();

    partial void OnPart1IndexChanged(int value) => Convert();

    partial void OnPart2IndexChanged(int value) => Convert();

    partial void OnPart3IndexChanged(int value) => Convert();

    partial void OnNumberingBaseIndexChanged(int value) => Convert();

    partial void OnExtractionIndexChanged(int value)
    {
        ShowLetterIndex = value == (int)BookCipherExtraction.NthLetter;
        Convert();
    }

    partial void OnLetterIndexChanged(int value) => Convert();

    partial void OnIgnoreSymbolsChanged(string value) => Convert();

    partial void OnIgnoreSpacesChanged(bool value) => Convert();

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is not (0 or 1))
        {
            return;
        }

        OnPropertyChanged(nameof(IsEncoding));
        Convert();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Re-runs the live transform whenever an input or option changes.</summary>
    private void Convert()
    {
        if (string.IsNullOrWhiteSpace(CodesText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = false;
            WarningMessage = string.Empty;
            return;
        }

        var format = BuildFormat();
        if (format.UsedParts.Count == 0)
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = true;
            WarningMessage = _localizer["BookCipherNoPartsWarning"].Value;
            return;
        }

        var result = IsEncoding
            ? _codec.Encode(CodesText, BookText, format)
            : _codec.Decode(BookText, CodesText, format);

        OutputText = result.Text;
        HasOutput = !string.IsNullOrEmpty(result.Text);

        if (result.HasErrors)
        {
            HasWarning = true;
            var firstError = result.Tokens.First(t => t.IsError);
            var key = IsEncoding ? "BookCipherEncodeErrorFormat" : "BookCipherDecodeErrorFormat";
            WarningMessage = string.Format(
                _localizer[key].Value,
                result.ErrorCount,
                firstError.Reference,
                firstError.Error);
        }
        else
        {
            HasWarning = false;
            WarningMessage = string.Empty;
        }
    }

    /// <summary>Swaps decode↔encode, carrying the current result into the input for a one-tap round-trip.</summary>
    [RelayCommand]
    private void SwapDirection()
    {
        var carried = OutputText;
        DirectionIndex = IsEncoding ? 0 : 1;
        if (!string.IsNullOrEmpty(carried))
        {
            CodesText = carried;
        }
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
        CodesText = string.Empty;
        OutputText = string.Empty;
    }
}
