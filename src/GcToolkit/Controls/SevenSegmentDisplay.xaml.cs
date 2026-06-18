using GcToolkit.Core.Alphabets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GcToolkit.Controls;

/// <summary>
/// Renders a single character as a classic seven-segment calculator digit. The <see cref="Glyph"/>
/// supplies the segment pattern; setting <see cref="Flipped"/> draws the 180°-rotated reading so a
/// number can be shown exactly as an upside-down calculator spells it. Lit segments use the accent
/// colour; unlit segments stay a faint outline so the cell shape always reads.
/// </summary>
public sealed partial class SevenSegmentDisplay : UserControl
{
    public SevenSegmentDisplay() => InitializeComponent();

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(SevenSegmentGlyph), typeof(SevenSegmentDisplay), new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty FlippedProperty = DependencyProperty.Register(
        nameof(Flipped), typeof(bool), typeof(SevenSegmentDisplay), new PropertyMetadata(false, OnVisualChanged));

    /// <summary>The character (and its segment pattern) to display.</summary>
    public SevenSegmentGlyph? Glyph
    {
        get => (SevenSegmentGlyph?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>When <see langword="true"/>, draws the 180°-rotated (upside-down) reading.</summary>
    public bool Flipped
    {
        get => (bool)GetValue(FlippedProperty);
        set => SetValue(FlippedProperty, value);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SevenSegmentDisplay)d).Refresh();

    private void Refresh()
    {
        if (SegA is null)
        {
            return; // template not realized yet
        }

        var segments = Glyph is { } glyph
            ? (Flipped ? glyph.Flipped : glyph.Segments)
            : default;

        var lit = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var dim = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];

        SegA.Fill = segments.A ? lit : dim;
        SegB.Fill = segments.B ? lit : dim;
        SegC.Fill = segments.C ? lit : dim;
        SegD.Fill = segments.D ? lit : dim;
        SegE.Fill = segments.E ? lit : dim;
        SegF.Fill = segments.F ? lit : dim;
        SegG.Fill = segments.G ? lit : dim;
    }
}
