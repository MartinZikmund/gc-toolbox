using System.Globalization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One ranked key-length candidate in the assisted solver: the <see cref="Length"/> tried and the
/// averaged per-column Index of Coincidence it scored (higher, nearer the language's IC, is better).
/// </summary>
public sealed class VigenereKeyLengthItem
{
    public VigenereKeyLengthItem(int length, double indexOfCoincidence)
    {
        Length = length.ToString(CultureInfo.CurrentCulture);
        Score = string.Format(CultureInfo.CurrentCulture, "IC {0:0.0000}", indexOfCoincidence);
    }

    public string Length { get; }

    public string Score { get; }

    /// <summary>A ListView item with no explicit automation name announces its ToString(), so make it the row.</summary>
    public override string ToString() => $"{Length}: {Score}";
}
