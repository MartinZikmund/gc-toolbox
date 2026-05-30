namespace GcToolkit.Services.Dialogs;

public sealed class DialogCoordinator : IDialogCoordinator
{
    private readonly Queue<QueuedDialog> _dialogQueue = new();
    private bool _isShowingDialog;

    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        var queuedDialog = new QueuedDialog(dialog);
        _dialogQueue.Enqueue(queuedDialog);
        await ProcessQueueAsync();
        return await queuedDialog.CompletionSource.Task;
    }

    private async Task ProcessQueueAsync()
    {
        if (_isShowingDialog)
        {
            return;
        }

        _isShowingDialog = true;
        while (_dialogQueue.Count > 0)
        {
            var queued = _dialogQueue.Dequeue();
            try
            {
                var result = await queued.Dialog.ShowAsync();
                queued.CompletionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                queued.CompletionSource.SetException(ex);
            }
        }

        _isShowingDialog = false;
    }

    private sealed class QueuedDialog
    {
        public QueuedDialog(ContentDialog dialog)
        {
            Dialog = dialog;
        }

        public TaskCompletionSource<ContentDialogResult> CompletionSource { get; } = new();

        public ContentDialog Dialog { get; }
    }
}
