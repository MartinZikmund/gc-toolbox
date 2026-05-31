using System.IO;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// Renders a Morse <see cref="MorseSignal"/> timeline to an in-memory 16-bit PCM mono WAV. Tone
/// segments are a sine wave at the chosen frequency with a short fade in/out to avoid clicks; gaps are
/// silence. The bytes are produced here (pure, testable) so the platform audio service only has to play
/// them — keeping the only interesting logic out of the head and under unit test.
/// </summary>
public static class MorseAudioRenderer
{
    private const int SampleRate = 44_100;
    private const double Amplitude = 0.6;
    private const int FadeMilliseconds = 5;

    /// <summary>
    /// Builds a WAV byte buffer for <paramref name="timeline"/> at the given tone
    /// <paramref name="frequencyHz"/> and Morse <paramref name="unitMilliseconds"/> (see
    /// <see cref="MorseTiming.UnitMilliseconds"/>). Returns a valid, silent WAV when the timeline is empty.
    /// </summary>
    public static byte[] RenderWav(IReadOnlyList<MorseSignal> timeline, int frequencyHz, int unitMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var samples = BuildSamples(timeline, frequencyHz, unitMilliseconds);
        return WriteWav(samples);
    }

    private static short[] BuildSamples(IReadOnlyList<MorseSignal> timeline, int frequencyHz, int unitMilliseconds)
    {
        var total = 0;
        foreach (var signal in timeline)
        {
            total += SampleCount(signal.Units, unitMilliseconds);
        }

        var samples = new short[total];
        var offset = 0;
        var fadeSamples = Math.Max(1, SampleRate * FadeMilliseconds / 1000);
        var angularStep = 2.0 * Math.PI * frequencyHz / SampleRate;

        foreach (var signal in timeline)
        {
            var count = SampleCount(signal.Units, unitMilliseconds);
            if (signal.On)
            {
                // Clamp the fade so a short element's fade-in and fade-out never overlap.
                var fade = Math.Max(1, Math.Min(fadeSamples, count / 2));
                for (var i = 0; i < count; i++)
                {
                    var envelope = Envelope(i, count, fade);
                    var value = Math.Sin(angularStep * i) * Amplitude * envelope;
                    samples[offset + i] = (short)(value * short.MaxValue);
                }
            }
            // Off segments stay zero (silence).

            offset += count;
        }

        return samples;
    }

    private static double Envelope(int index, int count, int fadeSamples)
    {
        if (index < fadeSamples)
        {
            return (double)index / fadeSamples;
        }

        if (index >= count - fadeSamples)
        {
            return (double)(count - index - 1) / fadeSamples;
        }

        return 1.0;
    }

    private static int SampleCount(int units, int unitMilliseconds)
        => Math.Max(0, units) * unitMilliseconds * SampleRate / 1000;

    private static byte[] WriteWav(short[] samples)
    {
        var dataBytes = samples.Length * sizeof(short);
        using var stream = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(stream);

        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = SampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));

        // RIFF header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(SampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);

        // data chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return stream.ToArray();
    }
}
