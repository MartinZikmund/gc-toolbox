namespace GcToolkit.Core.Services.Dialogs;

public interface IConfirmationDialogService
{
    Task<ConfirmationResult> ShowAsync(string title, string text);
}
