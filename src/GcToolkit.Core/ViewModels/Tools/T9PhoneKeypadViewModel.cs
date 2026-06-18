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
/// T9 / phone-keypad decoder (issue #288). Unifies the three classic keypad conventions the
/// reference sites split apart — T9 predictive, multi-tap, and key+position — behind one mode
/// selector, and goes beyond cachesleuth.com's decode-only predictive tool by encoding <em>and</em>
/// decoding every mode with a one-tap direction swap (Roman-numerals UX). The live preview, candidate
/// enumeration, and on-screen keypad all run fully offline on <see cref="PhoneKeypadCodec"/>.
/// </summary>
[Tool("T9PhoneKeypad", ToolCategory.Alphabets,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["t9", "phone", "keypad", "multitap", "multi-tap", "predictive", "telefon", "klávesnice"])]
public sealed partial class T9PhoneKeypadViewModel : ToolViewModelBase
{
    /// <summary>Mode index: 0 = T9 predictive, 1 = multi-tap, 2 = key+position.</summary>
    public const int ModeT9 = 0;
    public const int ModeMultitap = 1;
    public const int ModeKeyPosition = 2;

    /// <summary>Direction index: 0 = text → digits (encode), 1 = digits → text (decode).</summary>
    public const int DirectionEncode = 0;
    public const int DirectionDecode = 1;

    /// <summary>Cap on enumerated T9 candidates surfaced per word; keeps a long sequence bounded
    /// while staying generous enough to include real geocaching answers.</summary>
    private const int CandidateLimit = 2000;

    /// <summary>Threshold above which a word's combinations are truncated, so the UI can flag it.</summary>
    private bool _candidatesTruncated;

    private readonly PhoneKeypadCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressConvert;

    public T9PhoneKeypadViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("T9PhoneKeypad", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Convert();
    }

    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> in T9-decode mode, where the candidate list is meaningful.</summary>
    [ObservableProperty]
    public partial bool ShowCandidates { get; set; }

    /// <summary>Per-word enumerated letter combinations for the current T9 decode (capped).</summary>
    public ObservableCollection<T9WordCandidates> Candidates { get; } = [];

    /// <summary>The on-screen keypad (digit + letters), static reference for the keypad control.</summary>
    public IReadOnlyList<PhoneKey> Keypad { get; } = PhoneKeypadCodec.Keypad;

    private bool IsEncode => DirectionIndex != DirectionDecode;

    /// <summary>Localized placeholder/hint reflecting the active mode and direction.</summary>
    [ObservableProperty]
    public partial string InputPlaceholder { get; set; } = string.Empty;

    partial void OnModeIndexChanged(int value)
    {
        UpdatePlaceholder();
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    partial void OnDirectionIndexChanged(int value)
    {
        // RadioButtons can emit a transient -1; ignore anything outside the two real options.
        if (_suppressConvert || value is not (DirectionEncode or DirectionDecode))
        {
            return;
        }

        UpdatePlaceholder();

        // Switching direction carries the previous result into the input (one-tap round-trip).
        _suppressConvert = true;
        InputText = OutputText;
        _suppressConvert = false;
        Convert();
    }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void UpdatePlaceholder()
    {
        var key = (ModeIndex, IsEncode) switch
        {
            (ModeMultitap, true) => "T9PlaceholderMultitapEncode",
            (ModeMultitap, false) => "T9PlaceholderMultitapDecode",
            (ModeKeyPosition, true) => "T9PlaceholderKeyPositionEncode",
            (ModeKeyPosition, false) => "T9PlaceholderKeyPositionDecode",
            (_, true) => "T9PlaceholderT9Encode",
            (_, false) => "T9PlaceholderT9Decode",
        };

        InputPlaceholder = _localizer[key].Value;
    }

    private void Convert()
    {
        Candidates.Clear();
        ShowCandidates = false;
        _candidatesTruncated = false;

        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = false;
            WarningMessage = string.Empty;
            return;
        }

        OutputText = ModeIndex switch
        {
            ModeMultitap => IsEncode ? _codec.EncodeMultitap(InputText) : _codec.DecodeMultitap(InputText),
            ModeKeyPosition => IsEncode ? _codec.EncodeKeyPosition(InputText) : _codec.DecodeKeyPosition(InputText),
            _ => IsEncode ? _codec.EncodeT9(InputText) : BuildT9DecodePreview(),
        };

        HasOutput = OutputText.Length > 0;

        if (!HasOutput)
        {
            HasWarning = true;
            WarningMessage = _localizer["T9NoResultNotice"].Value;
        }
        else if (_candidatesTruncated)
        {
            HasWarning = true;
            WarningMessage = _localizer["T9TruncatedNotice"].Value;
        }
        else
        {
            HasWarning = false;
            WarningMessage = string.Empty;
        }
    }

    /// <summary>
    /// Builds the per-word candidate lists for a T9 decode and returns a compact preview string —
    /// the first combination of each word, space-joined — as the headline result.
    /// </summary>
    private string BuildT9DecodePreview()
    {
        var words = _codec.SplitWords(InputText);
        List<string> firsts = [];

        foreach (var word in words)
        {
            var combos = _codec.EnumerateT9Token(word, CandidateLimit);
            if (combos.Count == 0)
            {
                continue;
            }

            _candidatesTruncated |= combos.Count >= CandidateLimit;
            Candidates.Add(new T9WordCandidates(word, combos));
            firsts.Add(combos[0]);
        }

        ShowCandidates = Candidates.Count > 0;
        return string.Join(' ', firsts);
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

    /// <summary>Appends a tapped key's digit to the input (drives the on-screen keypad).</summary>
    [RelayCommand]
    private void PressKey(string? digit)
    {
        if (!string.IsNullOrEmpty(digit))
        {
            InputText += digit;
        }
    }
}

/// <summary>A T9 input word and every letter combination it could spell.</summary>
public sealed record T9WordCandidates(string Digits, IReadOnlyList<string> Combinations);
