using GcToolkit.Services.Localization;
using Microsoft.UI.Xaml.Data;

namespace GcToolkit.Converters;

public sealed class EnumLocalizationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Enum enumValue)
        {
            var enumType = enumValue.GetType();
            var enumName = Enum.GetName(enumType, enumValue);
            var key = $"{enumType.Name}_{enumName}";
            return Localizer.Instance.GetString(key);
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
