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

/// <summary>
/// Tap / knock code tool (issue #289). Three live-linked representations — plain Letters, number pairs,
/// and dot taps — where editing any one re-derives the other two, plus a switchable 5×5 (C/K merged) or
/// 6×6 (letters + digits) Polybius square shown as a clickable chart. Beyond the cachesleuth.com parity it
/// adds full round-tripping from any of the three sides, lenient decoding of messy separators, accent
/// folding, and copy/share. All logic lives in the pure <see cref="TapCode"/> codec (thin-VM convention).
/// </summary>
[Tool("TapCode", ToolCategory.Alphabets,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["tap", "code", "knock", "polybius", "prisoner", "kód", "klepání", "ťukání", "vězeňský", "mřížka"])]
public sealed partial class TapCodeViewModel : ToolViewModelBase
{
    private readonly TapCode _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    // Guards the three-way binding so a derived update doesn't recursively re-derive the others.
    private bool _suppress;

    public TapCodeViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("TapCode", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        RebuildGrid();
    }

    [ObservableProperty]
    public partial string Letters { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Numbers { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Dots { get; set; } = string.Empty;

    /// <summary>0 = 5×5 (C/K merged), 1 = 6×6 (letters + digits).</summary>
    [ObservableProperty]
    public partial int GridSizeIndex { get; set; }

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The Polybius square rows shown as a chart you can click to type a letter.</summary>
    public ObservableCollection<IReadOnlyList<TapCodeCell>> GridRows { get; } = [];

    private TapCodeGrid Grid => GridSizeIndex == 1 ? TapCodeGrid.SixBySix : TapCodeGrid.FiveByFive;

    partial void OnLettersChanged(string value)
    {
        if (_suppress)
        {
            return;
        }

        DeriveFrom(_codec.EncodeNumbers(value, Grid), _codec.EncodeDots(value, Grid));
    }

    partial void OnNumbersChanged(string value)
    {
        if (_suppress)
        {
            return;
        }

        var letters = _codec.DecodeNumbers(value, Grid);
        SetSuppressed(letters: letters, dots: _codec.EncodeDots(letters, Grid));
        UpdateHasOutput();
    }

    partial void OnDotsChanged(string value)
    {
        if (_suppress)
        {
            return;
        }

        var letters = _codec.DecodeDots(value, Grid);
        SetSuppressed(letters: letters, numbers: _codec.EncodeNumbers(letters, Grid));
        UpdateHasOutput();
    }

    partial void OnGridSizeIndexChanged(int value)
    {
        if (value is not (0 or 1))
        {
            return;
        }

        RebuildGrid();

        // Re-encode from the Letters side so switching squares re-derives the numbers/dots consistently.
        if (!_suppress)
        {
            DeriveFrom(_codec.EncodeNumbers(Letters, Grid), _codec.EncodeDots(Letters, Grid));
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyLettersCommand.NotifyCanExecuteChanged();
        CopyNumbersCommand.NotifyCanExecuteChanged();
        ShareCommand.NotifyCanExecuteChanged();
    }

    /// <summary>"Types" a clicked chart cell's letter into the Letters field.</summary>
    [RelayCommand]
    private void Insert(TapCodeCell? cell)
    {
        if (cell is null)
        {
            return;
        }

        // The merged 5×5 cell types its canonical C.
        Letters += cell.Label is "C/K" ? "C" : cell.Label;
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyLetters() => _clipboard.SetText(Letters);

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyNumbers() => _clipboard.SetText(Numbers);

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task ShareAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, Numbers);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear()
    {
        SetSuppressed(string.Empty, string.Empty, string.Empty);
        UpdateHasOutput();
    }

    private void DeriveFrom(string numbers, string dots)
    {
        SetSuppressed(numbers: numbers, dots: dots);
        UpdateHasOutput();
    }

    private void SetSuppressed(string? letters = null, string? numbers = null, string? dots = null)
    {
        _suppress = true;
        if (letters is not null)
        {
            Letters = letters;
        }

        if (numbers is not null)
        {
            Numbers = numbers;
        }

        if (dots is not null)
        {
            Dots = dots;
        }

        _suppress = false;
    }

    private void UpdateHasOutput() =>
        HasOutput = !string.IsNullOrEmpty(Letters) || !string.IsNullOrEmpty(Numbers) || !string.IsNullOrEmpty(Dots);

    private void RebuildGrid()
    {
        GridRows.Clear();
        foreach (var row in TapCode.BuildGrid(Grid))
        {
            GridRows.Add(row);
        }
    }
}
