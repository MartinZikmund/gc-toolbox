namespace GcToolkit.Core.Alphabets;

/// <summary>
/// One segment of a Morse playback timeline: the key is either down (<see cref="On"/> = true, a tone /
/// light) or up (a gap), held for <see cref="Units"/> Morse time units. A unit's wall-clock length is
/// derived from words-per-minute (see <see cref="MorseTiming"/>).
/// </summary>
public readonly record struct MorseSignal(bool On, int Units);
