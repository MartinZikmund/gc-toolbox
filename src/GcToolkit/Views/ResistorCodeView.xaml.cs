using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class ResistorCodeViewBase : ViewBase<ResistorCodeViewModel> { }

public sealed partial class ResistorCodeView : ResistorCodeViewBase
{
    public ResistorCodeView()
    {
        this.InitializeComponent();
    }

    /// <summary>Turns a <c>#RRGGBB</c> swatch string into a brush for the colour chips (used from x:Bind in templates).</summary>
    public static SolidColorBrush Brush(string hex)
    {
        if (TryParseHex(hex, out var color))
        {
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    private static bool TryParseHex(string? hex, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length != 7)
        {
            return false;
        }

        if (byte.TryParse(hex.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
            && byte.TryParse(hex.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            && byte.TryParse(hex.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            color = Color.FromArgb(255, r, g, b);
            return true;
        }

        return false;
    }
}
