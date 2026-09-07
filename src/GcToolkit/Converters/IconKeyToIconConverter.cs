using GcToolkit.Helpers;
using Microsoft.UI.Xaml.Data;

namespace GcToolkit.Converters;

/// <summary>
/// Binds a tool/category icon key to a freshly built outline <c>PathIcon</c> (see
/// <see cref="ToolIcons.CreateIcon"/>), for hosting in a <c>ContentPresenter</c>. Vector rather than
/// a bitmap so the icon takes its colour from <c>Foreground</c> and follows light, dark and High Contrast.
/// </summary>
public sealed class IconKeyToIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
        => ToolIcons.CreateIcon(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
