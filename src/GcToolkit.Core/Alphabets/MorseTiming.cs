namespace GcToolkit.Core.Alphabets;

/// <summary>Standard Morse timing helpers (the PARIS word standard).</summary>
public static class MorseTiming
{
    /// <summary>Sensible bounds for a words-per-minute setting exposed in the UI.</summary>
    public const int MinWordsPerMinute = 5;

    /// <summary>Sensible bounds for a words-per-minute setting exposed in the UI.</summary>
    public const int MaxWordsPerMinute = 40;

    /// <summary>
    /// The length of a single Morse time unit (a "dit") in milliseconds for the given
    /// <paramref name="wordsPerMinute"/>, using the PARIS standard (a unit = 1200 / WPM ms).
    /// </summary>
    public static int UnitMilliseconds(int wordsPerMinute)
    {
        var clamped = Math.Clamp(wordsPerMinute, MinWordsPerMinute, MaxWordsPerMinute);
        return 1200 / clamped;
    }
}
