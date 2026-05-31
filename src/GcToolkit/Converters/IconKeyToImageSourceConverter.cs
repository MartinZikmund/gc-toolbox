using GcToolkit.Helpers;
using Microsoft.UI.Xaml.Data;

namespace GcToolkit.Converters;

/// <summary>
/// Binds a tool/category icon key to a bitmap <see cref="Microsoft.UI.Xaml.Media.ImageSource"/> via
/// the convention path (see <see cref="ToolIcons"/>). Pass <c>ConverterParameter="Category"</c> for
/// category assets; the default is a tool asset.
/// </summary>
public sealed class IconKeyToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        var kind = string.Equals(parameter as string, "Category", StringComparison.OrdinalIgnoreCase)
            ? ToolIconKind.Category
            : ToolIconKind.Tool;

        return ToolIcons.For(value as string, kind);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
