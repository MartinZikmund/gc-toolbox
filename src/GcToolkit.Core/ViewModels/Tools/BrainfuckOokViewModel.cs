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
/// Brainfuck and Ook! tool (issue #39). Runs a program (code → text), generates a program that prints
/// given text (text → code), or transliterates between the three notations (Brainfuck / Ook! /
/// Ook! short). A single notation selector plus a mode selector covers all twelve site operations more
/// cleanly than a long dropdown. Beyond parity it auto-detects the notation of pasted code, feeds an
/// stdin field to the <c>,</c> command, guards against infinite loops, and offers copy/share. All logic
/// lives in the pure <see cref="BrainfuckOok"/> codec (thin-VM convention).
/// </summary>
[Tool("BrainfuckOok", ToolCategory.Alphabets,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["brainfuck", "ook", "esoteric", "interpreter", "code", "ezoterický", "jazyk", "interpret", "kód", "opice"])]
public sealed partial class BrainfuckOokViewModel : ToolViewModelBase
{
    private readonly BrainfuckOok _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public BrainfuckOokViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("BrainfuckOok", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
    }

    /// <summary>0 = Execute (code → text), 1 = Encode (text → code), 2 = Convert (notation → notation).</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    /// <summary>The working notation: 0 = Brainfuck, 1 = Ook!, 2 = Ook! short.</summary>
    [ObservableProperty]
    public partial int NotationIndex { get; set; }

    /// <summary>The target notation for Convert mode: 0 = Brainfuck, 1 = Ook!, 2 = Ook! short.</summary>
    [ObservableProperty]
    public partial int TargetNotationIndex { get; set; } = 1;

    /// <summary>The program or, in Encode mode, the text to print.</summary>
    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The stdin fed to the <c>,</c> command while executing.</summary>
    [ObservableProperty]
    public partial string StdinText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>Set when the run was stopped by the loop/output guard, so the UI can warn.</summary>
    [ObservableProperty]
    public partial bool IsAborted { get; set; }

    /// <summary>A parse/validation message (e.g. unbalanced brackets), or empty.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasStatus { get; set; }

    private bool IsExecuteMode => ModeIndex == 0;
    private bool IsConvertMode => ModeIndex == 2;

    /// <summary>True only in Execute mode, so the stdin field can hide otherwise.</summary>
    public bool ShowStdin => IsExecuteMode;

    /// <summary>True only in Convert mode, so the target-notation selector can hide otherwise.</summary>
    public bool ShowTargetNotation => IsConvertMode;

    private static BrainfuckNotation ToNotation(int index) => index switch
    {
        1 => BrainfuckNotation.Ook,
        2 => BrainfuckNotation.OokShort,
        _ => BrainfuckNotation.Brainfuck,
    };

    partial void OnModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowStdin));
        OnPropertyChanged(nameof(ShowTargetNotation));

        // Auto-detect the pasted notation when switching into a code-consuming mode.
        if (!IsEncodeMode() && !string.IsNullOrWhiteSpace(InputText))
        {
            NotationIndex = FromNotation(_codec.Detect(InputText));
        }

        Convert();
    }

    partial void OnNotationIndexChanged(int value) => Convert();

    partial void OnTargetNotationIndexChanged(int value) => Convert();

    partial void OnInputTextChanged(string value) => Convert();

    partial void OnStdinTextChanged(string value) => Convert();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private bool IsEncodeMode() => ModeIndex == 1;

    private static int FromNotation(BrainfuckNotation notation) => notation switch
    {
        BrainfuckNotation.Ook => 1,
        BrainfuckNotation.OokShort => 2,
        _ => 0,
    };

    private void Convert()
    {
        IsAborted = false;
        StatusMessage = string.Empty;

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasStatus = false;
            return;
        }

        var notation = ToNotation(NotationIndex);

        switch (ModeIndex)
        {
            case 1:
                OutputText = _codec.EncodeText(InputText, notation);
                break;
            case 2:
                try
                {
                    OutputText = _codec.Transliterate(InputText, notation, ToNotation(TargetNotationIndex));
                }
                catch (FormatException ex)
                {
                    // Malformed Ook!/Ook! short input must surface as a status, not crash the app.
                    OutputText = string.Empty;
                    StatusMessage = ex.Message;
                }

                break;
            default:
                var result = _codec.Execute(InputText, StdinText, notation);
                OutputText = result.Output;
                IsAborted = result.Aborted;
                StatusMessage = result.HasError ? result.Error ?? string.Empty : string.Empty;
                break;
        }

        HasOutput = !string.IsNullOrEmpty(OutputText);
        HasStatus = !string.IsNullOrEmpty(StatusMessage);
    }

    [RelayCommand]
    private void Clear()
    {
        InputText = string.Empty;
        StdinText = string.Empty;
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
}
