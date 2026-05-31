using System.Threading.Tasks;

namespace GcToolkit.Core.Services;

/// <summary>
/// Opens the OS share sheet. Lives in Core so tool ViewModels can share results; implemented per
/// platform in the app head.
/// </summary>
public interface IShareService
{
    /// <summary>Shares a web link.</summary>
    Task ShareAsync(string title, string uri);

    /// <summary>Shares plain text (e.g. a tool's result).</summary>
    Task ShareTextAsync(string title, string text);
}
