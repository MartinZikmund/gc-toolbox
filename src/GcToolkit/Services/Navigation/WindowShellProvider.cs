using System.Diagnostics.CodeAnalysis;
using GcToolkit.Core.ViewModels;
using GcToolkit.Infrastructure;
using Microsoft.UI.Dispatching;

namespace GcToolkit.Services.Navigation;

internal sealed class WindowShellProvider : IWindowShellProvider, IXamlRootProvider
{
    private WindowShell? _shell;
    private Window? _window;
    private DispatcherQueue? _dispatcherQueue;
    private XamlRoot? _xamlRoot;

    public void SetShell(WindowShell shell, Window window)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _window = window;
        _dispatcherQueue = shell.DispatcherQueue;
    }

    public IWindowShell Shell { get { EnsureInitialized(); return _shell; } }

    public Window Window { get { EnsureInitialized(); return _window; } }

    public WindowShellViewModel ViewModel { get { EnsureInitialized(); return _shell.ViewModel; } }

    public DispatcherQueue DispatcherQueue { get { EnsureInitialized(); return _dispatcherQueue; } }

    public Frame RootFrame { get { EnsureInitialized(); return _shell.RootFrame; } }

    public IServiceProvider ServiceProvider { get { EnsureInitialized(); return _shell.ServiceProvider; } }

    public XamlRoot XamlRoot => _xamlRoot
        ?? throw new InvalidOperationException("XamlRoot is not available yet. It is set during WindowShell's Loading event.");

    internal void SetXamlRoot(XamlRoot xamlRoot) => _xamlRoot = xamlRoot ?? throw new ArgumentNullException(nameof(xamlRoot));

    [MemberNotNull(nameof(_shell))]
    [MemberNotNull(nameof(_window))]
    [MemberNotNull(nameof(_dispatcherQueue))]
    private void EnsureInitialized()
    {
        if (_shell is null || _dispatcherQueue is null || _window is null)
        {
            throw new InvalidOperationException("WindowShellProvider was not initialized. Ensure WindowShell has been created.");
        }
    }
}
