using GcToolkit.Core.Services;

#if !HAS_UNO
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
#endif

namespace GcToolkit.Services;

/// <summary>
/// Plays a pre-rendered WAV buffer. Implemented on the Windows (WinAppSDK) head via the native WinRT
/// <see cref="MediaPlayer"/> + an in-memory stream (offline, no extra packages). On the Uno heads it
/// reports <see cref="IsSupported"/> = false and no-ops; the Morse view hides the audio affordances
/// there while the on-screen flash keeps working everywhere.
/// </summary>
public sealed class MorseAudioService : IMorseAudioService
{
#if !HAS_UNO
    private MediaPlayer? _player;

    public bool IsSupported => true;

    public async Task PlayAsync(byte[] wav, CancellationToken cancellationToken)
    {
        Stop();

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var stream = new InMemoryRandomAccessStream();
        MediaSource? source = null;
        MediaPlayer? player = null;
        try
        {
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(wav);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);

            source = MediaSource.CreateFromStream(stream, "audio/wav");
            player = new MediaPlayer { Source = source };
            _player = player;

            // RunContinuationsAsynchronously keeps the await continuation (and the disposal below) off the
            // WinRT media-event thread that raises MediaEnded/MediaFailed.
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnEnded(MediaPlayer sender, object args) => completion.TrySetResult();
            void OnFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) => completion.TrySetResult();

            player.MediaEnded += OnEnded;
            player.MediaFailed += OnFailed;

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    player.Pause();
                }
                catch (Exception)
                {
                    // Ignore — the player may already be torn down.
                }

                completion.TrySetResult();
            });

            try
            {
                player.Play();
                await completion.Task;
            }
            finally
            {
                player.MediaEnded -= OnEnded;
                player.MediaFailed -= OnFailed;
            }
        }
        finally
        {
            if (ReferenceEquals(_player, player))
            {
                _player = null;
            }

            player?.Dispose();
            source?.Dispose();
            stream.Dispose();
        }
    }

    public void Stop()
    {
        var player = _player;
        _player = null;
        if (player is not null)
        {
            try
            {
                player.Pause();
            }
            catch (Exception)
            {
                // Ignore — disposing below regardless.
            }

            player.Dispose();
        }
    }
#else
    public bool IsSupported => false;

    public Task PlayAsync(byte[] wav, CancellationToken cancellationToken) => Task.CompletedTask;

    public void Stop()
    {
    }
#endif
}
