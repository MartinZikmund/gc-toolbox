using System.Collections.ObjectModel;
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
/// Bacon (Baconian biliteral) cipher (issue #6). Encodes/decodes letters as 5-symbol groups live as
/// the user types, in two mappings — the classic 24-letter <see cref="BaconVersion.Standard"/> (I=J,
/// U=V) and the reversible <see cref="BaconVersion.Distinct"/> — with an encode/decode toggle, a swap
/// A/B toggle, an optional custom symbol pair, a one-tap direction swap, copy/share, and an on-page
/// reference chart. Decoding is lenient and flags malformed groups via an InfoBar. All transform logic
/// lives in the pure <see cref="BaconCipher"/> (thin-VM convention).
/// </summary>
[Tool("BaconCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["bacon", "baconian", "biliteral", "cipher", "binary", "steganography",
                  "baconova", "baconova šifra", "dvojková", "šifra"])]
public sealed partial class BaconCipherViewModel : ToolViewModelBase
{
    private readonly BaconCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    private bool _suppressRecompute;

    public BaconCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("BaconCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        Recompute();
    }

    /// <summary>0 = Standard (I=J &amp; U=V), 1 = V2 (distinct values).</summary>
    [ObservableProperty]
    public partial int VersionIndex { get; set; }

    /// <summary>0 = Encrypt (text → groups), 1 = Decrypt (groups → text).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Invert the two symbol roles before encode/decode.</summary>
    [ObservableProperty]
    public partial bool SwapSymbols { get; set; }

    /// <summary>The symbol standing for bit 0 (default <c>A</c>). Only its first character is used.</summary>
    [ObservableProperty]
    public partial string FirstSymbol { get; set; } = "A";

    /// <summary>The symbol standing for bit 1 (default <c>B</c>). Only its first character is used.</summary>
    [ObservableProperty]
    public partial string SecondSymbol { get; set; } = "B";

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when a decode produced one or more malformed/unknown groups.</summary>
    [ObservableProperty]
    public partial bool HasDecodeWarning { get; set; }

    /// <summary>The reference chart for the active version, shown on the page.</summary>
    public ObservableCollection<BaconTableRow> ReferenceTable { get; } = [];

    private BaconVersion Version => VersionIndex == 1 ? BaconVersion.Distinct : BaconVersion.Standard;

    private bool IsDecrypt => DirectionIndex == 1;

    private BaconOptions Options => new(Version, FirstChar(FirstSymbol, 'A'), FirstChar(SecondSymbol, 'B'), SwapSymbols);

    partial void OnVersionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnSwapSymbolsChanged(bool value) => Recompute();

    partial void OnFirstSymbolChanged(string value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnSecondSymbolChanged(string value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        RefreshReferenceTable();

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasDecodeWarning = false;
            return;
        }

        var options = Options;
        if (IsDecrypt)
        {
            OutputText = _cipher.Decode(InputText, options);
            HasDecodeWarning = OutputText.Contains(BaconCipher.UnknownMarker);
        }
        else
        {
            OutputText = _cipher.Encode(InputText, options);
            HasDecodeWarning = false;
        }

        HasOutput = !string.IsNullOrEmpty(OutputText);
    }

    private void RefreshReferenceTable()
    {
        ReferenceTable.Clear();
        foreach (var row in _cipher.GetReferenceTable(Version))
        {
            ReferenceTable.Add(row);
        }
    }

    /// <summary>Swaps encrypt↔decrypt and feeds the previous output back in as the new input.</summary>
    [RelayCommand]
    private void SwapDirection()
    {
        _suppressRecompute = true;
        var previousOutput = OutputText;
        DirectionIndex = IsDecrypt ? 0 : 1;
        InputText = previousOutput;
        _suppressRecompute = false;
        Recompute();
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

    /// <summary>The first character of a symbol field, falling back to <paramref name="fallback"/> when blank.</summary>
    private static char FirstChar(string value, char fallback)
        => string.IsNullOrEmpty(value) ? fallback : value[0];
}
