using System.Windows.Input;
using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the digit-sequence search results: the 1-based <see cref="Position"/> of the hit, its
/// surrounding context digits, and a self-contained <see cref="CopyCommand"/> for the position.
/// </summary>
public sealed class EulerNumberOccurrenceItem
{
    public EulerNumberOccurrenceItem(EulerNumberMatch match, Action<string> copy)
    {
        Position = match.Position;
        Before = match.Before.Length > 0 ? $"…{match.Before}" : string.Empty;
        Match = match.Match;
        After = match.After.Length > 0 ? $"{match.After}…" : string.Empty;
        CopyCommand = new RelayCommand(() => copy(Position.ToString()));
    }

    public int Position { get; }

    public string Before { get; }

    public string Match { get; }

    public string After { get; }

    /// <summary>The full context line, used by copy/share.</summary>
    public string ContextText => $"{Before}{Match}{After}";

    public ICommand CopyCommand { get; }
}
