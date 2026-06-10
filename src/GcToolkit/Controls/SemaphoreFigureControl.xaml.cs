using GcToolkit.Core.Alphabets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GcToolkit.Controls;

/// <summary>
/// Renders one flag-semaphore figure as a viewer-facing stick figure with two flags. Set
/// <see cref="Figure"/> to position the arms; the visual scales to the control's size.
/// </summary>
public sealed partial class SemaphoreFigureControl : UserControl
{
    public static readonly DependencyProperty FigureProperty = DependencyProperty.Register(
        nameof(Figure),
        typeof(SemaphoreFigure),
        typeof(SemaphoreFigureControl),
        new PropertyMetadata(null, OnFigureChanged));

    public SemaphoreFigureControl() => InitializeComponent();

    public SemaphoreFigure? Figure
    {
        get => (SemaphoreFigure?)GetValue(FigureProperty);
        set => SetValue(FigureProperty, value);
    }

    private static void OnFigureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SemaphoreFigureControl control && e.NewValue is SemaphoreFigure figure)
        {
            control.LeftArmRotation.Angle = figure.LeftFlagAngle;
            control.RightArmRotation.Angle = figure.RightFlagAngle;
        }
    }
}
