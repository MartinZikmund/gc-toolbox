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
/// Bidirectional Braille converter (issue #36). Translates text to and from Grade-1 (uncontracted)
/// English Braille live as the user types, rendering braille as copy-pasteable Unicode pattern
/// characters (<c>U+2800</c>–<c>U+283F</c>) rather than the images the geocachingtoolbox.com tool uses.
/// Beyond parity it adds the number and capital indicators, literary punctuation, full round-tripping in
/// both directions, an optional dot-number display, and copy/share. All logic lives in the pure
/// <see cref="BrailleCodec"/> (thin-VM convention).
/// </summary>
[Tool("Braille", ToolCategory.Alphabets,
      Introduced = "2026-06-06", Updated = "2026-06-06",
      Keywords = ["braille", "dots", "tactile", "blind", "abeceda", "slepecké", "písmo", "body", "unicode"])]
public sealed partial class BrailleViewModel : ToolViewModelBase
{
    private readonly BrailleCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    private bool _suppressConvert;

    public BrailleViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Braille", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
    }

    /// <summary>0 = text → Braille, 1 = Braille → text.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>When on, also renders the result's dot-numbers (e.g. <c>1-3-4</c>) — a teaching aid.</summary>
    [ObservableProperty]
    public partial bool ShowDots { get; set; }

    [ObservableProperty]
    public partial string DotsText { get; set; } = string.Empty;

    /// <summary>The dot display is only meaningful when encoding text to braille and an output exists.</summary>
    [ObservableProperty]
    public partial bool CanShowDots { get; set; }

    private bool IsTextToBraille => DirectionIndex != 1;

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

    partial void OnShowDotsChanged(bool value) => UpdateDots();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        OutputText = IsTextToBraille ? _codec.Encode(InputText) : _codec.Decode(InputText);
        HasOutput = !string.IsNullOrEmpty(OutputText);
        UpdateDots();
    }

    private void UpdateDots()
    {
        // Dots describe the braille cells of the source text; only sensible in the text→braille direction.
        CanShowDots = IsTextToBraille && HasOutput;
        DotsText = ShowDots && CanShowDots ? _codec.DescribeDots(InputText) : string.Empty;
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
