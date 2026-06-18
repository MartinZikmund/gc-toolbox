using System.Collections.ObjectModel;
using System.Text;
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
/// Playfair digraph cipher (issue #25). Encrypts/decrypts via a keyword-built (or manual) 5×5 key square,
/// shown live as the keyword is typed. Matches geocachingtoolbox.com parity — encrypt/decrypt toggle,
/// keyword square display, standard vs manual square, J→I merge vs skip-a-letter, selectable filler with
/// double-split on/off, odd-length padding, free-form input with non-letters stripped, copy/reset — and
/// goes beyond it: any merge pair / skip letter / filler / fill order, grouped 5-letter blocks, a
/// swap-direction button, multi-line batch mode, share, and a clear stripped-characters note. All cipher
/// logic lives in the pure <see cref="PlayfairCipher"/> (thin-VM convention). Fully offline, deterministic.
/// </summary>
[Tool("PlayfairCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["playfair", "digraph", "key square", "wheatstone", "cipher", "šifra", "klíčové slovo"])]
public sealed partial class PlayfairCipherViewModel : ToolViewModelBase
{
    private readonly PlayfairCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public PlayfairCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("PlayfairCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Recompute();
    }

    /// <summary>0 = Encrypt, 1 = Decrypt.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>0 = standard keyword square, 1 = manual/random square.</summary>
    [ObservableProperty]
    public partial int SquareSourceIndex { get; set; }

    /// <summary>0 = merge a letter (J→I), 1 = skip a letter (Q).</summary>
    [ObservableProperty]
    public partial int FitIndex { get; set; }

    /// <summary>0 = row-major fill, 1 = column-major fill.</summary>
    [ObservableProperty]
    public partial int FillOrderIndex { get; set; }

    [ObservableProperty]
    public partial string Keyword { get; set; } = "PLAYFAIR EXAMPLE";

    /// <summary>The 25 letters of a manual square (used when <see cref="SquareSourceIndex"/> is 1).</summary>
    [ObservableProperty]
    public partial string ManualSquare { get; set; } = "PLAYFIREXMBCDGHKNOQSTUVWZ";

    [ObservableProperty]
    public partial string MergeFrom { get; set; } = "J";

    [ObservableProperty]
    public partial string MergeTo { get; set; } = "I";

    [ObservableProperty]
    public partial string SkipLetter { get; set; } = "Q";

    [ObservableProperty]
    public partial string Filler { get; set; } = "X";

    /// <summary>When <see langword="true"/> repeated pairs are split by the filler; otherwise encoded directly.</summary>
    [ObservableProperty]
    public partial bool SplitDoubles { get; set; } = true;

    /// <summary>When <see langword="true"/> the output is shown in 5-letter blocks; otherwise continuous.</summary>
    [ObservableProperty]
    public partial bool GroupOutput { get; set; }

    /// <summary>When <see langword="true"/> each input line is transformed independently (batch mode).</summary>
    [ObservableProperty]
    public partial bool BatchMode { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasNotice { get; set; }

    [ObservableProperty]
    public partial string NoticeMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the manual square is the active key source — toggles the editor.</summary>
    [ObservableProperty]
    public partial bool IsManualSquare { get; set; }

    /// <summary><see langword="true"/> for the merge fit (shows the from/to inputs), <see langword="false"/> for skip (shows the skip-letter input).</summary>
    [ObservableProperty]
    public partial bool IsMergeFit { get; set; } = true;

    /// <summary>The 25 cells of the live key square, for the View's 5×5 grid.</summary>
    public ObservableCollection<PlayfairSquareCell> SquareCells { get; } = [];

    private bool IsDecrypt => DirectionIndex == 1;

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnSquareSourceIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            IsManualSquare = value == 1;
            Recompute();
        }
    }

    partial void OnFitIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            IsMergeFit = value == 0;
            Recompute();
        }
    }

    partial void OnFillOrderIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnKeywordChanged(string value) => Recompute();

    partial void OnManualSquareChanged(string value) => Recompute();

    partial void OnMergeFromChanged(string value) => Recompute();

    partial void OnMergeToChanged(string value) => Recompute();

    partial void OnSkipLetterChanged(string value) => Recompute();

    partial void OnFillerChanged(string value) => Recompute();

    partial void OnSplitDoublesChanged(bool value) => Recompute();

    partial void OnGroupOutputChanged(bool value) => Recompute();

    partial void OnBatchModeChanged(bool value) => Recompute();

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private PlayfairOptions BuildOptions()
    {
        var manual = SquareSourceIndex == 1 ? NormalizeManual(ManualSquare) : null;
        return new PlayfairOptions
        {
            Keyword = Keyword ?? string.Empty,
            ManualSquare = manual,
            Fit = FitIndex == 1 ? PlayfairFit.Skip : PlayfairFit.Merge,
            MergeFrom = FirstLetterOr(MergeFrom, 'J'),
            MergeTo = FirstLetterOr(MergeTo, 'I'),
            SkipLetter = FirstLetterOr(SkipLetter, 'Q'),
            Filler = FirstLetterOr(Filler, 'X'),
            FillOrder = FillOrderIndex == 1 ? PlayfairFillOrder.ColumnMajor : PlayfairFillOrder.RowMajor,
            SplitDoubles = SplitDoubles,
        };
    }

    private void Recompute()
    {
        var options = BuildOptions();

        if (!TryRefreshSquare(options))
        {
            // An invalid manual square blocks the square display and the transform.
            OutputText = string.Empty;
            HasOutput = false;
            HasNotice = true;
            NoticeMessage = _localizer["PlayfairErrorManualSquare"].Value;
            return;
        }

        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasNotice = false;
            NoticeMessage = string.Empty;
            return;
        }

        var lines = BatchMode
            ? InputText.Split('\n')
            : [InputText];

        var outputs = new List<string>(lines.Length);
        var totalStripped = 0;
        string? errorKey = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var result = IsDecrypt ? _cipher.Decrypt(line, options) : _cipher.Encrypt(line, options);
            totalStripped += result.StrippedCount;
            errorKey ??= result.ErrorKey;
            outputs.Add(GroupOutput ? Group(result.Text) : result.Text);
        }

        OutputText = string.Join(Environment.NewLine, outputs);
        HasOutput = outputs.Exists(o => o.Length > 0);

        if (errorKey is not null)
        {
            HasNotice = true;
            NoticeMessage = _localizer[errorKey].Value;
        }
        else if (totalStripped > 0)
        {
            HasNotice = true;
            NoticeMessage = string.Format(_localizer["PlayfairStrippedNotice"].Value, totalStripped);
        }
        else
        {
            HasNotice = false;
            NoticeMessage = string.Empty;
        }
    }

    /// <summary>Rebuilds <see cref="SquareCells"/> from the options. Returns <see langword="false"/> when a
    /// manual square is invalid (so the caller can surface the error and skip the transform).</summary>
    private bool TryRefreshSquare(PlayfairOptions options)
    {
        IReadOnlyList<string> rows;
        try
        {
            rows = _cipher.BuildSquare(options);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var nameFormat = _localizer["PlayfairCellName"].Value;

        SquareCells.Clear();
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < row.Length; c++)
            {
                var letter = row[c].ToString();
                var name = string.Format(nameFormat, r + 1, c + 1, letter);
                SquareCells.Add(new PlayfairSquareCell(letter, r + 1, c + 1, name));
            }
        }

        return true;
    }

    private static string Group(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + text.Length / 5);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && i % 5 == 0)
            {
                builder.Append(' ');
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    private static string? NormalizeManual(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty; // empty/whitespace stays invalid → surfaces the manual-square error.
        }

        var builder = new StringBuilder(25);
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    private static char FirstLetterOr(string? text, char fallback)
    {
        if (!string.IsNullOrEmpty(text))
        {
            foreach (var c in text)
            {
                if (char.IsLetter(c))
                {
                    return char.ToUpperInvariant(c);
                }
            }
        }

        return fallback;
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

    /// <summary>Feeds the current output back as input and flips the direction (one-tap round-trip check).</summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void SwapDirection()
    {
        var carry = OutputText;
        DirectionIndex = IsDecrypt ? 0 : 1;
        InputText = carry;
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
