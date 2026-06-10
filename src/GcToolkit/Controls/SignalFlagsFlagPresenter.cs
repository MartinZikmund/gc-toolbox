using GcToolkit.Core.Alphabets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace GcToolkit.Controls;

/// <summary>
/// Draws one ICS signal flag from its <see cref="SignalFlagDescriptor"/> vector description: plain
/// XAML shapes inside a <see cref="Viewbox"/>, so flags stay crisp at any size on every head. A thin
/// neutral outline keeps white flag fields visible in both themes.
/// </summary>
public sealed partial class SignalFlagsFlagPresenter : Grid
{
    /// <summary>Logical canvas height — descriptor coordinates are flag-relative (height = 1).</summary>
    private const double UnitScale = 100;

    private static readonly SolidColorBrush _whiteBrush = new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush _blueBrush = new(Color.FromArgb(0xFF, 0x00, 0x38, 0xA8));
    private static readonly SolidColorBrush _redBrush = new(Color.FromArgb(0xFF, 0xCE, 0x11, 0x26));
    private static readonly SolidColorBrush _yellowBrush = new(Color.FromArgb(0xFF, 0xFC, 0xD1, 0x16));
    private static readonly SolidColorBrush _blackBrush = new(Color.FromArgb(0xFF, 0x1B, 0x1B, 0x1B));
    private static readonly SolidColorBrush _outlineBrush = new(Color.FromArgb(0x80, 0x80, 0x80, 0x80));

    public static readonly DependencyProperty FlagProperty = DependencyProperty.Register(
        nameof(Flag),
        typeof(SignalFlagDescriptor),
        typeof(SignalFlagsFlagPresenter),
        new PropertyMetadata(null, static (sender, _) => ((SignalFlagsFlagPresenter)sender).Rebuild()));

    public SignalFlagDescriptor? Flag
    {
        get => (SignalFlagDescriptor?)GetValue(FlagProperty);
        set => SetValue(FlagProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();

        if (Flag is not { } flag)
        {
            return;
        }

        Canvas canvas = new()
        {
            Width = flag.AspectRatio * UnitScale,
            Height = UnitScale,
        };

        foreach (var shape in flag.Shapes)
        {
            switch (shape)
            {
                case SignalFlagPolygon polygon:
                    canvas.Children.Add(CreatePolygon(polygon.Points, ToBrush(polygon.Color)));
                    break;

                case SignalFlagCircle circle:
                    Ellipse ellipse = new()
                    {
                        Width = circle.Radius * 2 * UnitScale,
                        Height = circle.Radius * 2 * UnitScale,
                        Fill = ToBrush(circle.Color),
                    };
                    Canvas.SetLeft(ellipse, (circle.CenterX - circle.Radius) * UnitScale);
                    Canvas.SetTop(ellipse, (circle.CenterY - circle.Radius) * UnitScale);
                    canvas.Children.Add(ellipse);
                    break;
            }
        }

        // A subtle silhouette stroke so white fields read as a flag on light backgrounds.
        var outline = CreatePolygon(flag.Outline, fill: null);
        outline.Stroke = _outlineBrush;
        outline.StrokeThickness = 1.5;
        canvas.Children.Add(outline);

        Children.Add(new Viewbox
        {
            Child = canvas,
            Stretch = Stretch.Uniform,
        });
    }

    private static Polygon CreatePolygon(IReadOnlyList<SignalFlagPoint> points, Brush? fill)
    {
        Polygon polygon = new() { Fill = fill };
        foreach (var point in points)
        {
            polygon.Points.Add(new(point.X * UnitScale, point.Y * UnitScale));
        }

        return polygon;
    }

    private static SolidColorBrush ToBrush(SignalFlagColor color) => color switch
    {
        SignalFlagColor.Blue => _blueBrush,
        SignalFlagColor.Red => _redBrush,
        SignalFlagColor.Yellow => _yellowBrush,
        SignalFlagColor.Black => _blackBrush,
        _ => _whiteBrush,
    };
}
