using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Text;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Numbers ↔ letters / A1Z26 tool (issue #12). Converts live in either direction: letters → their
/// position numbers (A=1 … Z=26) and numbers → letters. Beyond geocachingtoolbox.com parity (which
/// only decodes numbers → letters with a fixed space separator) this tool does both directions in one
/// view, takes a custom separator, can keep or strip non-letters when encoding, lets the output case be
/// chosen, and either wraps out-of-range values modulo 26 or flags them. All transform logic lives in
/// the pure <see cref="AlphabetNumbers"/> codec (thin-VM convention).
/// </summary>
[Tool("NumbersToLetters", ToolCategory.Text,
      Introduced = "2026-06-06", Updated = "2026-06-06",
      Keywords = ["a1z26", "numbers", "letters", "alphabet", "position", "index", "číslapísmena", "čísla", "písmena", "abeceda", "pozice", "letter to number", "number to letter"])]
public sealed partial class NumbersToLettersViewModel : ToolViewModelBase
{
    private readonly AlphabetNumbers _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public NumbersToLettersViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("NumbersToLetters", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        InputPlaceholder = _localizer["NumbersInputPlaceholderLetters"].Value;
    }

    /// <summary>0 = letters → numbers, 1 = numbers → letters.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The string placed between numbers (encode) / split on (decode). Defaults to a space.</summary>
    [ObservableProperty]
    public partial string Separator { get; set; } = " ";

    /// <summary>When decoding, wrap values outside 1–26 back into range modulo 26 instead of flagging them.</summary>
    [ObservableProperty]
    public partial bool WrapModulo { get; set; }

    /// <summary>When decoding, emit upper-case letters (otherwise lower-case).</summary>
    [ObservableProperty]
    public partial bool UpperCase { get; set; } = true;

    /// <summary>When encoding, keep non-letter characters verbatim instead of dropping them.</summary>
    [ObservableProperty]
    public partial bool KeepNonLetters { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> while the letters → numbers direction is active (drives which option set shows).</summary>
    [ObservableProperty]
    public partial bool IsLettersToNumbers { get; set; } = true;

    /// <summary>Direction-aware placeholder for the input box.</summary>
    [ObservableProperty]
    public partial string InputPlaceholder { get; set; } = string.Empty;

    private bool IsDecode => DirectionIndex == 1;

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit while re-templating.
        if (value is not (0 or 1))
        {
            return;
        }

        IsLettersToNumbers = value == 0;
        InputPlaceholder = _localizer[IsLettersToNumbers ? "NumbersInputPlaceholderLetters" : "NumbersInputPlaceholderNumbers"].Value;
        Recompute();
    }

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnSeparatorChanged(string value) => Recompute();

    partial void OnWrapModuloChanged(bool value) => Recompute();

    partial void OnUpperCaseChanged(bool value) => Recompute();

    partial void OnKeepNonLettersChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        // An empty separator box falls back to the default single space (the parity convention).
        var options = new AlphabetNumberOptions
        {
            Separator = string.IsNullOrEmpty(Separator) ? " " : Separator,
            WrapModulo = WrapModulo,
            UpperCase = UpperCase,
            KeepNonLetters = KeepNonLetters,
        };

        OutputText = IsDecode
            ? _codec.NumbersToLetters(InputText, options)
            : _codec.LettersToNumbers(InputText, options);

        HasOutput = OutputText.Length > 0;
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
