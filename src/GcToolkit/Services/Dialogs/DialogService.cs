using GcToolkit.Services.Localization;
using GcToolkit.Services.Navigation;

namespace GcToolkit.Services.Dialogs;

public sealed class DialogService : IDialogService
{
    private readonly IDialogCoordinator _dialogCoordinator;
    private readonly IXamlRootProvider _xamlRootProvider;

    public DialogService(IDialogCoordinator dialogCoordinator, IXamlRootProvider xamlRootProvider)
    {
        _dialogCoordinator = dialogCoordinator ?? throw new ArgumentNullException(nameof(dialogCoordinator));
        _xamlRootProvider = xamlRootProvider ?? throw new ArgumentNullException(nameof(xamlRootProvider));
    }

    public async Task<ContentDialogResult> ShowAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = Localizer.Instance.GetString("Ok"),
            XamlRoot = _xamlRootProvider.XamlRoot,
        };
        return await _dialogCoordinator.ShowAsync(dialog);
    }

    public async Task<ContentDialogResult> ShowAsync(ContentDialog contentDialog)
    {
        if (contentDialog.XamlRoot is null)
        {
            contentDialog.XamlRoot = _xamlRootProvider.XamlRoot;
        }

        return await _dialogCoordinator.ShowAsync(contentDialog);
    }
}
