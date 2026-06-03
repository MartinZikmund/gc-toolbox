using System.Threading;
using System.Threading.Tasks;

namespace GcToolkit.Core.Services;

/// <summary>
/// Plays a pre-rendered WAV buffer (see <c>MorseAudioRenderer</c>). Audio is platform-dependent, so
/// callers must check <see cref="IsSupported"/> and hide audio affordances when it is false.
/// </summary>
public interface IMorseAudioService
{
    /// <summary><see langword="true"/> when this platform can play audio.</summary>
    bool IsSupported { get; }

    /// <summary>Plays <paramref name="wav"/> to completion, or until <paramref name="cancellationToken"/>
    /// is cancelled. A no-op (completed task) on platforms where <see cref="IsSupported"/> is false.</summary>
    Task PlayAsync(byte[] wav, CancellationToken cancellationToken);

    /// <summary>Stops any current playback immediately.</summary>
    void Stop();
}
