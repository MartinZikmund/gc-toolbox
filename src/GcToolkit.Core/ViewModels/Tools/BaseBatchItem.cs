using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One token of a batch conversion: the original <see cref="Input"/>, its converted <see cref="Value"/>
/// (empty when the token wasn't a valid number in the source base) and a self-contained
/// <see cref="CopyCommand"/>. Invalid tokens stay in the list flagged rather than being dropped
/// silently, so the solver can see which of their numbers was rejected and why.
/// </summary>
public sealed class BaseBatchItem
{
    public BaseBatchItem(string input, string value, bool isValid, string invalidLabel, Action<string> copy)
    {
        Input = input;
        Value = value;
        IsValid = isValid;
        InvalidLabel = invalidLabel;
        Display = isValid ? value : invalidLabel;
        CopyCommand = new RelayCommand(() => copy(value), () => isValid);
    }

    public string Input { get; }

    public string Value { get; }

    public bool IsValid { get; }

    /// <summary>The localized "skipped — not valid in base N" note shown in place of a result.</summary>
    public string InvalidLabel { get; }

    /// <summary>What the row shows in its value column: the result, or the invalid note.</summary>
    public string Display { get; }

    public ICommand CopyCommand { get; }

    /// <summary>A ListView item with no explicit automation name announces its ToString(), so make it the row.</summary>
    public override string ToString() => $"{Input}: {Display}";
}
