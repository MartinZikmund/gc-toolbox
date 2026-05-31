namespace GcToolkit.Core.Alphabets;

/// <summary>
/// Turns a Morse code string (dots/dashes with the usual separators) into an ordered list of
/// on/off <see cref="MorseSignal"/>s using standard relative timing: dot = 1 unit, dash = 3,
/// intra-character gap = 1, inter-letter gap = 3, word gap = 7. The same timeline drives both the
/// audio beep and the on-screen flash, so they stay in sync. No trailing gap is emitted.
/// </summary>
public static class MorseTimeline
{
    private const int DotUnits = 1;
    private const int DashUnits = 3;
    private const int IntraCharacterGapUnits = 1;
    private const int LetterGapUnits = 3;
    private const int WordGapUnits = 7;

    public static IReadOnlyList<MorseSignal> Build(string? morse)
    {
        var signals = new List<MorseSignal>();
        if (string.IsNullOrWhiteSpace(morse))
        {
            return signals;
        }

        // Group into words (on '/' or 2+ spaces) → letters (on single spaces) → elements, mirroring
        // the separator rules MorseCodec uses on decode.
        var grouped = GroupWords(morse);

        for (var w = 0; w < grouped.Count; w++)
        {
            var letters = grouped[w];
            for (var l = 0; l < letters.Count; l++)
            {
                var element = letters[l];
                for (var e = 0; e < element.Length; e++)
                {
                    signals.Add(new MorseSignal(true, element[e] == '-' ? DashUnits : DotUnits));
                    if (e < element.Length - 1)
                    {
                        signals.Add(new MorseSignal(false, IntraCharacterGapUnits));
                    }
                }

                if (l < letters.Count - 1)
                {
                    signals.Add(new MorseSignal(false, LetterGapUnits));
                }
            }

            if (w < grouped.Count - 1)
            {
                signals.Add(new MorseSignal(false, WordGapUnits));
            }
        }

        return signals;
    }

    private static List<List<string>> GroupWords(string morse)
    {
        var result = new List<List<string>>();

        foreach (var rawWord in System.Text.RegularExpressions.Regex.Split(morse.Trim(), @"\s*/\s*|\s{2,}"))
        {
            if (string.IsNullOrWhiteSpace(rawWord))
            {
                continue;
            }

            var letters = new List<string>();
            foreach (var token in rawWord.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                // Keep only dot/dash elements; anything else (e.g. an unknown '#') contributes no signal.
                var trimmed = Sanitize(token);
                if (trimmed.Length > 0)
                {
                    letters.Add(trimmed);
                }
            }

            if (letters.Count > 0)
            {
                result.Add(letters);
            }
        }

        return result;
    }

    private static string Sanitize(string token)
    {
        foreach (var c in token)
        {
            if (c != '.' && c != '-')
            {
                // Token contains a non-Morse symbol (such as the unknown placeholder); skip it.
                return string.Empty;
            }
        }

        return token;
    }
}
