namespace GcToolkit.Services;

public interface ILauncherService
{
    Task<bool> LaunchUriAsync(Uri uri);
}
