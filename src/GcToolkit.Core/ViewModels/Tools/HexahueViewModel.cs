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

/// <summary>
/// Bidirectional Hexahue converter (issue #51). Translates text to and from the colour-block alphabet
/// live as the user types: every supported character renders as a 2×3 grid of six coloured squares
/// (<see cref="HexahueGlyph"/>), and the plain-text result is shown alongside. A one-tap direction swap
/// carries the previous result into the input; the clickable reference chart "types" a character on tap.
/// Accented Latin letters fold to their base (Č → C) and unsupported characters are flagged with a count
/// rather than dropped. All transform logic lives in the pure <see cref="HexahueCodec"/> (thin-VM convention).
/// </summary>
[Tool("Hexahue", ToolCategory.Alphabets,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["hexahue", "colour", "color", "squares", "barvy", "barevná abeceda"])]
public sealed partial class HexahueViewModel : ToolViewModelBase
{
    private readonly HexahueCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressConvert;

    public HexahueViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Hexahue", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        var alphabet = HexahueCodec.GetAlphabet();
        LetterChart = [.. alphabet.Where(e => e.Group == HexahueGroup.Letters)];
        DigitChart = [.. alphabet.Where(e => e.Group == HexahueGroup.Digits)];
        PunctuationChart = [.. alphabet.Where(e => e.Group is HexahueGroup.Punctuation or HexahueGroup.Space)];
    }

    /// <summary>0 = text → Hexahue, 1 = Hexahue → text. Both render glyphs; the label flips the framing
    /// and the swap carries the result across.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The plain-text result (normalized, upper-cased).</summary>
    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary>The encoded glyph sequence, each carrying its decoded character for the screen-reader label.</summary>
    public ObservableCollection<HexahueGlyphItem> Glyphs { get; } = [];

    /// <summary>The clickable reference chart, split into sections.</summary>
    public IReadOnlyList<HexahuePaletteEntry> LetterChart { get; }

    public IReadOnlyList<HexahuePaletteEntry> DigitChart { get; }

    public IReadOnlyList<HexahuePaletteEntry> PunctuationChart { get; }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore re-entrant carries and the transient -1 a RadioButtons control can emit.
        if (_suppressConvert || value is not (0 or 1))
        {
            return;
        }

        // Switching direction carries the previous result into the input, so a round-trip is one tap.
        _suppressConvert = true;
        InputText = OutputText;
        _suppressConvert = false;
        Convert();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        var result = _codec.Encode(InputText);

        Glyphs.Clear();
        foreach (var glyph in result.Glyphs)
        {
            // Each glyph round-trips to exactly one character — the screen-reader label.
            var character = _codec.Decode([glyph])[0];
            Glyphs.Add(new HexahueGlyphItem(glyph, character));
        }

        OutputText = _codec.Decode(result.Glyphs);
        HasOutput = Glyphs.Count > 0;

        if (result.HasUnsupported)
        {
            HasWarning = true;
            var skipped = string.Join(" ", result.UnsupportedCharacters);
            WarningMessage = string.Format(
                _localizer["HexahueSkippedNotice"].Value, result.UnsupportedCount, skipped);
        }
        else
        {
            HasWarning = false;
            WarningMessage = string.Empty;
        }
    }

    /// <summary>"Types" a chart character into the input.</summary>
    [RelayCommand]
    private void Insert(HexahuePaletteEntry? entry)
    {
        if (entry is not null)
        {
            InputText += entry.Character;
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
    private void Clear() => InputText = string.Empty;
}
