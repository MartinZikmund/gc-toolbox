using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.UI.Dispatching;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Phone keypad / vanity code (issue #33). Two complementary halves over the standard ITU keypad:
/// an <b>interactive keypad</b> you type on like an old phone — tapping a key cycles its letters
/// (multitap), pausing locks the letter in — and a <b>batch converter</b> between text and keypad
/// digits in both the lossy <see cref="PhoneKeypadMode.Vanity"/> and reversible
/// <see cref="PhoneKeypadMode.Multitap"/> conventions. The pure transform/state lives in
/// <see cref="PhoneKeypadCodec"/> and <see cref="PhoneKeypadComposer"/>; this VM only adapts them to
/// bindings (the multitap timeout itself is driven by the View's timer, the one UI-timing concern).
/// Decoding a vanity code resolves each digit run against the bundled offline word list
/// (<see cref="PhoneKeypadDictionary"/>) — <c>34448</c> → DIGIT / EIGHT / FIGHT — falling back to the
/// per-key letters when nothing matches. Beyond geocachingtoolbox.com parity it adds the interactive
/// keypad and the reversible multitap mode.
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
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    private int _decodeGeneration;
    private bool _suppressRecompute;

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

    /// <summary>Which digit stands for a space: 0 → <c>'0'</c>, 1 → <c>'1'</c> (the site's own option).</summary>
    [ObservableProperty]
    public partial int SpaceDigitIndex { get; set; }

    /// <summary><see langword="true"/> when decoding a vanity code, where one digit run can spell several
    /// words — surfaces the candidate list and the explanatory note.</summary>
    [ObservableProperty]
    public partial bool IsVanityDecode { get; set; }

    /// <summary>One row per digit run of the vanity code being decoded, with the words it can spell.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<VanityCandidateItem> Candidates { get; set; } = [];

    /// <summary>The decoder is resolving the digit runs against the word list on a background thread.</summary>
    [ObservableProperty]
    public partial bool IsSolving { get; set; }

    /// <summary>The word list the vanity decoder resolves against. Defaults to the bundled English list;
    /// swappable so unit tests inject a tiny dictionary instead of loading 300k words.</summary>
    public Func<PhoneKeypadDictionary> DictionarySource { get; set; } = static () => PhoneKeypadDictionary.English;

    private PhoneKeypadMode Mode => ModeIndex == 1 ? PhoneKeypadMode.Multitap : PhoneKeypadMode.Vanity;

    private bool IsDecode => DirectionIndex == 1;

    private char SpaceDigit => SpaceDigitIndex == 1 ? '1' : PhoneKeypadCodec.DefaultSpaceDigit;

    partial void OnModeIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            _debouncer.RunNow(RecomputeConverter);
        }
    }

    /// <summary>Flipping the direction carries the previous result into the input, so encode → decode is a
    /// one-tap round trip.</summary>
    partial void OnDirectionIndexChanged(int value)
    {
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        _suppressRecompute = true;
        ConverterInput = ConverterOutput;
        _suppressRecompute = false;

        _debouncer.RunNow(RecomputeConverter);
    }

    partial void OnSpaceDigitIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            _debouncer.RunNow(RecomputeConverter);
        }
    }

    // Vanity decoding hits a 300k-word list on every keystroke, so debounce that path; the cheap
    // encode/multitap paths stay instant.
    partial void OnConverterInputChanged(string value)
    {
        if (_suppressRecompute)
        {
            return;
        }

        if (Mode == PhoneKeypadMode.Vanity && IsDecode)
        {
            _debouncer.Debounce(RecomputeConverter);
        }
        else
        {
            _debouncer.RunNow(RecomputeConverter);
        }
    }

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
            _decodeGeneration++; // discard any in-flight decode
            Candidates = [];
            IsSolving = false;
            ConverterOutput = string.Empty;
            HasConverterOutput = false;
            return;
        }

        if (IsVanityDecode)
        {
            StartVanityDecode(ConverterInput);
            return;
        }

        _decodeGeneration++;
        Candidates = [];
        IsSolving = false;

        ConverterOutput = IsDecode
            ? _codec.DecodeMultitap(ConverterInput)
            : _codec.Encode(ConverterInput, Mode, SpaceDigit);

        HasConverterOutput = ConverterOutput.Length > 0;
    }

    /// <summary>
    /// Resolves every digit run against the word list on a background thread — the first call also
    /// decompresses and indexes the embedded list — then publishes on the UI thread. A generation guard
    /// drops results a newer keystroke has superseded. Runs synchronously when there is no dispatcher
    /// (unit tests).
    /// </summary>
    private void StartVanityDecode(string input)
    {
        var generation = ++_decodeGeneration;

        if (_dispatcher is null)
        {
            PublishVanityDecode(BuildVanityCandidates(input));
            return;
        }

        IsSolving = true;
        _ = Task.Run(() =>
        {
            var candidates = BuildVanityCandidates(input);
            _dispatcher.TryEnqueue(() =>
            {
                if (generation != _decodeGeneration)
                {
                    return; // a newer input already superseded this result
                }

                PublishVanityDecode(candidates);
            });
        });
    }

    private void PublishVanityDecode(IReadOnlyList<VanityCandidateItem> candidates)
    {
        Candidates = candidates;
        IsSolving = false;
        ConverterOutput = BuildBestGuess(candidates);
        HasConverterOutput = candidates.Count > 0;
    }

    private IReadOnlyList<VanityCandidateItem> BuildVanityCandidates(string digits)
    {
        var dictionary = DictionarySource();

        return
        [
            .. PhoneKeypadCodec.SplitVanityTokens(digits)
                .Select(token => new VanityCandidateItem(
                    token,
                    dictionary.WordsFor(token),
                    LetterOptionsFor(token),
                    _clipboard.SetText))
        ];
    }

    /// <summary>The per-key letters of each digit (<c>34448 → DEF GHI GHI GHI TUV</c>) — the hand-solving
    /// fallback shown when no dictionary word matches the run.</summary>
    private string LetterOptionsFor(string token)
        => string.Join(' ', _codec.DecodeVanityCandidates(token).Select(c => c.Letters));

    /// <summary>The most likely reading: the commonest word for each run, unresolved runs left as digits.
    /// This is what Copy/Share emit and what the direction toggle swaps back into the input.</summary>
    private static string BuildBestGuess(IReadOnlyList<VanityCandidateItem> candidates)
        => string.Join(' ', candidates.Select(c => c.HasWords ? c.Words[0] : c.Code));

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
