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
/// Four-square (Delastelle) digraph cipher (issue #15). Encrypts and decrypts text two letters at a
/// time using four 5×5 squares — two plain and two built from the user's keywords — updating live as
/// the keywords, alphabet mode (I/J merge or skip a letter), filler and direction change. The four
/// squares render as accessible glyph grids and all transform logic lives in the pure
/// <see cref="FourSquareCipher"/> (thin-VM convention). Fully offline.
/// </summary>
[Tool("FourSquareCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["four-square", "four square", "delastelle", "digraph", "polygraphic", "cipher", "šifra"])]
public sealed partial class FourSquareCipherViewModel : ToolViewModelBase
{
    private const string DefaultTopRightKeyword = "EXAMPLE";
    private const string DefaultBottomLeftKeyword = "KEYWORD";

    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    private FourSquareCipher _cipher;
    private bool _suppressRecompute;

    public FourSquareCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("FourSquareCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _cipher = BuildCipher();
        RebuildSquares();
    }

    /// <summary>The keyword that seeds the top-right cipher square.</summary>
    [ObservableProperty]
    public partial string TopRightKeyword { get; set; } = DefaultTopRightKeyword;

    /// <summary>The keyword that seeds the bottom-left cipher square.</summary>
    [ObservableProperty]
    public partial string BottomLeftKeyword { get; set; } = DefaultBottomLeftKeyword;

    /// <summary>0 = Encrypt, 1 = Decrypt.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>0 = merge two letters (J→I), 1 = skip a single letter.</summary>
    [ObservableProperty]
    public partial int AlphabetModeIndex { get; set; }

    /// <summary>The letter omitted from the square in skip mode (default Q, matching the Wikipedia example).</summary>
    [ObservableProperty]
    public partial string SkipLetter { get; set; } = "Q";

    /// <summary>The filler letter appended to odd-length plaintext (default X).</summary>
    [ObservableProperty]
    public partial string Filler { get; set; } = "X";

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when a filler was appended to make the input even-length.</summary>
    [ObservableProperty]
    public partial bool PaddingApplied { get; set; }

    /// <summary>The top-left / bottom-right plain square cells (row-major, 25 entries).</summary>
    public ObservableCollection<FourSquareCellItem> PlainSquare { get; } = [];

    /// <summary>The top-right keyword square cells.</summary>
    public ObservableCollection<FourSquareCellItem> TopRightSquare { get; } = [];

    /// <summary>The bottom-left keyword square cells.</summary>
    public ObservableCollection<FourSquareCellItem> BottomLeftSquare { get; } = [];

    /// <summary><see langword="true"/> when skip mode is active (drives the skip-letter input's visibility).</summary>
    public bool IsSkipMode => AlphabetModeIndex == 1;

    private bool IsDecrypt => DirectionIndex == 1;

    partial void OnTopRightKeywordChanged(string value) => RebuildCipher();

    partial void OnBottomLeftKeywordChanged(string value) => RebuildCipher();

    partial void OnAlphabetModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSkipMode));
        RebuildCipher();
    }

    partial void OnSkipLetterChanged(string value) => RebuildCipher();

    partial void OnFillerChanged(string value) => RebuildCipher();

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
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
        SwapCommand.NotifyCanExecuteChanged();
    }

    private FourSquareAlphabet ActiveAlphabet
        => IsSkipMode ? FourSquareAlphabet.Skip(FirstLetterOr(SkipLetter, 'Q')) : FourSquareAlphabet.MergeJIntoI;

    private FourSquareCipher BuildCipher()
        => new(TopRightKeyword ?? string.Empty, BottomLeftKeyword ?? string.Empty, ActiveAlphabet, FirstLetterOr(Filler, 'X'));

    private void RebuildCipher()
    {
        if (_suppressRecompute)
        {
            return;
        }

        _cipher = BuildCipher();
        RebuildSquares();
        Recompute();
    }

    private void RebuildSquares()
    {
        FillSquare(PlainSquare, _cipher.PlainSquare, "FourSquarePlainCell");
        FillSquare(TopRightSquare, _cipher.TopRightSquare, "FourSquareTopRightCell");
        FillSquare(BottomLeftSquare, _cipher.BottomLeftSquare, "FourSquareBottomLeftCell");
    }

    private static void FillSquare(ObservableCollection<FourSquareCellItem> target, IReadOnlyList<char> letters, string namePrefix)
    {
        target.Clear();
        for (var i = 0; i < letters.Count; i++)
        {
            var row = i / 5;
            var col = i % 5;
            target.Add(new FourSquareCellItem(letters[i], row, col, $"{namePrefix} {letters[i]}"));
        }
    }

    private void Recompute()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            PaddingApplied = false;
            ClearHighlights();
            return;
        }

        var result = IsDecrypt ? _cipher.Decrypt(InputText) : _cipher.Encrypt(InputText);
        OutputText = result.Text;
        PaddingApplied = result.PaddingApplied;
        HasOutput = result.Text.Length > 0;

        HighlightFirstDigraph();
    }

    /// <summary>
    /// Highlights the cells of the first processed digraph: the two source letters in the plain
    /// squares and the two intersection cells in the keyword squares (swapped for decrypt). Gives the
    /// user a live, visual trace of how a pair maps through the four squares.
    /// </summary>
    private void HighlightFirstDigraph()
    {
        ClearHighlights();

        var pair = FirstDigraph();
        if (pair is not (char a, char b))
        {
            return;
        }

        var (sourceA, sourceB, targetA, targetB) = IsDecrypt
            ? (TopRightSquare, BottomLeftSquare, PlainSquare, PlainSquare)
            : (PlainSquare, PlainSquare, TopRightSquare, BottomLeftSquare);

        var posA = Locate(sourceA, a);
        var posB = Locate(sourceB, b);
        if (posA is not (int ra, int ca) || posB is not (int rb, int cb))
        {
            return;
        }

        Set(sourceA, ra, ca);
        Set(sourceB, rb, cb);
        Set(targetA, ra, cb);   // first cipher letter: row of A, column of B
        Set(targetB, rb, ca);   // second cipher letter: row of B, column of A
    }

    /// <summary>The first representable digraph of the (sanitized, padded) input, or <see langword="null"/>.</summary>
    private (char, char)? FirstDigraph()
    {
        char? first = null;
        foreach (var raw in InputText)
        {
            if (!char.IsLetter(raw))
            {
                continue;
            }

            if (ActiveAlphabet.Normalize(char.ToUpperInvariant(raw)) is not char c)
            {
                continue;
            }

            if (first is null)
            {
                first = c;
            }
            else
            {
                return (first.Value, c);
            }
        }

        // A lone letter pairs with the filler.
        return first is char only ? (only, FirstLetterOr(Filler, 'X')) : null;
    }

    private void ClearHighlights()
    {
        ClearHighlights(PlainSquare);
        ClearHighlights(TopRightSquare);
        ClearHighlights(BottomLeftSquare);
    }

    private static void ClearHighlights(ObservableCollection<FourSquareCellItem> square)
    {
        foreach (var cell in square)
        {
            cell.IsHighlighted = false;
        }
    }

    private static (int Row, int Col)? Locate(ObservableCollection<FourSquareCellItem> square, char letter)
    {
        foreach (var cell in square)
        {
            if (cell.Letter.Length == 1 && cell.Letter[0] == letter)
            {
                return (cell.Row, cell.Column);
            }
        }

        return null;
    }

    private static void Set(ObservableCollection<FourSquareCellItem> square, int row, int col)
    {
        foreach (var cell in square)
        {
            if (cell.Row == row && cell.Column == col)
            {
                cell.IsHighlighted = true;
                return;
            }
        }
    }

    private static char FirstLetterOr(string? value, char fallback)
    {
        if (!string.IsNullOrEmpty(value))
        {
            foreach (var c in value)
            {
                if (char.IsLetter(c))
                {
                    return char.ToUpperInvariant(c);
                }
            }
        }

        return fallback;
    }

    /// <summary>Carries the current result back into the input and flips direction — a one-tap round-trip.</summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void Swap()
    {
        _suppressRecompute = true;
        InputText = OutputText;
        DirectionIndex = IsDecrypt ? 0 : 1;
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
}
