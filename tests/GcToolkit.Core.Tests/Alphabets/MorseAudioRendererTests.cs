using System.Text;
using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class MorseAudioRendererTests
{
    private const int SampleRate = 44_100;
    private const int HeaderBytes = 44;

    [TestMethod]
    public void RenderWav_Empty_IsHeaderOnly()
    {
        var wav = MorseAudioRenderer.RenderWav([], 600, 100);

        Assert.AreEqual(HeaderBytes, wav.Length);
    }

    [TestMethod]
    public void RenderWav_WritesValidRiffWaveHeader()
    {
        var wav = MorseAudioRenderer.RenderWav([new MorseSignal(true, 1)], 600, 100);

        Assert.AreEqual("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.AreEqual("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.AreEqual("data", Encoding.ASCII.GetString(wav, 36, 4));
    }

    [TestMethod]
    public void RenderWav_LengthMatchesTiming()
    {
        // on(3) + off(1) units at 10 ms/unit → (3 + 1) * 10 ms of 44.1 kHz 16-bit mono.
        const int unitMs = 10;
        var wav = MorseAudioRenderer.RenderWav([new MorseSignal(true, 3), new MorseSignal(false, 1)], 600, unitMs);

        var expectedSamples = (3 * unitMs * SampleRate / 1000) + (1 * unitMs * SampleRate / 1000);
        Assert.AreEqual(HeaderBytes + (expectedSamples * sizeof(short)), wav.Length);
    }

    [TestMethod]
    public void RenderWav_ToneSegment_ProducesNonSilentSamples()
    {
        var wav = MorseAudioRenderer.RenderWav([new MorseSignal(true, 2)], 600, 100);

        var hasSignal = false;
        for (var i = HeaderBytes; i < wav.Length; i++)
        {
            if (wav[i] != 0)
            {
                hasSignal = true;
                break;
            }
        }

        Assert.IsTrue(hasSignal, "A tone segment must contain non-zero PCM samples.");
    }

    [TestMethod]
    public void RenderWav_GapSegment_IsSilent()
    {
        var wav = MorseAudioRenderer.RenderWav([new MorseSignal(false, 2)], 600, 100);

        for (var i = HeaderBytes; i < wav.Length; i++)
        {
            Assert.AreEqual(0, wav[i], "A gap segment must be silent.");
        }
    }
}
