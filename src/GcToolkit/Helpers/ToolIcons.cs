using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GcToolkit.Helpers;

public enum ToolIconKind
{
    Tool,
    Category,
}

/// <summary>
/// Resolves a logical icon key (a tool <c>Id</c> or category member name) to a bitmap by convention:
/// <c>ms-appx:///Assets/Icons/{Tools|Categories}/&lt;key&gt;.png</c> (research R7). A missing asset
/// simply renders nothing — icons are never build-validated and never break the app.
/// </summary>
public static class ToolIcons
{
    public static ImageSource? For(string? key, ToolIconKind kind)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var folder = kind == ToolIconKind.Category ? "Categories" : "Tools";
        return new BitmapImage(new Uri($"ms-appx:///Assets/Icons/{folder}/{key}.png"));
    }
}
