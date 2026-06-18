using System.Collections.ObjectModel;
using System.Linq;
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
/// Bidirectional electronic segment-display tool (issue #58). Beyond the geocachingtoolbox.com reference —
/// which only renders a segment code as a display — this closes the loop both ways: type text to get the
/// per-character lit-segment codes and a live glyph preview, or paste segment codes (label list, binary
/// mask or decimal, auto-detected) to read them back as text. It spans all four display families
/// (7/9/14/16-segment) and adds a bit-order toggle, common-anode/cathode inversion, a closest-match
/// decoder for arbitrary masks, a clickable reference chart, and copy/share. All logic lives in the pure
/// <see cref="SegmentDisplayCodec"/> (thin-VM convention).
/// </summary>
[Tool("SegmentDisplay", ToolCategory.Alphabets,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["segment", "7-segment", "seven segment", "14-segment", "16-segment", "display", "lcd", "displej"])]
public sealed partial class SegmentDisplayViewModel : ToolViewModelBase
{
    private readonly SegmentDisplayCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressConvert;

    public SegmentDisplayViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("SegmentDisplay", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        RebuildChart();
    }

    /// <summary>The display families shown in the picker, in <see cref="SegmentDisplayType"/> order.</summary>
    public IReadOnlyList<string> DisplayTypeOptions { get; } =
    [
        "7-segment", "9-segment", "14-segment", "16-segment",
    ];

    /// <summary>The notation choices: 0 = auto-detect, then labels / binary / decimal.</summary>
    public IReadOnlyList<string> NotationOptions { get; private set; } = [];

    /// <summary>0 = text → display, 1 = display code → text.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Index into <see cref="DisplayTypeOptions"/> / <see cref="SegmentDisplayType"/>.</summary>
    [ObservableProperty]
    public partial int DisplayTypeIndex { get; set; }

    /// <summary>0 = auto-detect, 1 = labels, 2 = binary, 3 = decimal (decode side only).</summary>
    [ObservableProperty]
    public partial int NotationIndex { get; set; }

    /// <summary>Common-cathode ("lit = 0") inversion of every mask.</summary>
    [ObservableProperty]
    public partial bool Invert { get; set; }

    /// <summary>Read the mask bits in reverse order (some hardware wires the segments the other way).</summary>
    [ObservableProperty]
    public partial bool ReverseBits { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasGlyphs { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> only on the decode side, where the notation picker is meaningful.</summary>
    [ObservableProperty]
    public partial bool IsDecoding { get; set; }

    /// <summary>The per-character glyphs to render (the result when encoding, the input when decoding).</summary>
    public ObservableCollection<SegmentGlyph> Glyphs { get; } = [];

    /// <summary>The clickable reference chart for the current display type.</summary>
    public ObservableCollection<SegmentGlyph> Chart { get; } = [];

    private SegmentDisplayType CurrentType => (SegmentDisplayType)DisplayTypeIndex;

    private bool IsTextToDisplay => DirectionIndex != 1;

    /// <summary>The selected notation, or <see langword="null"/> for auto-detect.</summary>
    private SegmentNotation? SelectedNotation => NotationIndex switch
    {
        1 => SegmentNotation.Labels,
        2 => SegmentNotation.Binary,
        3 => SegmentNotation.Decimal,
        _ => null,
    };

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit and re-entrant carries.
        if (_suppressConvert || value is not (0 or 1))
        {
            return;
        }

        IsDecoding = value == 1;

        // Carry the previous result into the input so a round-trip is one tap.
        _suppressConvert = true;
        InputText = OutputText;
        _suppressConvert = false;
        Convert();
    }

    partial void OnDisplayTypeIndexChanged(int value)
    {
        if (value is < 0 or > 3)
        {
            return;
        }

        RebuildChart();
        Convert();
    }

    partial void OnNotationIndexChanged(int value) => Convert();

    partial void OnInvertChanged(bool value) => Convert();

    partial void OnReverseBitsChanged(bool value) => Convert();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = false;
            WarningMessage = string.Empty;
            Glyphs.Clear();
            HasGlyphs = false;
            return;
        }

        if (IsTextToDisplay)
        {
            ConvertTextToDisplay();
        }
        else
        {
            ConvertDisplayToText();
        }

        HasGlyphs = Glyphs.Count > 0;
    }

    private void ConvertTextToDisplay()
    {
        var glyphs = _codec.Encode(InputText, CurrentType, Invert);
        RebuildGlyphs(glyphs);

        // The result text is the chosen notation, one code per character, comma-separated.
        var notation = SelectedNotation ?? SegmentNotation.Labels;
        OutputText = string.Join(", ", glyphs.Select(g => SegmentDisplayCodec.Format(g.Mask, CurrentType, notation)));
        HasOutput = !string.IsNullOrEmpty(OutputText);

        var unmapped = glyphs.Count(g => g.Character == SegmentDisplayCodec.Unknown);
        SetWarning(unmapped > 0, "SegmentUnmappedNotice");
    }

    private void ConvertDisplayToText()
    {
        var result = _codec.Decode(InputText, CurrentType, SelectedNotation, ReverseBits, Invert);
        RebuildGlyphs(result.Glyphs);

        OutputText = result.Text;
        HasOutput = !string.IsNullOrEmpty(OutputText);

        // Flag any code that was unparseable or only a closest (inexact) match.
        var unknown = result.Glyphs.Count(g => g.Character == SegmentDisplayCodec.Unknown);
        var approximate = result.Glyphs.Count(g => g is { IsExact: false, Character: not '?' });
        if (unknown > 0)
        {
            SetWarning(true, "SegmentUnparsedNotice");
        }
        else
        {
            SetWarning(approximate > 0, "SegmentApproximateNotice");
        }
    }

    private void SetWarning(bool show, string key)
    {
        HasWarning = show;
        WarningMessage = show ? _localizer[key].Value : string.Empty;
    }

    private void RebuildGlyphs(IReadOnlyList<SegmentGlyph> glyphs)
    {
        Glyphs.Clear();
        foreach (var glyph in glyphs)
        {
            Glyphs.Add(glyph);
        }
    }

    private void RebuildChart()
    {
        Chart.Clear();
        foreach (var glyph in _codec.GetChart(CurrentType))
        {
            Chart.Add(glyph);
        }

        NotationOptions =
        [
            _localizer["SegmentNotationAuto"].Value,
            _localizer["SegmentNotationLabels"].Value,
            _localizer["SegmentNotationBinary"].Value,
            _localizer["SegmentNotationDecimal"].Value,
        ];
    }

    /// <summary>"Types" a chart glyph into the input: the character itself when encoding text, or the
    /// glyph's code (in the active notation) when decoding.</summary>
    [RelayCommand]
    private void InsertGlyph(SegmentGlyph? glyph)
    {
        if (glyph is null)
        {
            return;
        }

        if (IsTextToDisplay)
        {
            InputText += glyph.Character;
        }
        else
        {
            var notation = SelectedNotation ?? SegmentNotation.Decimal;
            var code = SegmentDisplayCodec.Format(glyph.Mask, CurrentType, notation);
            InputText = InputText.Length == 0 ? code : $"{InputText}, {code}";
        }
    }

    [RelayCommand]
    private void SwapDirection() => DirectionIndex = DirectionIndex == 0 ? 1 : 0;

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
