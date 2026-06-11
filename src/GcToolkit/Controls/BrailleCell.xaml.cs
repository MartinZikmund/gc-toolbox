using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GcToolkit.Controls;

/// <summary>
/// Renders one braille cell as a 2×3 grid of dots — a solid circle for each raised dot, a hollow outline
/// for every empty position — so it's clear which dots are filled and which aren't. Driven by the six
/// boolean <c>DotN</c> dependency properties (1-2-3 down the left column, 4-5-6 down the right).
/// </summary>
public sealed partial class BrailleCell : UserControl
{
    public BrailleCell() => InitializeComponent();

    public static readonly DependencyProperty Dot1Property = DependencyProperty.Register(
        nameof(Dot1), typeof(bool), typeof(BrailleCell), new PropertyMetadata(false, OnDotChanged));

    public static readonly DependencyProperty Dot2Property = DependencyProperty.Register(
        nameof(Dot2), typeof(bool), typeof(BrailleCell), new PropertyMetadata(false, OnDotChanged));

    public static readonly DependencyProperty Dot3Property = DependencyProperty.Register(
        nameof(Dot3), typeof(bool), typeof(BrailleCell), new PropertyMetadata(false, OnDotChanged));

    public static readonly DependencyProperty Dot4Property = DependencyProperty.Register(
        nameof(Dot4), typeof(bool), typeof(BrailleCell), new PropertyMetadata(false, OnDotChanged));

    public static readonly DependencyProperty Dot5Property = DependencyProperty.Register(
        nameof(Dot5), typeof(bool), typeof(BrailleCell), new PropertyMetadata(false, OnDotChanged));

    public static readonly DependencyProperty Dot6Property = DependencyProperty.Register(
        nameof(Dot6), typeof(bool), typeof(BrailleCell), new PropertyMetadata(false, OnDotChanged));

    public bool Dot1 { get => (bool)GetValue(Dot1Property); set => SetValue(Dot1Property, value); }

    public bool Dot2 { get => (bool)GetValue(Dot2Property); set => SetValue(Dot2Property, value); }

    public bool Dot3 { get => (bool)GetValue(Dot3Property); set => SetValue(Dot3Property, value); }

    public bool Dot4 { get => (bool)GetValue(Dot4Property); set => SetValue(Dot4Property, value); }

    public bool Dot5 { get => (bool)GetValue(Dot5Property); set => SetValue(Dot5Property, value); }

    public bool Dot6 { get => (bool)GetValue(Dot6Property); set => SetValue(Dot6Property, value); }

    private static void OnDotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((BrailleCell)d).Refresh();

    private void Refresh()
    {
        if (Fill1 is null)
        {
            return; // properties set before the template is realized; the XAML defaults already match.
        }

        Fill1.Visibility = Dot1 ? Visibility.Visible : Visibility.Collapsed;
        Fill2.Visibility = Dot2 ? Visibility.Visible : Visibility.Collapsed;
        Fill3.Visibility = Dot3 ? Visibility.Visible : Visibility.Collapsed;
        Fill4.Visibility = Dot4 ? Visibility.Visible : Visibility.Collapsed;
        Fill5.Visibility = Dot5 ? Visibility.Visible : Visibility.Collapsed;
        Fill6.Visibility = Dot6 ? Visibility.Visible : Visibility.Collapsed;
    }
}
