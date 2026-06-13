namespace GcToolkit.Core.Numbers;

/// <summary>One iteration of Kaprekar's routine: the digits arranged descending and ascending, and
/// the difference between the two (which becomes the next iteration's input).</summary>
public readonly record struct KaprekarStep(long Descending, long Ascending, long Difference);

/// <summary>The full trace of running Kaprekar's routine on a seed at a given digit width.</summary>
/// <param name="Seed">The starting number.</param>
/// <param name="DigitWidth">The fixed digit width the routine pads to.</param>
/// <param name="Steps">Ordered iterations until the fixed point (or the single degenerate step).</param>
/// <param name="Constant">The fixed point reached (e.g. 6174 / 495), or 0 for a repdigit.</param>
/// <param name="ReachedConstant"><see langword="true"/> when a non-zero fixed point was reached.</param>
/// <param name="IsRepdigit"><see langword="true"/> when every digit was the same (collapses to 0).</param>
public readonly record struct KaprekarRoutineResult(
    long Seed,
    int DigitWidth,
    IReadOnlyList<KaprekarStep> Steps,
    long Constant,
    bool ReachedConstant,
    bool IsRepdigit)
{
    /// <summary>Number of iterations performed to reach the fixed point.</summary>
    public int StepCount => Steps.Count;
}

/// <summary>Describes a Kaprekar number's defining square split: <c>n² = Left·10^k + Right</c> where
/// <c>Left + Right == n</c> (e.g. <c>45² = 2025 → 20 + 25 = 45</c>).</summary>
public readonly record struct KaprekarNumberDescription(long Number, long Square, long Left, long Right);

/// <summary>
/// Pure number-theory engine behind the Kaprekar tool. Implements two distinct notions that share the
/// name "Kaprekar": the <b>routine</b> (sort-and-subtract toward 6174 / 495) and <b>Kaprekar numbers</b>
/// (n whose square splits into parts summing to n). No I/O — fully offline and unit-testable.
/// </summary>
public sealed class KaprekarCalculator
{
    /// <summary>Hard cap on the Kaprekar-number lister (parity with cachesleuth.com).</summary>
    public const long MaxListLimit = 1_000_000;

    /// <summary>Smallest supported digit width.</summary>
    public const int MinDigitWidth = 2;

    /// <summary>Largest supported digit width (keeps the routine's work bounded).</summary>
    public const int MaxDigitWidth = 10;

    // A run can't exceed a small fixed bound for the real constants; the guard catches any width whose
    // orbit is a longer cycle rather than a fixed point, so we never loop forever.
    private const int MaxIterations = 64;

