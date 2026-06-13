using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the "try all directions" list: the source/target layout names, the remapped
/// <see cref="Text"/>, and a self-contained <see cref="CopyCommand"/> so a candidate can be copied
/// without the row reaching back into the parent ViewModel.
/// </summary>
public sealed class DvorakCandidateItem
{
    public DvorakCandidateItem(string fromName, string toName, string text, Action<string> copy)
    {
        FromName = fromName;
        ToName = toName;
        Text = text;
        CopyCommand = new RelayCommand(() => copy(text));
    }

    public string FromName { get; }

    public string ToName { get; }

    /// <summary>The two layout names joined with an arrow, e.g. "QWERTY → Dvorak (two hands)".</summary>
    public string Pairing => $"{FromName} → {ToName}";

    public string Text { get; }

    public ICommand CopyCommand { get; }
}
