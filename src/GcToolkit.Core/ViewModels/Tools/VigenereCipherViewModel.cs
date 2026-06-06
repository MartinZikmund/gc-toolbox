using System.Collections.ObjectModel;
using System.Globalization;
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
/// Vigenère cipher (issue #34). Encodes and decodes a keyword Vigenère live as the user types — case
/// preserved, non-letters passed through, the key advancing only on letters — and adds an "assisted
/// decode" mode that recovers an <em>unknown</em> key from a long ciphertext via the Index of Coincidence
/// (key-length estimate) plus chi-squared scoring against English letter frequencies (the
/// geocachingtoolbox.com solver parity feature). Goes beyond parity by also showing the normalized key,
/// the running substitution "key" rows, and the ranked key-length candidates, with copy/share/clear. All
/// cipher logic lives in the pure <see cref="VigenereCipher"/> (thin-VM convention).
/// </summary>
[Tool("VigenereCipher", ToolCategory.Ciphers,
      Introduced = "2026-02-10", Updated = "2026-06-06",
      Keywords = ["vigenère", "vigenere", "key", "keyword", "polyalphabetic", "cipher", "solve", "break",
                  "klíč", "klic", "šifra", "sifra", "heslo"])]
public sealed partial class VigenereCipherViewModel : ToolViewModelBase
{
    private const int MaxKeyLength = 12;

    private readonly VigenereCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public VigenereCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("VigenereCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        Recompute();
    }

    /// <summary>0 = Encode, 1 = Decode (with a known key), 2 = Assisted decode (recover an unknown key).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string KeyText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The key actually used, normalized to A–Z (e.g. <c>"Le-mon" → "LEMON"</c>).</summary>
    [ObservableProperty]
    public partial string NormalizedKey { get; set; } = string.Empty;

    /// <summary><see langword="true"/> in the two keyed modes (Encode/Decode), where the key box and the
    /// running-key display apply.</summary>
    [ObservableProperty]
    public partial bool IsKeyedMode { get; set; } = true;

    /// <summary><see langword="true"/> in assisted-decode mode, where the derived key + candidates show.</summary>
    [ObservableProperty]
    public partial bool IsAssistedMode { get; set; }

    /// <summary><see langword="true"/> when assisted decode has a derived key to display.</summary>
    [ObservableProperty]
    public partial bool HasDerivedKey { get; set; }

    /// <summary>The key the assisted solver recovered from the ciphertext.</summary>
    [ObservableProperty]
    public partial string DerivedKey { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the assisted solver had too little text to find a key.</summary>
    [ObservableProperty]
    public partial bool ShowAssistedHint { get; set; }

    /// <summary><see langword="true"/> when the running-key substitution card has rows to show.</summary>
    [ObservableProperty]
    public partial bool ShowKeyRows { get; set; }

    /// <summary>The running "key" display: one row per key letter, each showing its alphabet mapping.</summary>
    public ObservableCollection<VigenereKeyRow> KeyRows { get; } = [];

    /// <summary>The ranked key-length candidates (length + averaged IC) shown in assisted mode.</summary>
    public ObservableCollection<string> KeyLengthCandidates { get; } = [];

    private bool IsDecode => DirectionIndex == 1;

    private bool IsAssisted => DirectionIndex == 2;

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit while re-templating.
        if (value is 0 or 1 or 2)
        {
            Recompute();
        }
    }

    partial void OnKeyTextChanged(string value) => Recompute();

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        IsAssistedMode = IsAssisted;
        IsKeyedMode = !IsAssisted;

        if (IsAssisted)
        {
            RecomputeAssisted();
        }
        else
        {
            RecomputeKeyed();
        }
    }

    private void RecomputeKeyed()
    {
        // Reset assisted-only state so a leftover derived key never lingers.
        DerivedKey = string.Empty;
        HasDerivedKey = false;
        ShowAssistedHint = false;
        KeyLengthCandidates.Clear();

        NormalizedKey = _cipher.NormalizeKey(KeyText);
        BuildKeyRows(NormalizedKey);

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        OutputText = IsDecode
            ? _cipher.Decode(InputText, KeyText)
            : _cipher.Encode(InputText, KeyText);
        HasOutput = true;
    }

    private void RecomputeAssisted()
    {
        KeyRows.Clear();
        KeyLengthCandidates.Clear();

        if (string.IsNullOrEmpty(InputText))
        {
            DerivedKey = string.Empty;
            NormalizedKey = string.Empty;
            OutputText = string.Empty;
            HasOutput = false;
            HasDerivedKey = false;
            ShowAssistedHint = false;
            ShowKeyRows = false;
            return;
        }

        var result = _cipher.Solve(InputText, MaxKeyLength);
        DerivedKey = result.Key;
        NormalizedKey = result.Key;
        HasDerivedKey = result.Key.Length > 0;

        // Surface the top key-length candidates (length + averaged IC) so the estimate is transparent.
        foreach (var candidate in result.KeyLengthCandidates.Take(5))
        {
            KeyLengthCandidates.Add(string.Format(
                CultureInfo.CurrentCulture,
                "{0} · IC {1:0.0000}",
                candidate.Length,
                candidate.IndexOfCoincidence));
        }

        if (HasDerivedKey)
        {
            BuildKeyRows(result.Key);
            OutputText = result.PlainText;
            HasOutput = true;
            ShowAssistedHint = false;
        }
        else
        {
            OutputText = string.Empty;
            HasOutput = false;
            ShowAssistedHint = true;
            ShowKeyRows = false;
        }
    }

    /// <summary>Builds the running-key display: one row per key letter, each showing how the plain
    /// alphabet maps under that letter's Caesar shift.</summary>
    private void BuildKeyRows(string key)
    {
        KeyRows.Clear();
        foreach (var letter in key)
        {
            KeyRows.Add(new VigenereKeyRow(letter, _cipher.PlainAlphabet, _cipher.CipherAlphabetForKeyLetter(letter)));
        }

        ShowKeyRows = KeyRows.Count > 0;
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
        InputText = string.Empty;
        KeyText = string.Empty;
    }
}
