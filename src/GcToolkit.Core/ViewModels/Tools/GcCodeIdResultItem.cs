using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of a batch conversion: the original <see cref="Input"/> token, its converted
/// <see cref="Output"/>, an <see cref="IsValid"/> flag for tokens that could not be converted, and a
/// self-contained <see cref="CopyCommand"/> so a single result can be copied without the row reaching
/// back into the parent ViewModel.
/// </summary>
public sealed class GcCodeIdResultItem
{
    public GcCodeIdResultItem(string input, string output, bool isValid, Action<string> copy)
    {
        Input = input;
        Output = output;
        IsValid = isValid;
        CopyCommand = new RelayCommand(() => copy(output), () => isValid);
    }

    public string Input { get; }

    public string Output { get; }

    public bool IsValid { get; }

    public ICommand CopyCommand { get; }
}
