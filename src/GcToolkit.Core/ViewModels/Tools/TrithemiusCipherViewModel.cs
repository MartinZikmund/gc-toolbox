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
/// Trithemius cipher (issue #300) — the keyless progressive polyalphabetic ancestor of Vigenère.
/// Encodes/decodes live as the user types: the i-th letter is shifted by <c>startOffset + i*step</c>
/// (mod 26), so with the defaults <c>AAAA</c> becomes <c>ABCD</c> (cachesleuth.com parity). Renders the
/// 26×26 tabula recta as a reference. Goes beyond parity with configurable start offset / step and a
/// "show all offsets" view that decodes at every starting row for cracking an unknown start. All
/// transform logic lives in the pure <see cref="TrithemiusCipher"/> (thin-VM convention).
/// </summary>
[Tool("TrithemiusCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["trithemius", "progressive", "polyalphabetic", "tabula recta", "vigenere", "cipher", "šifra", "progresivní", "polyalfabetická"])]
public sealed partial class TrithemiusCipherViewModel : ToolViewModelBase
{
    private readonly TrithemiusCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public TrithemiusCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("TrithemiusCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        TabulaRecta = [.. _cipher.TabulaRecta()];
        Recompute();
    }

    /// <summary>0 = Encrypt (shift forward), 1 = Decrypt (shift back).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The starting row of the tabula recta (the shift applied to the first letter).</summary>
    [ObservableProperty]
    public partial int StartOffset { get; set; }

    /// <summary>How much the shift grows per letter. Default 1 gives the classic progression.</summary>
    [ObservableProperty]
    public partial int Step { get; set; } = 1;

    [ObservableProperty]
    public partial bool ShowAllOffsets { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The 26 rows of the tabula recta, shown as a monospace reference card.</summary>
    public ObservableCollection<string> TabulaRecta { get; }

    /// <summary>The decode candidates shown in "show all offsets" mode (one per starting row).</summary>
    public ObservableCollection<TrithemiusOffsetItem> OffsetResults { get; } = [];

    private bool IsDecrypt => DirectionIndex == 1;

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnStartOffsetChanged(int value) => Recompute();

    partial void OnStepChanged(int value) => Recompute();

    partial void OnShowAllOffsetsChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        OffsetResults.Clear();

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        if (ShowAllOffsets)
        {
            foreach (var candidate in _cipher.AllOffsets(InputText, Step))
            {
                OffsetResults.Add(new TrithemiusOffsetItem(candidate.StartOffset, candidate.Text, _clipboard.SetText));
            }

            OutputText = string.Empty;
        }
        else
        {
            OutputText = _cipher.Transform(InputText, IsDecrypt, StartOffset, Step);
        }

        HasOutput = true;
    }

    /// <summary>The text Copy/Share emit: the single result, or every offset line in "show all" mode.</summary>
    private string BuildResultText()
        => ShowAllOffsets
            ? string.Join(Environment.NewLine, OffsetResults.Select(r => $"{r.StartOffset}: {r.Text}"))
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
