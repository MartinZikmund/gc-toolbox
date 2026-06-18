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

/// <summary>One row of the on-screen keyboard, paired plain key over its shifted substitute.</summary>
public sealed record QwertyKeyboardKey(string Plain, string Shifted);

/// <summary>One brute-force row: the shift amount and the resulting text, ready to bind.</summary>
public sealed record QwertyBruteForceRow(int Shift, string Text);

/// <summary>
/// QWERTY (keyboard) shifter (issue #218). Slides each character along the keyboard's key ordering by N
/// with circular wraparound, live in both directions. Goes beyond cachesleuth.com parity (36/47-key sets,
/// left/right, wrap, pass-through) with a brute-force "all shifts" list, selectable layouts
/// (QWERTY/QWERTZ/AZERTY), a row-bounded shift model, an on-screen substitution keyboard, batch multi-line
/// input, fold-to-upper, and copy/share — all fully offline.
/// </summary>
[Tool("QwertyShifter", ToolCategory.Alphabets,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["qwerty", "keyboard shift", "shift", "keyboard", "qwertz", "azerty", "klávesnice", "posun"])]
public sealed partial class QwertyShifterViewModel : ToolViewModelBase
{
    private readonly QwertyShifter _shifter = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public QwertyShifterViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("QwertyShifter", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>0 = Right, 1 = Left.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>0 = A-Z + 0-9 (36), 1 = include special characters (47).</summary>
    [ObservableProperty]
    public partial int AlphabetIndex { get; set; }

    /// <summary>0 = QWERTY, 1 = QWERTZ, 2 = AZERTY.</summary>
    [ObservableProperty]
    public partial int LayoutIndex { get; set; }

    /// <summary>0 = linear (whole keyboard), 1 = row-bounded.</summary>
    [ObservableProperty]
    public partial int ModelIndex { get; set; }

    [ObservableProperty]
    public partial int Shift { get; set; } = 1;

    /// <summary>Upper bound for the shift slider/entry, derived from the active alphabet size.</summary>
    [ObservableProperty]
    public partial int MaxShift { get; set; } = QwertyShifter.LettersDigitsSize;

    [ObservableProperty]
    public partial bool FoldToUpper { get; set; }

    /// <summary><see langword="true"/> when each line is shifted independently and listed.</summary>
    [ObservableProperty]
    public partial bool BatchMode { get; set; }

    /// <summary>The plain/shifted key pairs for the on-screen keyboard, one collection per row.</summary>
    public ObservableCollection<ObservableCollection<QwertyKeyboardKey>> KeyboardRows { get; } = [];

    /// <summary>Every candidate shift (1..size-1) for cracking an unknown shift.</summary>
    public ObservableCollection<QwertyBruteForceRow> BruteForceResults { get; } = [];

    private ShiftDirection Direction => DirectionIndex == 1 ? ShiftDirection.Left : ShiftDirection.Right;

    private QwertyAlphabet Alphabet => AlphabetIndex == 1 ? QwertyAlphabet.Extended : QwertyAlphabet.LettersDigits;

    private KeyboardLayout Layout => LayoutIndex switch
    {
        1 => KeyboardLayout.Qwertz,
        2 => KeyboardLayout.Azerty,
        _ => KeyboardLayout.Qwerty,
    };

    private ShiftModel Model => ModelIndex == 1 ? ShiftModel.RowBounded : ShiftModel.Linear;

    public override void ViewCreated()
    {
        base.ViewCreated();
        Recompute();
    }

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnDirectionIndexChanged(int value) => Recompute();

    partial void OnLayoutIndexChanged(int value) => Recompute();

    partial void OnModelIndexChanged(int value) => Recompute();

    partial void OnFoldToUpperChanged(bool value) => Recompute();

    partial void OnBatchModeChanged(bool value) => Recompute();

    partial void OnShiftChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, MaxShift);
        if (clamped != value)
        {
            Shift = clamped;
            return; // The re-assignment re-enters with the clamped value.
        }

        Recompute();
    }

    partial void OnAlphabetIndexChanged(int value)
    {
        // The alphabet size sets the shift ceiling; re-clamp before recomputing.
        MaxShift = QwertyShifter.AlphabetSize(Alphabet);
        if (Shift > MaxShift)
        {
            Shift = MaxShift; // Triggers a recompute via OnShiftChanged.
            return;
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
        OutputText = Transform(InputText);
        HasOutput = !string.IsNullOrEmpty(OutputText);

        UpdateKeyboard();
        UpdateBruteForce();
    }

    private string Transform(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (!BatchMode)
        {
            return ShiftLine(text);
        }

        // Each line is shifted independently so multi-line batches stay aligned.
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return string.Join("\n", lines.Select(ShiftLine));

        string ShiftLine(string line) => _shifter.Encode(line, Shift, Direction, Alphabet, Layout, Model, FoldToUpper);
    }

    private void UpdateKeyboard()
    {
        KeyboardRows.Clear();

        var rows = _shifter.KeyRows(Layout, Alphabet);
        foreach (var row in rows)
        {
            var shifted = _shifter.Encode(row, Shift, Direction, Alphabet, Layout, Model, foldToUpper: true);
            var keys = new ObservableCollection<QwertyKeyboardKey>();
            for (var i = 0; i < row.Length; i++)
            {
                keys.Add(new QwertyKeyboardKey(row[i].ToString(), shifted[i].ToString()));
            }

            KeyboardRows.Add(keys);
        }
    }

    private void UpdateBruteForce()
    {
        BruteForceResults.Clear();
        if (string.IsNullOrEmpty(InputText))
        {
            return;
        }

        // Brute force the first line only — that's the puzzle text being cracked.
        var firstLine = InputText.Replace("\r\n", "\n").Split('\n')[0];
        foreach (var candidate in _shifter.BruteForce(firstLine, Direction, Alphabet, Layout, FoldToUpper))
        {
            BruteForceResults.Add(new QwertyBruteForceRow(candidate.Shift, candidate.Text));
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
}