    /// <summary>
    /// Runs Kaprekar's routine on <paramref name="number"/> padded to <paramref name="digitWidth"/>:
    /// sort the digits high-to-low and low-to-high, subtract the smaller from the larger, and repeat
    /// until the value stops changing. A repdigit (all identical digits) yields a difference of 0 and is
    /// flagged as the degenerate case rather than looping.
    /// </summary>
    public KaprekarRoutineResult Routine(long number, int digitWidth)
    {
        if (digitWidth < MinDigitWidth || digitWidth > MaxDigitWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(digitWidth), digitWidth,
                $"Digit width must be between {MinDigitWidth} and {MaxDigitWidth}.");
        }

        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Number must be non-negative.");
        }

        var max = Pow10(digitWidth) - 1;
        if (number > max)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number,
                $"Number must fit within {digitWidth} digits (0–{max}).");
        }

        var steps = new List<KaprekarStep>();
        var current = number;
        var seen = new HashSet<long>();

        for (var i = 0; i < MaxIterations; i++)
        {
            var digits = ToDigits(current, digitWidth);
            var descending = FromDigits(digits, descending: true);
            var ascending = FromDigits(digits, descending: false);
            var difference = descending - ascending;

            // Repdigit input: every digit is the same, so the two arrangements are equal and the
            // difference is 0. This includes the seed itself (e.g. 1111) — flag it as the degenerate
            // case and stop, recording the one zero-difference step so the UI can show what happened.
            if (difference == 0)
            {
                steps.Add(new KaprekarStep(descending, ascending, 0));
                return new KaprekarRoutineResult(number, digitWidth, steps, 0, ReachedConstant: false, IsRepdigit: true);
            }

            // Fixed point: the input was already the constant, so this step would just reproduce it —
            // don't record a redundant step. (3524 reaches 6174 in 3 steps, 6174 itself in 0.)
            if (difference == current)
            {
                return new KaprekarRoutineResult(number, digitWidth, steps, current, ReachedConstant: true, IsRepdigit: false);
            }

            steps.Add(new KaprekarStep(descending, ascending, difference));

            // Cycle guard for widths that orbit instead of fixing (defensive — 3/4 always fix).
            if (!seen.Add(difference))
            {
                return new KaprekarRoutineResult(number, digitWidth, steps, difference, ReachedConstant: true, IsRepdigit: false);
            }

            current = difference;
        }

        return new KaprekarRoutineResult(number, digitWidth, steps, current, ReachedConstant: true, IsRepdigit: false);
    }

    /// <summary>
    /// Tests whether <paramref name="n"/> is a Kaprekar number: its square can be split into a left and
    /// right part that sum to <paramref name="n"/>, where the right part has as many digits as <c>n</c>
    /// (e.g. 45 → 2025 → 20 + 25). 1 is Kaprekar by convention; non-positive values are not.
    /// </summary>
    public bool IsKaprekarNumber(long n) => TryDescribeKaprekar(n, out _);

    /// <summary>
    /// Like <see cref="IsKaprekarNumber"/> but also yields the square split when <paramref name="n"/> is
    /// a Kaprekar number.
    /// </summary>
    public bool TryDescribeKaprekar(long n, out KaprekarNumberDescription description)
    {
        description = default;
        if (n < 1)
        {
            return false;
        }

        if (n == 1)
        {
            description = new KaprekarNumberDescription(1, 1, 0, 1);
            return true;
        }

        var square = n * n;

        // The right part takes as many digits as n; the left part is everything above it.
        var digits = DigitCount(n);
        var divisor = Pow10(digits);
        var right = square % divisor;
        var left = square / divisor;

        // The right part must be non-zero (e.g. 100² = 10000 → 100 + 00 is excluded).
        if (right != 0 && left + right == n)
        {
            description = new KaprekarNumberDescription(n, square, left, right);
            return true;
        }

        return false;
    }

    /// <summary>Lists every Kaprekar number in <c>[1, limit]</c>, clamped at <see cref="MaxListLimit"/>.</summary>
    public IReadOnlyList<long> ListKaprekar(long limit)
    {
        var ceiling = Math.Min(Math.Max(limit, 0), MaxListLimit);
        var result = new List<long>();
        for (long n = 1; n <= ceiling; n++)
        {
            if (IsKaprekarNumber(n))
            {
                result.Add(n);
            }
        }

        return result;
    }

    private static int[] ToDigits(long value, int width)
    {
        var digits = new int[width];
        for (var i = width - 1; i >= 0; i--)
        {
            digits[i] = (int)(value % 10);
            value /= 10;
        }

        return digits;
    }

    private static long FromDigits(int[] digits, bool descending)
    {
        var ordered = (int[])digits.Clone();
        Array.Sort(ordered);
        if (descending)
        {
            Array.Reverse(ordered);
        }

        long value = 0;
        foreach (var d in ordered)
        {
            value = value * 10 + d;
        }

        return value;
    }

    private static int DigitCount(long n)
    {
        var count = 0;
        do
        {
            count++;
            n /= 10;
        }
        while (n > 0);

        return count;
    }

    private static long Pow10(int exponent)
    {
        long value = 1;
        for (var i = 0; i < exponent; i++)
        {
            value *= 10;
        }

        return value;
    }
}
