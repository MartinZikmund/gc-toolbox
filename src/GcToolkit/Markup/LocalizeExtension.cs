using GcToolkit.Services.Localization;
using Microsoft.UI.Xaml.Markup;

namespace GcToolkit.Markup;

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue()
    {
        if (string.IsNullOrEmpty(Key))
        {
            return string.Empty;
        }

        return Localizer.Instance.GetString(Key);
    }
}
