using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Numbers;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One letter of the breakdown, ready to bind (the letter and its value in the focused system).</summary>
public sealed record NumerologyLetterRow(string Letter, int Value);

/// <summary>One word/line total in the focused system (the word, its raw total and reduced value).</summary>
public sealed record NumerologyWordRow(string Word, int Total, int Reduced);

/// <summary>One system's live summary: the localized name plus its total and reduced value.</summary>
public sealed partial class NumerologySystemRow : ObservableObject
{
    public NumerologySystemRow(NumerologySystem system, string name)
    {
        System = system;
        Name = name;
    }

    public NumerologySystem System { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial int Total { get; set; }

    [ObservableProperty]
    public partial int Reduced { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Numerology tool (issue #245). Live, all-systems-at-once scoring as the user types — Pythagorean
/// (1–9), Pythagorean (1–0), Chaldean, and Simple/Ordinal side by side, beyond cachesleuth.com's single
/// Calculate. A focused system drives the per-letter breakdown and per-word/line totals; the reduction
/// mode (full reduce / single step / raw total) and master-number (11/22/33) preservation are toggleable.
/// Copy/Share the full breakdown, Clear, and "no letters found" validation. Fully offline; all scoring
/// lives in the pure <see cref="NumerologyCodec"/>.
/// </summary>
[Tool("Numerology", ToolCategory.Numbers,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["numerology", "gematria", "pythagorean", "chaldean", "digital root", "numerologie", "číslo"])]
public sealed partial class NumerologyViewModel : ToolViewModelBase
{
    private readonly NumerologyCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public NumerologyViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Numerology", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        foreach (var system in NumerologyCodec.AllSystems)
        {
            Systems.Add(new NumerologySystemRow(system, _localizer[$"Numerology_System_{NumerologyCodec.SystemKey(system)}"].Value));
        }

        Systems[0].IsSelected = true;
        Recompute();
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>Index into <see cref="NumerologyCodec.AllSystems"/> for the focused system (drives the breakdown).</summary>
    [ObservableProperty]
    public partial int SelectedSystemIndex { get; set; }

    /// <summary>0 = full reduce (digital root), 1 = single step, 2 = raw total.</summary>
    [ObservableProperty]
    public partial int ReductionModeIndex { get; set; }

    [ObservableProperty]
    public partial bool PreserveMasterNumbers { get; set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial bool ShowEmptyNotice { get; set; }

    /// <summary>The focused system's total (e.g. <c>25</c>).</summary>
    [ObservableProperty]
    public partial int FocusedTotal { get; set; }

    /// <summary>The focused system's reduced result (e.g. <c>7</c>), shown large.</summary>
    [ObservableProperty]
    public partial int FocusedReduced { get; set; }

    /// <summary>Live summary for every system (all shown at once).</summary>
    public ObservableCollection<NumerologySystemRow> Systems { get; } = [];

    /// <summary>Per-letter breakdown for the focused system.</summary>
    public ObservableCollection<NumerologyLetterRow> Breakdown { get; } = [];

    /// <summary>Per-word / per-line totals for the focused system (only when more than one word).</summary>
    public ObservableCollection<NumerologyWordRow> WordTotals { get; } = [];

    [ObservableProperty]
    public partial bool ShowWordTotals { get; set; }

    private ReductionMode Mode => ReductionModeIndex switch
    {
        1 => ReductionMode.SingleStep,
        2 => ReductionMode.RawTotal,
        _ => ReductionMode.FullReduce,
    };

    private NumerologySystem FocusedSystem
    {
        get
        {
            var index = Math.Clamp(SelectedSystemIndex, 0, NumerologyCodec.AllSystems.Count - 1);
            return NumerologyCodec.AllSystems[index];
        }
    }

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnReductionModeIndexChanged(int value) => Recompute();

    partial void OnPreserveMasterNumbersChanged(bool value) => Recompute();

    partial void OnSelectedSystemIndexChanged(int value)
    {
        if (value is < 0)
        {
            return; // transient -1 from a selector clearing
        }

        for (var i = 0; i < Systems.Count; i++)
        {
            Systems[i].IsSelected = i == value;
        }

        Recompute();
    }

    partial void OnHasResultChanged(bool value)
    {
        CopyCommand.NotifyCanExecuteChanged();
        ShareCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        var all = _codec.AnalyzeAll(InputText, Mode, PreserveMasterNumbers);
        var hasLetters = all[FocusedSystem].HasLetters;

        foreach (var row in Systems)
        {
            var result = all[row.System];
            row.Total = result.Total;
            row.Reduced = result.Reduced;
        }

        var focused = all[FocusedSystem];
        FocusedTotal = focused.Total;
        FocusedReduced = focused.Reduced;

        Breakdown.Clear();
        WordTotals.Clear();

        if (hasLetters)
        {
            foreach (var letter in focused.Letters)
            {
                Breakdown.Add(new NumerologyLetterRow(letter.Letter.ToString(), letter.Value));
            }

            if (focused.Words.Count > 1)
            {
                foreach (var word in focused.Words)
                {
                    WordTotals.Add(new NumerologyWordRow(word.Word, word.Total, word.Reduced));
                }
            }
        }

        ShowWordTotals = WordTotals.Count > 0;
        HasResult = hasLetters;
        ShowEmptyNotice = !hasLetters && !string.IsNullOrWhiteSpace(InputText);
    }

    /// <summary>A plain-text summary of every system plus the focused breakdown — used by Copy/Share.</summary>
    private string BuildReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine(InputText.Trim());
        builder.AppendLine();

        foreach (var row in Systems)
        {
            builder.AppendLine($"{row.Name}: {row.Total} → {row.Reduced}");
        }

        if (Breakdown.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(string.Join("  ", Breakdown.Select(b => $"{b.Letter}={b.Value.ToString(CultureInfo.InvariantCulture)}")));
        }

        return builder.ToString().TrimEnd();
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void Copy() => _clipboard.SetText(BuildReport());

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildReport());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
