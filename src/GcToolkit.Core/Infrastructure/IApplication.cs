namespace GcToolkit.Core.Infrastructure;

public interface IApplication
{
    ApplicationTheme RequestedTheme { get; }

    ResourceDictionary Resources { get; }

    /// <summary>The app's display version, e.g. <c>1.2.3</c>.</summary>
    string AppVersion { get; }

    void Exit();
}
