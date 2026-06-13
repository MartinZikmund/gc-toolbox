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
/// Scytale (rod) cipher (issue #272). Encodes and decodes the ancient columnar transposition live as
/// the user types: the text is written across a chosen number of columns (the rod's diameter) and read
/// down them. Mirrors cachesleuth.com parity — columns key, random columns, an extra/pad character for
/// the last row, and an ignore-spaces toggle — then goes beyond it with an auto-solve that brute-forces
/// every plausible column count (cachesleuth makes you guess), plus copy/share. All transform logic
/// lives in the pure <see cref="ScytaleCipher"/> (thin-VM convention).
/// </summary>
[Tool("ScytaleCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["scytale", "skytale", "rod", "rail", "transposition", "columns", "caesar box", "cipher", "skytalé", "tyč", "sloupce", "transpozice", "šifra"])]
public sealed partial class ScytaleCipherViewModel : ToolViewModelBase
{
    private const int DefaultColumns = 4;

    private readonly ScytaleCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly Random _random = new();

    public ScytaleCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("ScytaleCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
    }

    /// <summary>Number of columns (rod diameter) — the key. Must be at least <see cref="ScytaleCipher.MinColumns"/>.</summary>
    [ObservableProperty]
    public partial int Columns { get; set; } = DefaultColumns;

    /// <summary>0 = Encrypt (write across, read down), 1 = Decrypt (the inverse).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Exclude spaces from the transposition (cachesleuth's "Ignore Spaces").</summary>
    [ObservableProperty]
    public partial bool IgnoreSpaces { get; set; }

    /// <summary>Pad the final incomplete row when encrypting (cachesleuth's "Extra Character").</summary>
    [ObservableProperty]
    public partial bool UsePadCharacter { get; set; }

    /// <summary>The single character used to pad the final row when <see cref="UsePadCharacter"/> is on.</summary>
    [ObservableProperty]
    public partial string PadCharacter { get; set; } = "X";

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>When on, lists a decoding for every plausible column count (the beyond-parity win).</summary>
    [ObservableProperty]
    public partial bool AutoSolve { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>Set when the column count is invalid (below the minimum) — surfaces the validation hint.</summary>
    [ObservableProperty]
    public partial bool HasColumnError { get; set; }

    /// <summary>The candidates shown in auto-solve mode (one per plausible column count).</summary>
    public ObservableCollection<ScytaleSolveItem> SolveResults { get; } = [];

    private bool IsDecode => DirectionIndex == 1;

    private char? PadChar
        => UsePadCharacter && !IsDecode && PadCharacter.Length > 0 ? PadCharacter[0] : null;

    public override void ViewCreated()
    {
        base.ViewCreated();
        Recompute();
    }

    partial void OnColumnsChanged(int value) => Recompute();

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnIgnoreSpacesChanged(bool value) => Recompute();

    partial void OnUsePadCharacterChanged(bool value) => Recompute();

    partial void OnPadCharacterChanged(string value)
    {
        // Keep only the first character — the pad is a single symbol.
        if (value.Length > 1)
        {
            PadCharacter = value[..1];
            return;
        }

        Recompute();
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
        SolveResults.Clear();
        HasColumnError = Columns < ScytaleCipher.MinColumns;

        if (HasColumnError || string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        if (AutoSolve)
        {
            foreach (var candidate in _cipher.AutoSolve(InputText))
            {
                SolveResults.Add(new ScytaleSolveItem(candidate.Columns, candidate.Text, _clipboard.SetText));
            }

            OutputText = string.Empty;
            HasOutput = SolveResults.Count > 0;
            return;
        }

        OutputText = IsDecode
            ? _cipher.Decrypt(InputText, Columns, IgnoreSpaces)
            : _cipher.Encrypt(InputText, Columns, IgnoreSpaces, PadChar);
        HasOutput = OutputText.Length > 0;
    }

    /// <summary>Picks a random column count in a useful range (2 … 12) and re-runs the transform.</summary>
    [RelayCommand]
    private void RandomizeColumns() => Columns = _random.Next(ScytaleCipher.MinColumns, 13);

    /// <summary>The text the Copy/Share actions emit: the single result, or every candidate line in auto-solve mode.</summary>
    private string BuildResultText()
        => AutoSolve
            ? string.Join(Environment.NewLine, SolveResults.Select(r => $"{r.Columns}: {r.Text}"))
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
