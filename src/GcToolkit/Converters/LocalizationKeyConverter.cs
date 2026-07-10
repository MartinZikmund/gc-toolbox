using GcToolkit.Services.Localization;
using Microsoft.UI.Xaml.Data;

namespace GcToolkit.Converters;

/// <summary>
/// Resolves a localization key string (e.g. <c>ColourConversionRgb</c>) to its localized value.
/// Lets data-templated items carry a key and have it localized at bind time, where the
/// <c>{markup:Localize}</c> extension (which needs a literal key) can't reach.
/// </summary>
public sealed class LocalizationKeyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is string key && key.Length > 0 ? Localizer.Instance.GetString(key) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
