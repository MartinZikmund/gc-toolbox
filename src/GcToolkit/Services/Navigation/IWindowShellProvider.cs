using GcToolkit.Core.ViewModels;
using GcToolkit.Infrastructure;
using Microsoft.UI.Dispatching;

namespace GcToolkit.Services.Navigation;

public interface IWindowShellProvider
{
    Window Window { get; }

    IWindowShell Shell { get; }

    WindowShellViewModel ViewModel { get; }

    DispatcherQueue DispatcherQueue { get; }

    Frame RootFrame { get; }
}
