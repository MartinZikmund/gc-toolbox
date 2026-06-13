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
/// Affine cipher (issue #3). A monoalphabetic substitution <c>E(x) = (a·x + b) mod 26</c> over A–Z that
/// encodes and decodes live as the user types, with a multiplier <c>a</c> dropdown (the 12 values coprime
/// to 26), an offset <c>b</c> dropdown (0–26), an encode/decode toggle, the live substitution "key" rows,
/// and a beyond-parity "auto-solve" that lists every candidate decryption across all 324 valid keys for
/// brute-forcing an unknown key. The decode inverse <c>aⁱⁿᵛ</c> is surfaced too. All transform logic lives
/// in the pure <see cref="AffineCipher"/> (thin-VM convention).
/// </summary>
[Tool("AffineCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["affine", "cipher", "monoalphabetic", "substitution", "modular", "afinní", "afinni", "šifra", "sifra", "substituce", "modulární"])]
public sealed partial class AffineCipherViewModel : ToolViewModelBase
{
    private readonly AffineCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public AffineCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("AffineCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        Recompute();
    }

    /// <summary>The 12 multipliers coprime to 26 — the only valid choices for <see cref="Multiplier"/>.</summary>
    public IReadOnlyList<int> Multipliers { get; } = AffineCipher.ValidMultipliers;

    /// <summary>The selectable offsets 0–26 (26 ≡ 0 mod 26), mirroring the reference site.</summary>
    public IReadOnlyList<int> Offsets { get; } = [.. Enumerable.Range(0, AffineCipher.MaxOffset + 1)];

    /// <summary>The multiplier <c>a</c> (default 5 — a common coprime value).</summary>
    [ObservableProperty]
    public partial int Multiplier { get; set; } = 5;

    /// <summary>The offset <c>b</c> (default 8).</summary>
    [ObservableProperty]
    public partial int Offset { get; set; } = 8;

    /// <summary>0 = Encode, 1 = Decode.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AutoSolve { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The modular inverse <c>aⁱⁿᵛ</c> of the current multiplier, shown as the decode key hint.</summary>
    [ObservableProperty]
    public partial int ModularInverse { get; set; }

    /// <summary>The plain alphabet row of the substitution "key" (<c>A B C … Z</c>).</summary>
    [ObservableProperty]
    public partial string KeyPlain { get; set; } = string.Empty;

    /// <summary>The substituted alphabet row of the "key", aligned under <see cref="KeyPlain"/>.</summary>
    [ObservableProperty]
    public partial string KeyCipher { get; set; } = string.Empty;

    /// <summary>The candidate decryptions shown in "auto-solve" mode (one per valid key).</summary>
    public ObservableCollection<AffineCandidateItem> Candidates { get; } = [];

    private bool IsDecode => DirectionIndex == 1;

    partial void OnMultiplierChanged(int value) => Recompute();

    partial void OnOffsetChanged(int value) => Recompute();

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnAutoSolveChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        // A ComboBox can momentarily report no selection while re-templating; ignore invalid multipliers.
        if (!_cipher.IsCoprime(Multiplier))
        {
            return;
        }

        ModularInverse = _cipher.ModInverse(Multiplier);

        // The live substitution key always reflects the current key (encode mapping A→…).
        KeyPlain = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        KeyCipher = _cipher.SubstitutionAlphabet(Multiplier, Offset);

        Candidates.Clear();

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        if (AutoSolve)
        {
            foreach (var candidate in _cipher.BruteForce(InputText))
            {
                Candidates.Add(new AffineCandidateItem(candidate.A, candidate.B, candidate.Text, _clipboard.SetText));
            }

            OutputText = string.Empty;
        }
        else
        {
            OutputText = _cipher.Transform(InputText, Multiplier, Offset, IsDecode);
        }

        HasOutput = true;
    }

    /// <summary>The text the Copy/Share actions emit: the single result, or every candidate line in auto-solve mode.</summary>
    private string BuildResultText()
        => AutoSolve
            ? string.Join(Environment.NewLine, Candidates.Select(c => $"{c.Key}: {c.Text}"))
            : OutputText;

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(BuildResultText());

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildResultText());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
