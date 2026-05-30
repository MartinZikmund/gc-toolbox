namespace GcToolkit.Services.Dialogs;

public interface IDialogService
{
    Task<ContentDialogResult> ShowAsync(string title, string content);

    Task<ContentDialogResult> ShowAsync(ContentDialog contentDialog);
}
