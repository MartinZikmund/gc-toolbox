using CommunityToolkit.WinUI.Converters;
using Microsoft.UI.Xaml.Data;

namespace GcToolkit.Converters;

public sealed class NullToVisibilityConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var shouldShow = Invert ? value is null : value is not null;
        return shouldShow ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
