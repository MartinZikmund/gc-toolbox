namespace GcToolkit.Core.Infrastructure;

public interface IAppUpdater
{
    Task EnsureAppUpToDateAsync();
}
