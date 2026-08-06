using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GcToolkit.Converters;

/// <summary>
/// Turns an <c>#AARRGGBB</c> (or <c>#RRGGBB</c>) hex string into a <see cref="SolidColorBrush"/> for the
/// colour-conversion preview swatch. Unparseable input falls back to transparent rather than throwing.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && TryParse(hex, out var color))
        {
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    private static bool TryParse(string hex, out Color color)
    {
        color = Colors.Transparent;
        var s = hex.AsSpan().Trim();
        if (s.StartsWith("#"))
        {
            s = s[1..];
        }

        byte a = 0xFF, r, g, b;
        if (s.Length == 8)
        {
            if (!TryHex(s[..2], out a) || !TryHex(s.Slice(2, 2), out r) || !TryHex(s.Slice(4, 2), out g) || !TryHex(s.Slice(6, 2), out b))
            {
                return false;
            }
        }
        else if (s.Length == 6)
        {
            if (!TryHex(s[..2], out r) || !TryHex(s.Slice(2, 2), out g) || !TryHex(s.Slice(4, 2), out b))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        color = Color.FromArgb(a, r, g, b);
        return true;
    }

    private static bool TryHex(ReadOnlySpan<char> span, out byte value)
        => byte.TryParse(span, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value);
}
