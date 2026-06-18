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
/// Keyboard cipher (issue #216): substitutes the 26 letters by their position in a keyboard layout's
/// key order. Transforms live as the user types, in either direction, across QWERTY/AZERTY/QWERTZ/
/// Dvorak/Colemak and a validated custom layout. Beyond cachesleuth.com parity it adds the two extra
/// layouts, a custom-layout field, a one-tap direction swap, a live A-Z &lt;-&gt; layout reference
/// table, batch multi-line processing, and copy/share — all fully offline.
/// </summary>
[Tool("KeyboardCipher", ToolCategory.Alphabets,
      Introduced = "2026-06-18", Updated = "2026-06-18",
      Keywords = ["keyboard", "qwerty", "azerty", "qwertz", "dvorak", "layout", "klávesnice", "rozložení"])]
public sealed partial class KeyboardCipherViewModel : ToolViewModelBase
{
    private readonly KeyboardCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    /// <summary>Layout selector order — built-ins first, then Custom — matching <see cref="LayoutForIndex"/>.</summary>
    private static readonly KeyboardLayout[] _layoutOrder =
    [
        KeyboardLayout.Qwerty,
        KeyboardLayout.Azerty,
        KeyboardLayout.Qwertz,
        KeyboardLayout.Dvorak,
        KeyboardLayout.Colemak,
        KeyboardLayout.Custom,
    ];

    private bool _suppressConvert;

    public KeyboardCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("KeyboardCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        RebuildMapping();
    }

    /// <summary>0 = Encrypt (plaintext -> keyboard), 1 = Decrypt (keyboard -> plaintext).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Index into <see cref="_layoutOrder"/>; the last entry is the custom layout.</summary>
    [ObservableProperty]
    public partial int LayoutIndex { get; set; }

    /// <summary>The user's custom key order — only used (and validated) when <see cref="IsCustomLayout"/> is set.</summary>
    [ObservableProperty]
    public partial string CustomLayout { get; set; } = KeyboardCipher.GetLayoutOrder(KeyboardLayout.Qwerty);

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when a non-blocking notice should show (e.g. an invalid custom layout).</summary>
    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the custom-layout field should be shown.</summary>
    public bool IsCustomLayout => SelectedLayout == KeyboardLayout.Custom;

    /// <summary>The A-Z &lt;-&gt; layout reference rows for the currently selected (valid) layout.</summary>
    public ObservableCollection<KeyboardMappingRow> Mapping { get; } = [];

    private KeyboardLayout SelectedLayout => LayoutForIndex(LayoutIndex);

    private bool IsEncrypt => DirectionIndex != 1;

    private static KeyboardLayout LayoutForIndex(int index)
        => index >= 0 && index < _layoutOrder.Length ? _layoutOrder[index] : KeyboardLayout.Qwerty;

    partial void OnInputTextChanged(string value) => Convert();

    partial void OnCustomLayoutChanged(string value)
    {
        RebuildMapping();
        Convert();
    }

    partial void OnLayoutIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomLayout));
        RebuildMapping();
        Convert();
    }

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore re-entrancy and the transient -1 a RadioButtons control can emit.
        if (_suppressConvert || value is not (0 or 1))
        {
            return;
        }

        // Switching direction carries the previous result back into the input so a round-trip is one tap.
        _suppressConvert = true;
        InputText = OutputText;
        _suppressConvert = false;
        Convert();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Resolves the active key order, or <see langword="null"/> when the custom layout is invalid.</summary>
    private string? ResolveLayoutOrder()
    {
        if (SelectedLayout == KeyboardLayout.Custom)
        {
            return KeyboardCipher.IsValidLayout(CustomLayout) ? CustomLayout : null;
        }

        return KeyboardCipher.GetLayoutOrder(SelectedLayout);
    }

    private void Convert()
    {
        if (_suppressConvert)
        {
            return;
        }

        var order = ResolveLayoutOrder();
        if (order is null)
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = true;
            WarningMessage = _localizer["KeyboardInvalidLayoutNotice"].Value;
            return;
        }

        HasWarning = false;
        WarningMessage = string.Empty;

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        var direction = IsEncrypt ? KeyboardCipherDirection.Encrypt : KeyboardCipherDirection.Decrypt;

        // Transform each line independently so batch (one puzzle per line) keeps its line structure.
        var lines = InputText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            // Preserve any trailing '\r' so CRLF input round-trips unchanged.
            var hasCr = lines[i].EndsWith('\r');
            var body = hasCr ? lines[i][..^1] : lines[i];
            lines[i] = _cipher.Transform(body, order, direction) + (hasCr ? "\r" : string.Empty);
        }

        OutputText = string.Join('\n', lines);
        HasOutput = OutputText.Length > 0;
    }

    private void RebuildMapping()
    {
        Mapping.Clear();
        var order = ResolveLayoutOrder();
        if (order is null)
        {
            return;
        }

        foreach (var entry in _cipher.GetMapping(order))
        {
            Mapping.Add(new KeyboardMappingRow(entry.Letter.ToString(), entry.Mapped.ToString()));
        }
    }

    [RelayCommand]
    private void Swap()
    {
        // Flip direction and carry the current output into the input for an instant inverse.
        _suppressConvert = true;
        (InputText, DirectionIndex) = (OutputText, IsEncrypt ? 1 : 0);
        _suppressConvert = false;
        Convert();
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

/// <summary>One reference-table row: a plain letter and the layout letter it maps to.</summary>
public sealed record KeyboardMappingRow(string Letter, string Mapped);
