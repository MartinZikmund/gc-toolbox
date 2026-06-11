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
/// Phone keypad / vanity code (issue #33). Two complementary halves over the standard ITU keypad:
/// an <b>interactive keypad</b> you type on like an old phone — tapping a key cycles its letters
/// (multitap), pausing locks the letter in — and a <b>batch converter</b> between text and keypad
/// digits in both the lossy <see cref="PhoneKeypadMode.Vanity"/> and reversible
/// <see cref="PhoneKeypadMode.Multitap"/> conventions. The pure transform/state lives in
/// <see cref="PhoneKeypadCodec"/> and <see cref="PhoneKeypadComposer"/>; this VM only adapts them to
/// bindings (the multitap timeout itself is driven by the View's timer, the one UI-timing concern).
/// Beyond geocachingtoolbox.com parity it adds the interactive keypad, the reversible multitap mode,
/// and a dictionary-free offline decode.
/// </summary>
[Tool("PhoneKeypad", ToolCategory.Ciphers,
      Introduced = "2026-06-11", Updated = "2026-06-11",
      Keywords =
      [
          "phone", "keypad", "vanity", "vanity code", "multitap", "multi-tap", "t9", "sms",
          "telephone", "dial", "telefon", "klávesnice", "číselník", "mobil",
      ])]
public sealed partial class PhoneKeypadViewModel : ToolViewModelBase
{
    private readonly PhoneKeypadCodec _codec = new();
    private readonly PhoneKeypadComposer _composer = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public PhoneKeypadViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("PhoneKeypad", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
    }

    // ---- Interactive keypad ----

    /// <summary>The composed text including the letter currently cycling on the last key.</summary>
    [ObservableProperty]
    public partial string ComposedText { get; set; } = string.Empty;

    /// <summary>The multitap press sequence for everything composed so far (e.g. <c>44 33 555</c>).</summary>
    [ObservableProperty]
    public partial string PressCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasComposed { get; set; }

    /// <summary>How long (ms) a letter keeps cycling before it auto-commits. The View uses this as its
    /// multitap timer interval, so the slider literally tunes "tap fast vs. slow".</summary>
    [ObservableProperty]
    public partial int MultitapTimeoutMs { get; set; } = 1000;

    /// <summary>Registers a keypad press (called from the View, which also restarts its multitap timer).</summary>
    public void PressKey(char key)
    {
        _composer.Press(key);
        SyncComposer();
    }

    /// <summary>Locks in the pending letter — invoked by the View's multitap timeout.</summary>
    public void CommitPending()
    {
        _composer.CommitPending();
        SyncComposer();
    }

    public void Backspace()
    {
        _composer.Backspace();
        SyncComposer();
    }

    public void ClearComposed()
    {
        _composer.Clear();
        SyncComposer();
    }

    private void SyncComposer()
    {
        ComposedText = _composer.DisplayText;
        PressCode = _composer.PressCode;
        HasComposed = ComposedText.Length > 0;
    }

    partial void OnHasComposedChanged(bool value)
    {
        CopyComposedTextCommand.NotifyCanExecuteChanged();
        CopyComposedCodeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasComposed))]
    private void CopyComposedText() => _clipboard.SetText(ComposedText);

    [RelayCommand(CanExecute = nameof(HasComposed))]
    private void CopyComposedCode() => _clipboard.SetText(PressCode);

    // ---- Batch converter ----

    /// <summary>0 = Vanity (single digit), 1 = Multitap.</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    /// <summary>0 = Encode (text → digits), 1 = Decode (digits → text).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string ConverterInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConverterOutput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasConverterOutput { get; set; }

    /// <summary><see langword="true"/> when decoding a vanity code, where the result is the ambiguous
    /// per-digit candidates rather than a single string — surfaces the explanatory note.</summary>
    [ObservableProperty]
    public partial bool IsVanityDecode { get; set; }

    private PhoneKeypadMode Mode => ModeIndex == 1 ? PhoneKeypadMode.Multitap : PhoneKeypadMode.Vanity;

    private bool IsDecode => DirectionIndex == 1;

    partial void OnModeIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            RecomputeConverter();
        }
    }

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            RecomputeConverter();
        }
    }

    partial void OnConverterInputChanged(string value) => RecomputeConverter();

    partial void OnHasConverterOutputChanged(bool value)
    {
        CopyConverterCommand.NotifyCanExecuteChanged();
        ShareConverterCommand.NotifyCanExecuteChanged();
    }

    private void RecomputeConverter()
    {
        IsVanityDecode = Mode == PhoneKeypadMode.Vanity && IsDecode;

        if (string.IsNullOrEmpty(ConverterInput))
        {
            ConverterOutput = string.Empty;
            HasConverterOutput = false;
            return;
        }

        ConverterOutput = (Mode, IsDecode) switch
        {
            (PhoneKeypadMode.Multitap, true) => _codec.DecodeMultitap(ConverterInput),
            (PhoneKeypadMode.Vanity, true) => FormatVanityCandidates(ConverterInput),
            _ => _codec.Encode(ConverterInput, Mode),
        };

        HasConverterOutput = ConverterOutput.Length > 0;
    }

    /// <summary>Renders the ambiguous vanity decode as one <c>digit → LETTERS</c> line per digit.</summary>
    private string FormatVanityCandidates(string digits)
    {
        var candidates = _codec.DecodeVanityCandidates(digits);
        return string.Join(
            Environment.NewLine,
            candidates.Select(c => $"{c.Digit} → {(c.Letters == " " ? "␣" : c.Letters)}"));
    }

    [RelayCommand(CanExecute = nameof(HasConverterOutput))]
    private void CopyConverter() => _clipboard.SetText(ConverterOutput);

    [RelayCommand(CanExecute = nameof(HasConverterOutput))]
    private async Task ShareConverterAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, ConverterOutput);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void ClearConverter() => ConverterInput = string.Empty;
}
