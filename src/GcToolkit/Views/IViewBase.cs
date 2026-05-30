namespace GcToolkit.Views;

/// <summary>
/// Non-generic, type-erased surface of <see cref="ViewBase{TViewModel}"/>.
/// </summary>
/// <remarks>
/// Useful when a consumer (for example a <c>DataTemplate</c>) needs to reference a view without
/// taking a dependency on the concrete <see cref="ViewBase{TViewModel}"/> generic type, or when a
/// view needs to be referenced from a test without knowing its view model type at compile time.
/// </remarks>
public interface IViewBase
{
    /// <summary>
    /// Gets the view model associated with the view, or <see langword="null"/> if it has not been
    /// resolved yet.
    /// </summary>
    object? ViewModel { get; }
}
