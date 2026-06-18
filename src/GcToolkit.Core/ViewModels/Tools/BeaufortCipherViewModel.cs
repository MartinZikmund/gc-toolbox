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

/// <summary>One row of the displayed tabula recta: a header letter plus the row's cipher letters.</summary>
public sealed record TabulaRow(string Header, string Letters);

/// <summary>
/// Beaufort cipher (issue #126) — a reciprocal "reversed Vigenère". Enciphers/deciphers live as the
/// user types, across three flavours: <see cref="BeaufortVariant.Standard"/> (<c>C = K - P</c>,
/// self-reciprocal), <see cref="BeaufortVariant.Variant"/> "German" (<c>C = P - K</c>) and
/// <see cref="BeaufortVariant.Autokey"/>. A repeating, case-insensitive keyword drives the transform;
/// non-alphabet characters pass through and case is preserved. Offers an optional custom alphabet, an
/// interactive tabula recta, copy/share/clear and inline validation. All cipher logic lives in the
/// pure <see cref="BeaufortCipher"/> (thin-VM convention); everything runs fully offline.
/// </summary>
[Tool("BeaufortCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["beaufort", "vigenere", "vigenère", "reciprocal", "tabula recta", "cipher", "šifra", "klíč"])]
public sealed partial class BeaufortCipherViewModel : ToolViewModelBase
{
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private BeaufortCipher _cipher = new();

    public BeaufortCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("BeaufortCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        RebuildTableau();
    }

    /// <summary>Active variant: 0 = Standard, 1 = Variant (German), 2 = Autokey.</summary>
    [ObservableProperty]
    public partial int VariantIndex { get; set; }

    /// <summary>0 = Encrypt, 1 = Decrypt. Hidden/ignored for Standard, which is self-reciprocal.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string Keyword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    /// <summary>Optional custom cipher alphabet. Blank uses the default A–Z.</summary>
    [ObservableProperty]
    public partial string CustomAlphabet { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> for Standard, where the same op enciphers and deciphers (no direction needed).</summary>
    [ObservableProperty]
    public partial bool IsReciprocal { get; set; } = true;

    /// <summary>A one-line explainer for the active variant, shown beneath the picker.</summary>
    [ObservableProperty]
    public partial string VariantExplainer { get; set; } = string.Empty;

    /// <summary>The displayed tabula recta rows (header letter + the row of cipher letters).</summary>
    public ObservableCollection<TabulaRow> TableauRows { get; } = [];

    /// <summary>The alphabet header shown above the tabula recta columns.</summary>
    [ObservableProperty]
    public partial string TableauHeader { get; set; } = string.Empty;

    private BeaufortVariant Variant => VariantIndex switch
    {
        1 => BeaufortVariant.Variant,
        2 => BeaufortVariant.Autokey,
        _ => BeaufortVariant.Standard,
    };

    private bool IsDecrypt => DirectionIndex == 1 && !IsReciprocal;

    partial void OnVariantIndexChanged(int value)
    {
        if (value is not (0 or 1 or 2))
        {
            return;
        }

        IsReciprocal = Variant == BeaufortVariant.Standard;
        Recompute();
    }

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnKeywordChanged(string value) => Recompute();

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnCustomAlphabetChanged(string value)
    {
        if (TryBuildCipher(out var cipher))
        {
            _cipher = cipher;
            RebuildTableau();
        }

        Recompute();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        VariantExplainer = _localizer[Variant switch
        {
            BeaufortVariant.Variant => "BeaufortExplainerVariant",
            BeaufortVariant.Autokey => "BeaufortExplainerAutokey",
            _ => "BeaufortExplainerStandard",
        }].Value;

        // An invalid custom alphabet is the most fundamental error — flag it and bail.
        if (!TryBuildCipher(out _))
        {
            Fail("BeaufortInvalidAlphabet");
            return;
        }

        if (string.IsNullOrEmpty(InputText))
        {
            ClearResult();
            return;
        }

        if (string.IsNullOrEmpty(Keyword))
        {
            Fail("BeaufortEmptyKeyword");
            return;
        }

        if (!_cipher.IsValidKeyword(Keyword))
        {
            Fail("BeaufortInvalidKeyword");
            return;
        }

        OutputText = IsDecrypt
            ? _cipher.Decrypt(InputText, Keyword, Variant)
            : _cipher.Encrypt(InputText, Keyword, Variant);
        HasOutput = !string.IsNullOrEmpty(OutputText);
        HasWarning = false;
        WarningMessage = string.Empty;
    }

    private void Fail(string messageKey)
    {
        OutputText = string.Empty;
        HasOutput = false;
        HasWarning = true;
        WarningMessage = _localizer[messageKey].Value;
    }

    private void ClearResult()
    {
        OutputText = string.Empty;
        HasOutput = false;
        HasWarning = false;
        WarningMessage = string.Empty;
    }

    private bool TryBuildCipher(out BeaufortCipher cipher)
    {
        if (string.IsNullOrWhiteSpace(CustomAlphabet))
        {
            cipher = new BeaufortCipher();
            return true;
        }

        try
        {
            cipher = new BeaufortCipher(CustomAlphabet.Trim());
            return true;
        }
        catch (ArgumentException)
        {
            cipher = null!;
            return false;
        }
    }

    private void RebuildTableau()
    {
        TableauRows.Clear();
        var alphabet = _cipher.Alphabet;
        TableauHeader = alphabet;
        var rows = _cipher.Tableau();
        for (var r = 0; r < rows.Count; r++)
        {
            TableauRows.Add(new TabulaRow(alphabet[r].ToString(), rows[r]));
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

    [RelayCommand]
    private void SwapInputOutput()
    {
        // Feed the result back as input for a one-tap round trip.
        InputText = OutputText;
    }
}
