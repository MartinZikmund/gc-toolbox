using GcToolkit.Core.Alphabets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace GcToolkit.Controls;

/// <summary>
/// Draws one Hexahue glyph as a vector 2×3 grid of coloured cells from its <see cref="HexahueGlyph"/>.
/// Plain XAML rectangles in a fixed 2-wide, 3-tall grid, so the glyph stays crisp at any size on every
/// head and renders identically in light and dark themes. A thin neutral outline keeps white cells visible.
/// </summary>
public sealed partial class HexahueGlyphPresenter : Grid
{
    private static readonly SolidColorBrush _red = new(Color.FromArgb(0xFF, 0xE0, 0x1B, 0x24));
    private static readonly SolidColorBrush _green = new(Color.FromArgb(0xFF, 0x1E, 0xA8, 0x4C));
    private static readonly SolidColorBrush _blue = new(Color.FromArgb(0xFF, 0x1E, 0x5B, 0xD6));
    private static readonly SolidColorBrush _yellow = new(Color.FromArgb(0xFF, 0xF5, 0xC2, 0x18));
    private static readonly SolidColorBrush _cyan = new(Color.FromArgb(0xFF, 0x14, 0xC4, 0xD9));
    private static readonly SolidColorBrush _magenta = new(Color.FromArgb(0xFF, 0xD6, 0x1F, 0xC4));
    private static readonly SolidColorBrush _purple = new(Color.FromArgb(0xFF, 0x8E, 0x24, 0xC9));
    private static readonly SolidColorBrush _white = new(Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA));
    private static readonly SolidColorBrush _grey = new(Color.FromArgb(0xFF, 0x80, 0x80, 0x80));
    private static readonly SolidColorBrush _black = new(Color.FromArgb(0xFF, 0x12, 0x12, 0x12));
    private static readonly SolidColorBrush _outline = new(Color.FromArgb(0x66, 0x80, 0x80, 0x80));

    public HexahueGlyphPresenter()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
    }

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(HexahueGlyph),
        typeof(HexahueGlyphPresenter),
        new PropertyMetadata(null, static (sender, _) => ((HexahueGlyphPresenter)sender).Rebuild()));

    /// <summary>When set, the sixth hue (magenta) is drawn as purple — the purple-convention variant.</summary>
    public static readonly DependencyProperty UsePurpleProperty = DependencyProperty.Register(
        nameof(UsePurple),
        typeof(bool),
        typeof(HexahueGlyphPresenter),
        new PropertyMetadata(false, static (sender, _) => ((HexahueGlyphPresenter)sender).Rebuild()));

    public HexahueGlyph? Glyph
    {
        get => (HexahueGlyph?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public bool UsePurple
    {
        get => (bool)GetValue(UsePurpleProperty);
        set => SetValue(UsePurpleProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();

        if (Glyph is not { } glyph)
        {
            return;
        }

        // Reading order: TL, TR, ML, MR, BL, BR -> (column, row) on a 2-wide, 3-tall grid.
        AddCell(glyph.TopLeft, 0, 0);
        AddCell(glyph.TopRight, 1, 0);
        AddCell(glyph.MiddleLeft, 0, 1);
        AddCell(glyph.MiddleRight, 1, 1);
        AddCell(glyph.BottomLeft, 0, 2);
        AddCell(glyph.BottomRight, 1, 2);
    }

    private void AddCell(HexahueColor color, int column, int row)
    {
        Rectangle cell = new()
        {
            Fill = ToBrush(color),
            Stroke = _outline,
            StrokeThickness = 0.5,
        };
        SetColumn(cell, column);
        SetRow(cell, row);
        Children.Add(cell);
    }

    private SolidColorBrush ToBrush(HexahueColor color) => color switch
    {
        HexahueColor.Red => _red,
        HexahueColor.Green => _green,
        HexahueColor.Blue => _blue,
        HexahueColor.Yellow => _yellow,
        HexahueColor.Cyan => _cyan,
        HexahueColor.Magenta => UsePurple ? _purple : _magenta,
        HexahueColor.White => _white,
        HexahueColor.Grey => _grey,
        HexahueColor.Black => _black,
        _ => _white,
    };
}
