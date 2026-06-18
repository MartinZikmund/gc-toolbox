using GcToolkit.Core.Text;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One bindable row of the frequency histogram: the character/sequence, its count, its share, and a
/// 0..1 fill factor (relative to the most-common entry) the View turns into a bar width. Optionally
/// carries the expected reference percentage so the View can show an "expected" marker for letters.
/// </summary>
public sealed class FrequencyBarItem
{
    /// <summary>Track width (device pixels) the bars are scaled within; the longest bar fills it.</summary>
    public const double TrackWidth = 220.0;

    public FrequencyBarItem(FrequencyEntry entry, double fillFactor, double? expectedPercentage)
    {
        Display = entry.Display;
        Count = entry.Count;
        Percentage = entry.Percentage;
        FillFactor = fillFactor;
        BarWidth = Math.Max(2.0, fillFactor * TrackWidth);
        ExpectedPercentage = expectedPercentage;
        PercentageText = $"{entry.Percentage:0.0}%";
        HasExpected = expectedPercentage is > 0;
        ExpectedText = HasExpected ? $"≈ {expectedPercentage:0.0}%" : string.Empty;
    }

    public string Display { get; }

    public int Count { get; }

    public double Percentage { get; }

    /// <summary>0..1 share of the longest bar.</summary>
    public double FillFactor { get; }

    /// <summary>The bar's rendered width in device pixels (<see cref="FillFactor"/> × <see cref="TrackWidth"/>).</summary>
    public double BarWidth { get; }

    public double? ExpectedPercentage { get; }

    public bool HasExpected { get; }

    public string PercentageText { get; }

    public string ExpectedText { get; }

    /// <summary>Accessible label combining the character, count and percentage.</summary>
    public string AutomationName => $"{Display}: {Count} ({PercentageText})";
}
