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
/// Calculator-spelling (BEGHILOS) tool (issue #128). Converts words into the number to punch into a
/// calculator and numbers into the word they spell upside-down, live and in either direction. Beyond
/// the cachesleuth.com reference it offers auto-direction detection, a strict/extended mapping profile,
/// unsupported-character flagging (counted, never silently dropped), a seven-segment glyph preview with
/// a 180°-flipped reading, and copy/share — all fully offline via the pure <see cref="BeghilosCodec"/>.
/// </summary>
[Tool("Beghilos", ToolCategory.Alphabets,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["beghilos", "calculator", "spelling", "upside down", "seven segment", "kalkulačka", "číslo"])]
public sealed partial class BeghilosViewModel : ToolViewModelBase
{
    private readonly BeghilosCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public BeghilosViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Beghilos", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = auto-detect, 1 = word → number (encode), 2 = number → word (decode).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>0 = strict BEGHILOS, 1 = extended (adds 9 → G on decode).</summary>
    [ObservableProperty]
    public partial int ProfileIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The direction actually used (after auto-detection), as a localized caption for the UI.</summary>
    [ObservableProperty]
    public partial string ResolvedDirectionLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary>The seven-segment glyphs for the result, drawn right-side-up (the calculator's actual display).</summary>
    public ObservableCollection<SevenSegmentGlyph> Glyphs { get; } = [];

    /// <summary>The same glyphs read upside-down (180° rotated) — the spelled word as a solver sees it.</summary>
    public ObservableCollection<SevenSegmentGlyph> FlippedGlyphs { get; } = [];

    [ObservableProperty]
    public partial bool HasGlyphs { get; set; }

    /// <summary>The letter↔digit reference pairs (e.g. <c>"B = 8"</c>) for an inline legend.</summary>
    public IReadOnlyList<string> MappingReference { get; } =
        [.. BeghilosCodec.Mapping.Select(pair => $"{pair.Letter} = {pair.Digit}")];

    private BeghilosProfile Profile => ProfileIndex == 1 ? BeghilosProfile.Extended : BeghilosProfile.Strict;

    private BeghilosDirection? RequestedDirection => DirectionIndex switch
    {
        1 => BeghilosDirection.Encode,
        2 => BeghilosDirection.Decode,
        _ => null, // auto
    };

    partial void OnInputTextChanged(string value) => Convert();

    partial void OnDirectionIndexChanged(int value) => Convert();

    partial void OnProfileIndexChanged(int value) => Convert();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        Glyphs.Clear();
        FlippedGlyphs.Clear();

        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasGlyphs = false;
            HasWarning = false;
            WarningMessage = string.Empty;
            ResolvedDirectionLabel = string.Empty;
            return;
        }

        var direction = RequestedDirection ?? BeghilosCodec.DetectDirection(InputText);
        var result = _codec.Convert(InputText, direction, Profile);

        OutputText = result.Result;
        HasOutput = result.Result.Length > 0;
        ResolvedDirectionLabel = _localizer[direction == BeghilosDirection.Encode ? "BeghilosResolvedEncode" : "BeghilosResolvedDecode"].Value;

        // Glyphs make sense for the digit side: the encoded number, or the input number being decoded.
        var glyphSource = direction == BeghilosDirection.Encode ? result.Result : InputText;
        foreach (var glyph in SevenSegmentGlyph.ForText(glyphSource))
        {
            Glyphs.Add(glyph);
            FlippedGlyphs.Add(glyph);
        }

        HasGlyphs = Glyphs.Count > 0;

        if (result.HasUnsupported)
        {
            HasWarning = true;
            var chars = string.Join(" ", result.UnsupportedCharacters);
            WarningMessage = _localizer["BeghilosUnsupportedNotice"].Value is { Length: > 0 } template
                ? string.Format(System.Globalization.CultureInfo.CurrentCulture, template, result.UnsupportedCount, chars)
                : chars;
        }
        else
        {
            HasWarning = false;
            WarningMessage = string.Empty;
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
