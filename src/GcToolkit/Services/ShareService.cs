using System.Runtime.InteropServices;
using GcToolkit.Services.Navigation;
using Windows.ApplicationModel.DataTransfer;

namespace GcToolkit.Services;

public sealed class ShareService : IShareService
{
    private readonly IWindowShellProvider _windowShellProvider;
    private string? _pendingTitle;
    private string? _pendingUri;

    public ShareService(IWindowShellProvider windowShellProvider)
        => _windowShellProvider = windowShellProvider;

    public Task ShareAsync(string title, string uri)
    {
        _pendingTitle = title;
        _pendingUri = uri;

#if !HAS_UNO
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_windowShellProvider.Window);
        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        var result = interop.GetForWindow(hwnd, _dtmIid);
        var dtm = WinRT.MarshalInterface<DataTransferManager>.FromAbi(result);
        dtm.DataRequested += OnDataRequested;
        try
        {
            interop.ShowShareUIForWindow(hwnd);
        }
        catch (COMException)
        {
            dtm.DataRequested -= OnDataRequested;
        }
#else
		var dtm = DataTransferManager.GetForCurrentView();
		dtm.DataRequested += OnDataRequested;
		try
		{
			DataTransferManager.ShowShareUI();
		}
		catch (COMException)
		{
			dtm.DataRequested -= OnDataRequested;
		}
#endif

        return Task.CompletedTask;
    }

    private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        sender.DataRequested -= OnDataRequested;
        args.Request.Data.Properties.Title = _pendingTitle ?? string.Empty;
        if (_pendingUri is not null)
        {
            args.Request.Data.SetWebLink(new Uri(_pendingUri));
            args.Request.Data.SetText(_pendingUri);
        }

        _pendingTitle = null;
        _pendingUri = null;
    }

#if !HAS_UNO
    [ComImport]
    [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);

        void ShowShareUIForWindow(IntPtr appWindow);
    }

    private static readonly Guid _dtmIid =
        new(0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);
#endif
}
