namespace GcToolkit.Core.Alphabets;

/// <summary>
/// The canonical flag-semaphore alphabet: 26 letters plus the Space (rest), Letters and Numbers
/// signs, with the standard digit doubling (A=1 … I=9, K=0; J is skipped). Arm positions are the
/// signaller's own left/right, cross-checked against the standard chart (Wikipedia "Flag semaphore").
/// </summary>
public static class SemaphoreAlphabet
{
    /// <summary>Rest position — both arms down. Doubles as the word space.</summary>
    public static SemaphoreFigure Space { get; } =
        new(SemaphoreFigureKind.Space, '\0', null, SemaphoreArmPosition.Down, SemaphoreArmPosition.Down);

    /// <summary>"Letters follow" sign — same arms as J, distinct meaning.</summary>
    public static SemaphoreFigure LettersSign { get; } =
        new(SemaphoreFigureKind.LettersSign, '\0', null, SemaphoreArmPosition.Out, SemaphoreArmPosition.Up);

    /// <summary>"Numerals follow" sign.</summary>
    public static SemaphoreFigure NumbersSign { get; } =
        new(SemaphoreFigureKind.NumbersSign, '\0', null, SemaphoreArmPosition.High, SemaphoreArmPosition.Up);

    /// <summary>"Cancel" — nullifies the signal sent so far. A single upper-left to lower-right diagonal.</summary>
    public static SemaphoreFigure Cancel { get; } =
        new(SemaphoreFigureKind.Cancel, '\0', null, SemaphoreArmPosition.Low, SemaphoreArmPosition.High);

    /// <summary>"Error" — both flags waved overhead and back down (the repeated-E waggle), so it carries
    /// the down pose as a secondary position to render as motion.</summary>
    public static SemaphoreFigure Error { get; } =
        new(SemaphoreFigureKind.Error, '\0', null,
            SemaphoreArmPosition.High, SemaphoreArmPosition.High,
            SemaphoreArmPosition.Low, SemaphoreArmPosition.Low);

    /// <summary>Display-only reference signals (Cancel, Error) — never tappable and not part of the codec.</summary>
    public static IReadOnlyList<SemaphoreFigure> SpecialFigures { get; } = [Cancel, Error];

    public static IReadOnlyDictionary<char, char> DigitToLetter { get; } = new Dictionary<char, char>
    {
        ['1'] = 'A',
        ['2'] = 'B',
        ['3'] = 'C',
        ['4'] = 'D',
        ['5'] = 'E',
        ['6'] = 'F',
        ['7'] = 'G',
        ['8'] = 'H',
        ['9'] = 'I',
        ['0'] = 'K',
    };

    public static IReadOnlyDictionary<char, SemaphoreFigure> Letters { get; } = BuildLetters();

    /// <summary>Chart/palette order: A–Z, then Space, Letters and Numbers signs.</summary>
    public static IReadOnlyList<SemaphoreFigure> ChartFigures { get; } =
        [.. Enumerable.Range('A', 26).Select(c => Letters[(char)c]), Space, LettersSign, NumbersSign];

    private static Dictionary<char, SemaphoreFigure> BuildLetters()
    {
        // (letter, signaller's left arm, signaller's right arm)
        (char Letter, SemaphoreArmPosition Left, SemaphoreArmPosition Right)[] table =
        [
            ('A', SemaphoreArmPosition.Down, SemaphoreArmPosition.Low),
            ('B', SemaphoreArmPosition.Down, SemaphoreArmPosition.Out),
            ('C', SemaphoreArmPosition.Down, SemaphoreArmPosition.High),
            ('D', SemaphoreArmPosition.Down, SemaphoreArmPosition.Up),
            ('E', SemaphoreArmPosition.High, SemaphoreArmPosition.Down),
            ('F', SemaphoreArmPosition.Out, SemaphoreArmPosition.Down),
            ('G', SemaphoreArmPosition.Low, SemaphoreArmPosition.Down),
            ('H', SemaphoreArmPosition.AcrossLow, SemaphoreArmPosition.Out),
            ('I', SemaphoreArmPosition.AcrossLow, SemaphoreArmPosition.High),
            ('J', SemaphoreArmPosition.Out, SemaphoreArmPosition.Up),
            ('K', SemaphoreArmPosition.Up, SemaphoreArmPosition.Low),
            ('L', SemaphoreArmPosition.High, SemaphoreArmPosition.Low),
            ('M', SemaphoreArmPosition.Out, SemaphoreArmPosition.Low),
            ('N', SemaphoreArmPosition.Low, SemaphoreArmPosition.Low),
            ('O', SemaphoreArmPosition.AcrossHigh, SemaphoreArmPosition.Out),
            ('P', SemaphoreArmPosition.Up, SemaphoreArmPosition.Out),
            ('Q', SemaphoreArmPosition.High, SemaphoreArmPosition.Out),
            ('R', SemaphoreArmPosition.Out, SemaphoreArmPosition.Out),
            ('S', SemaphoreArmPosition.Low, SemaphoreArmPosition.Out),
            ('T', SemaphoreArmPosition.Up, SemaphoreArmPosition.High),
            ('U', SemaphoreArmPosition.High, SemaphoreArmPosition.High),
            ('V', SemaphoreArmPosition.Low, SemaphoreArmPosition.Up),
            ('W', SemaphoreArmPosition.Out, SemaphoreArmPosition.AcrossHigh),
            ('X', SemaphoreArmPosition.Low, SemaphoreArmPosition.AcrossHigh),
            ('Y', SemaphoreArmPosition.Out, SemaphoreArmPosition.High),
            ('Z', SemaphoreArmPosition.Out, SemaphoreArmPosition.AcrossLow),
        ];

        var letterByDigit = DigitToLetter.ToDictionary(p => p.Value, p => p.Key);
        return table.ToDictionary(
            e => e.Letter,
            e => new SemaphoreFigure(
                SemaphoreFigureKind.Letter,
                e.Letter,
                letterByDigit.TryGetValue(e.Letter, out var digit) ? digit : null,
                e.Left,
                e.Right));
    }
}
