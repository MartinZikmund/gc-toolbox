using System.Collections.Generic;
using GcToolkit.Core.Alphabets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace GcToolkit.Controls;

/// <summary>
/// Draws one segment-display character from a <see cref="SegmentGlyph"/>: every segment of the family is
/// rendered as a theme-aware polygon, lit ones in the accent ink and unlit ones as a faint "ghost" so the
/// off segments stay visible (like a real LCD). Plain XAML shapes inside a <see cref="Viewbox"/> keep the
/// glyph crisp at any size on every head. The segment geometry lives here in the head; the lit/unlit data
/// comes from the pure Core codec.
/// </summary>
public sealed partial class SegmentDisplayPresenter : Grid
{
    // Logical canvas; segment polygons below are authored in these units.
    private const double CanvasWidth = 120;
    private const double CanvasHeight = 200;

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(SegmentGlyph),
        typeof(SegmentDisplayPresenter),
        new PropertyMetadata(null, static (sender, _) => ((SegmentDisplayPresenter)sender).Rebuild()));

    public SegmentGlyph? Glyph
    {
        get => (SegmentGlyph?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();

        if (Glyph is not { } glyph)
        {
            return;
        }

        var geometry = SegmentGeometry.For(glyph.DisplayType);
        var litBrush = ResolveBrush("TextFillColorPrimaryBrush", FallbackLit);
        var offBrush = ResolveBrush("SmokeFillColorDefaultBrush", FallbackOff);

        Canvas canvas = new() { Width = CanvasWidth, Height = CanvasHeight };

        // A lit lookup by label so order/extra labels never throw.
        var lit = new HashSet<string>();
        foreach (var segment in glyph.LitSegments)
        {
            if (segment.Lit)
            {
                lit.Add(segment.Label);
            }
        }

        foreach (var (label, points) in geometry)
        {
            Polygon polygon = new()
            {
                Fill = lit.Contains(label) ? litBrush : offBrush,
            };

            foreach (var (x, y) in points)
            {
                polygon.Points.Add(new(x, y));
            }

            canvas.Children.Add(polygon);
        }

        Children.Add(new Viewbox { Child = canvas, Stretch = Stretch.Uniform });
    }

    private static Brush ResolveBrush(string themeKey, Brush fallback)
        => Application.Current.Resources.TryGetValue(themeKey, out var value) && value is Brush brush
            ? brush
            : fallback;

    private static readonly SolidColorBrush FallbackLit =
        new(Windows.UI.Color.FromArgb(0xFF, 0xE0, 0x4F, 0x2A));

    private static readonly SolidColorBrush FallbackOff =
        new(Windows.UI.Color.FromArgb(0x22, 0x80, 0x80, 0x80));
}
