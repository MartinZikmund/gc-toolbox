using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace GcToolkit.Helpers;

/// <summary>
/// Resolves a logical icon key (a tool <c>Id</c> or a category member name) to its outline icon from
/// <c>Resources/IconGeometries.xaml</c>, where each key is a <see cref="DataTemplate"/> named
/// <c>ToolIcon_&lt;key&gt;</c>. A missing key simply renders nothing — icons are never build-validated
/// and must never break the app.
/// </summary>
public static class ToolIcons
{
    private const double NavIconScale = 16d / 24d;

    /// <summary>Builds a fresh outline icon for <paramref name="key"/>, or <see langword="null"/> if there is none.</summary>
    /// <remarks>
    /// Each call re-parses via <see cref="DataTemplate.LoadContent"/> rather than handing out a shared
    /// object. A <see cref="Geometry"/> lifted from a resource and assigned to a second
    /// <c>PathIcon.Data</c> throws <see cref="ArgumentException"/> — it is already owned — and the same
    /// tool's icon legitimately appears in the pane, on a card and on its tool page at once.
    /// </remarks>
    public static PathIcon? CreateIcon(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        return Application.Current.Resources.TryGetValue($"ToolIcon_{key}", out var value)
            && value is DataTemplate template
                ? template.LoadContent() as PathIcon
                : null;
    }

    /// <summary>
    /// A navigation-pane icon for <paramref name="key"/>. The paths are authored on a 24 DIP box and
    /// the pane wants 16; a <c>PathIcon</c> does not scale its data to fit, so the difference is taken
    /// with a centred transform rather than by rewriting 52 paths.
    /// </summary>
    public static IconElement? NavIcon(string? key)
    {
        if (CreateIcon(key) is not { } icon)
        {
            return null;
        }

        icon.RenderTransformOrigin = new Point(0.5, 0.5);
        icon.RenderTransform = new ScaleTransform { ScaleX = NavIconScale, ScaleY = NavIconScale };
        return icon;
    }
}
