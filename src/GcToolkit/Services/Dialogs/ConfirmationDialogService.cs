using GcToolkit.Services.Localization;
using GcToolkit.Services.Navigation;

namespace GcToolkit.Services.Dialogs;

public sealed class ConfirmationDialogService : IConfirmationDialogService
{
    private readonly IDialogCoordinator _dialogCoordinator;
    private readonly IXamlRootProvider _xamlRootProvider;

    public ConfirmationDialogService(IDialogCoordinator dialogCoordinator, IXamlRootProvider xamlRootProvider)
    {
        _dialogCoordinator = dialogCoordinator ?? throw new ArgumentNullException(nameof(dialogCoordinator));
        _xamlRootProvider = xamlRootProvider ?? throw new ArgumentNullException(nameof(xamlRootProvider));
    }

    public async Task<ConfirmationResult> ShowAsync(string title, string text)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = text,
            PrimaryButtonText = Localizer.Instance.GetString("Yes"),
            SecondaryButtonText = Localizer.Instance.GetString("No"),
            DefaultButton = ContentDialogButton.Secondary,
            XamlRoot = _xamlRootProvider.XamlRoot,
        };

        var result = await _dialogCoordinator.ShowAsync(dialog);
        return result == ContentDialogResult.Primary ? ConfirmationResult.Confirmed : ConfirmationResult.Denied;
    }
}
