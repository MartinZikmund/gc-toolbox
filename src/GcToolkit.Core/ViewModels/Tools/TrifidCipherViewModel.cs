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
/// Trifid cipher (issue #31) — the classic Delastelle fractionating cipher. Encodes and decodes live
/// as the user types, with a keyword-seeded (or explicit/random) 3×3×3 cube, all four fill orders, all
/// six coordinate reading orders, a configurable period and 27th filler symbol, and the cube rendered as
/// three labelled 3×3 squares. Visible input normalisation reports how many characters were dropped. All
/// transform logic lives in the pure <see cref="TrifidCipher"/> (thin-VM convention); copy/share are
/// best-effort and the tool is fully offline.
/// </summary>
[Tool("TrifidCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["trifid", "delastelle", "fractionation", "cube", "cipher", "šifra"])]
public sealed partial class TrifidCipherViewModel : ToolViewModelBase
{
    /// <summary>Filler symbols offered in the 27th-character selector (index maps to this list).</summary>
    public static readonly IReadOnlyList<char> FillerOptions = ['+', '.', '#', '@'];

    private const int DefaultPeriod = 5;

    private readonly TrifidCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;
    private readonly Random _random = new();

    private bool _suppressRecompute;
    private string _alphabet;

    public TrifidCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("TrifidCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        _alphabet = _cipher.BuildAlphabet();
        RebuildCube();
        Recompute();
    }

    /// <summary>0 = Encrypt, 1 = Decrypt.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The keyword that seeds the cube. Empty → the standard A–Z+ cube.</summary>
    [ObservableProperty]
    public partial string Keyword { get; set; } = string.Empty;

    /// <summary>Index into <see cref="TrifidFillOrder"/> (0 = SquareFirstByRow … 3 = ColumnFirst).</summary>
    [ObservableProperty]
    public partial int FillOrderIndex { get; set; }

    /// <summary>Index into <see cref="TrifidReadingOrder"/> (0 = SquareRowColumn … 5 = ColumnRowSquare).</summary>
    [ObservableProperty]
    public partial int ReadingOrderIndex { get; set; }

    /// <summary>Index into <see cref="FillerOptions"/> for the 27th symbol.</summary>
    [ObservableProperty]
    public partial int FillerIndex { get; set; }

    /// <summary>The period / block length; 0 (or less) treats the whole message as one block.</summary>
    [ObservableProperty]
    public partial int Period { get; set; } = DefaultPeriod;

    /// <summary>Double-typed bridge for the <c>NumberBox.Value</c> binding (which is <see cref="double"/>).</summary>
    public double PeriodValue
    {
        get => Period;
        set => Period = (int)Math.Round(value);
    }

    /// <summary><see langword="true"/> to use the whole message as a single block (ignores <see cref="Period"/>).</summary>
    [ObservableProperty]
    public partial bool WholeMessage { get; set; }

    /// <summary><see langword="true"/> when the last run dropped one or more out-of-alphabet characters.</summary>
    [ObservableProperty]
    public partial bool HasDropNotice { get; set; }

    [ObservableProperty]
    public partial string DropNotice { get; set; } = string.Empty;

    /// <summary>The cube's three squares, each a 3×3 grid of <see cref="TrifidCubeCell"/> (9 cells per square).</summary>
    public ObservableCollection<TrifidCubeCell> SquareOne { get; } = [];

    public ObservableCollection<TrifidCubeCell> SquareTwo { get; } = [];

    public ObservableCollection<TrifidCubeCell> SquareThree { get; } = [];

    private char Filler => FillerOptions[Math.Clamp(FillerIndex, 0, FillerOptions.Count - 1)];

    private TrifidFillOrder FillOrder => (TrifidFillOrder)Math.Clamp(FillOrderIndex, 0, 3);

    private TrifidReadingOrder ReadingOrder => (TrifidReadingOrder)Math.Clamp(ReadingOrderIndex, 0, 5);

    private int EffectivePeriod => WholeMessage ? 0 : Period;

    private bool IsDecrypt => DirectionIndex == 1;

    partial void OnDirectionIndexChanged(int value)
    {
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        // Switching direction carries the previous result into the input for one-tap round-trips.
        _suppressRecompute = true;
        InputText = OutputText;
        _suppressRecompute = false;
        Recompute();
    }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnKeywordChanged(string value) => RebuildAndRecompute();

    partial void OnFillOrderIndexChanged(int value)
    {
        if (value is >= 0 and <= 3)
        {
            RebuildAndRecompute();
        }
    }

    partial void OnFillerIndexChanged(int value)
    {
        if (value is >= 0 && value < FillerOptions.Count)
        {
            RebuildAndRecompute();
        }
    }

    partial void OnReadingOrderIndexChanged(int value)
    {
        if (value is >= 0 and <= 5)
        {
            Recompute();
        }
    }

    partial void OnPeriodChanged(int value)
    {
        OnPropertyChanged(nameof(PeriodValue));
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnWholeMessageChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void RebuildAndRecompute()
    {
        if (_suppressRecompute)
        {
            return;
        }

        _alphabet = _cipher.BuildAlphabet(Keyword, Filler, FillOrder);
        RebuildCube();
        Recompute();
    }

    /// <summary>Repopulates the three rendered squares from the current <see cref="_alphabet"/>.</summary>
    private void RebuildCube()
    {
        SquareOne.Clear();
        SquareTwo.Clear();
        SquareThree.Clear();

        for (var i = 0; i < TrifidCipher.CubeSize; i++)
        {
            var layer = i / 9 + 1;
            var row = i / 3 % 3 + 1;
            var col = i % 3 + 1;
            var cell = new TrifidCubeCell(layer, row, col, _alphabet[i]);
            switch (layer)
            {
                case 1: SquareOne.Add(cell); break;
                case 2: SquareTwo.Add(cell); break;
                default: SquareThree.Add(cell); break;
            }
        }
    }

    private void Recompute()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasDropNotice = false;
            DropNotice = string.Empty;
            return;
        }

        var result = IsDecrypt
            ? _cipher.Decrypt(InputText, _alphabet, EffectivePeriod, ReadingOrder)
            : _cipher.Encrypt(InputText, _alphabet, EffectivePeriod, ReadingOrder);

        OutputText = result.Text;
        HasOutput = result.Text.Length > 0;

        if (result.DroppedCount > 0)
        {
            HasDropNotice = true;
            DropNotice = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localizer["TrifidDropNotice"].Value,
                result.DroppedCount);
        }
        else
        {
            HasDropNotice = false;
            DropNotice = string.Empty;
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

    /// <summary>Swaps direction, carrying the current result into the input (one-tap round-trip).</summary>
    [RelayCommand]
    private void Swap() => DirectionIndex = IsDecrypt ? 0 : 1;

    [RelayCommand]
    private void Clear() => InputText = string.Empty;

    /// <summary>Resets the cube to the standard A–Z+ layout (clears the keyword and restores defaults).</summary>
    [RelayCommand]
    private void ResetCube()
    {
        _suppressRecompute = true;
        Keyword = string.Empty;
        FillOrderIndex = 0;
        FillerIndex = 0;
        _suppressRecompute = false;
        RebuildAndRecompute();
    }

    /// <summary>Generates a random keyword-free cube by shuffling the alphabet (geocachingtoolbox "random cube").</summary>
    [RelayCommand]
    private void RandomCube()
    {
        var symbols = (TrifidCipher.Letters + Filler).ToCharArray();
        for (var i = symbols.Length - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (symbols[i], symbols[j]) = (symbols[j], symbols[i]);
        }

        _alphabet = new string(symbols);
        RebuildCube();
        Recompute();
    }
}
