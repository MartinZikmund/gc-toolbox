using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace GcToolkit.Controls;

/// <summary>
/// Minimal left-to-right wrapping panel for the signal-flag hoist: flags have varying widths
/// (square letter flags vs. long pennants), which the WinUI uniform-grid layouts cannot wrap.
/// </summary>
public sealed partial class SignalFlagsWrapPanel : Panel
{
    public static readonly DependencyProperty ItemSpacingProperty = DependencyProperty.Register(
        nameof(ItemSpacing), typeof(double), typeof(SignalFlagsWrapPanel),
        new PropertyMetadata(8.0, static (sender, _) => ((SignalFlagsWrapPanel)sender).InvalidateMeasure()));

    public static readonly DependencyProperty LineSpacingProperty = DependencyProperty.Register(
        nameof(LineSpacing), typeof(double), typeof(SignalFlagsWrapPanel),
        new PropertyMetadata(8.0, static (sender, _) => ((SignalFlagsWrapPanel)sender).InvalidateMeasure()));

    public double ItemSpacing
    {
        get => (double)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    public double LineSpacing
    {
        get => (double)GetValue(LineSpacingProperty);
        set => SetValue(LineSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var lineWidthLimit = double.IsInfinity(availableSize.Width) ? double.MaxValue : availableSize.Width;
        double x = 0, y = 0, lineHeight = 0, panelWidth = 0;

        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = child.DesiredSize;

            if (x > 0 && x + size.Width > lineWidthLimit)
            {
                y += lineHeight + LineSpacing;
                x = 0;
                lineHeight = 0;
            }

            x += size.Width + ItemSpacing;
            lineHeight = Math.Max(lineHeight, size.Height);
            panelWidth = Math.Max(panelWidth, x - ItemSpacing);
        }

        return new(panelWidth, Children.Count == 0 ? 0 : y + lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, lineHeight = 0;

        foreach (var child in Children)
        {
            var size = child.DesiredSize;

            if (x > 0 && x + size.Width > finalSize.Width)
            {
                y += lineHeight + LineSpacing;
                x = 0;
                lineHeight = 0;
            }

            child.Arrange(new Rect(x, y, size.Width, size.Height));
            x += size.Width + ItemSpacing;
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        return finalSize;
    }
}
