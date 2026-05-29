namespace GcToolkit.Core.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? PageTitle { get; set; }

    public virtual void ViewCreated() { }

    public virtual void ViewLoading() { }

    public virtual void ViewLoaded() { }

    public virtual void ViewUnloaded() { }

    public virtual void OnNavigatedTo(object? parameter) { }

    public virtual void OnNavigatedFrom() { }
}
