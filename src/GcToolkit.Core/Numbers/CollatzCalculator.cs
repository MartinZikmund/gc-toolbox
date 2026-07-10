using System.Numerics;
using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>
/// The result of tracing one starting number through the Collatz (3n+1) map until it reaches 1.
/// </summary>
/// <param name="Sequence">The full hailstone sequence, including the start and the terminal <c>1</c>.</param>
/// <param name="Steps">The stopping time — the number of operations applied to reach 1 (<see cref="Length"/> − 1).</param>
/// <param name="Peak">The largest value reached anywhere in the sequence.</param>
/// <param name="PeakIndex">The index of <see cref="Peak"/> within <see cref="Sequence"/> (first occurrence).</param>
/// <param name="EvenSteps">How many steps halved an even value.</param>
/// <param name="OddSteps">How many steps applied the 3n+1 rule to an odd value.</param>
/// <param name="ParityBits">One char per step (<c>O</c> = the value was odd, <c>E</c> = even), excluding the terminal 1.</param>
public sealed record CollatzTrace(
    IReadOnlyList<BigInteger> Sequence,
    int Steps,
    BigInteger Peak,
    int PeakIndex,
    int EvenSteps,
    int OddSteps,
    string ParityBits)
{
    /// <summary>The total length of the sequence, including the start and the terminal <c>1</c>.</summary>
    public int Length => Sequence.Count;
}

/// <summary>The longest-sequence finding of a <see cref="CollatzCalculator.MostStubborn"/> search.</summary>
/// <param name="Starter">The starting number whose sequence was the longest in range.</param>
/// <param name="Steps">That sequence's stopping time.</param>
public readonly record struct CollatzStubbornResult(BigInteger Starter, int Steps);

/// <summary>
/// Pure Collatz conjecture (3n+1) engine. Tracing uses arbitrary-precision <see cref="BigInteger"/> so
/// large starters never overflow (the sequence can briefly exceed <see cref="long"/> range). Mirrors the
/// cachesleuth.com Collatz tool (trace a number + most-stubborn search capped at 1,000,000) and goes
/// beyond it with the even/odd step split and a per-step parity bit-string.
/// </summary>
public sealed class CollatzCalculator
{
    /// <summary>The smallest valid starting number (Collatz is defined for positive integers).</summary>
    public const int MinValue = 1;

    /// <summary>Upper bound on the most-stubborn search range, matching cachesleuth's responsiveness cap.</summary>
    public const int MaxSearchLimit = 1_000_000;

    /// <summary>
    /// Traces <paramref name="start"/> through the Collatz map to 1, returning the full sequence and stats.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> is less than <see cref="MinValue"/>.</exception>
    public CollatzTrace Trace(BigInteger start)
    {
        if (start < MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, $"Starting number must be at least {MinValue}.");
        }

        var sequence = new List<BigInteger> { start };
        var peak = start;
        var peakIndex = 0;
        var evenSteps = 0;
        var oddSteps = 0;
        var parity = new StringBuilder();

        var current = start;
        while (current > BigInteger.One)
        {
            if (current.IsEven)
            {
                current /= 2;
                evenSteps++;
                parity.Append('E');
            }
            else
            {
                current = (current * 3) + BigInteger.One;
                oddSteps++;
                parity.Append('O');
            }

            sequence.Add(current);
            if (current > peak)
            {
                peak = current;
                peakIndex = sequence.Count - 1;
            }
        }

        return new CollatzTrace(sequence, evenSteps + oddSteps, peak, peakIndex, evenSteps, oddSteps, parity.ToString());
    }

    /// <summary>
    /// Returns the stopping time of <paramref name="start"/> without materializing the sequence — the
    /// inner loop of <see cref="MostStubborn"/>.
    /// </summary>
    public int StepCount(BigInteger start)
    {
        if (start < MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, $"Starting number must be at least {MinValue}.");
        }

        var steps = 0;
        var current = start;
        while (current > BigInteger.One)
        {
            current = current.IsEven ? current / 2 : (current * 3) + BigInteger.One;
            steps++;
        }

        return steps;
    }

    /// <summary>
    /// Finds the starter in <c>1..limit</c> with the longest sequence (the lowest such starter on a tie).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside <c>[1, <see cref="MaxSearchLimit"/>]</c>.</exception>
    public CollatzStubbornResult MostStubborn(int limit)
    {
        if (limit < MinValue || limit > MaxSearchLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, $"Limit must be between {MinValue} and {MaxSearchLimit}.");
        }

        // Memoize stopping times up to the limit so each starter reuses already-computed tails. Values can
        // briefly exceed the limit (or long range) mid-sequence, so only cache hits within the table count.
        var cache = new int[limit + 1];
        var bestStarter = 1;
        var bestSteps = 0;

        for (var n = 1; n <= limit; n++)
        {
            var steps = StepsWithMemo(n, cache, limit);
            cache[n] = steps;
            if (steps > bestSteps)
            {
                bestSteps = steps;
                bestStarter = n;
            }
        }

        return new CollatzStubbornResult(bestStarter, bestSteps);
    }

    private static int StepsWithMemo(int start, int[] cache, int limit)
    {
        var steps = 0;
        BigInteger current = start;
        while (current > BigInteger.One)
        {
            // Reuse a previously computed tail once the value falls back inside the memo table.
            // limit == cache.Length - 1, so current <= limit guarantees the (int) cast is in range.
            if (current <= limit && cache[(int)current] > 0)
            {
                return steps + cache[(int)current];
            }

            current = current.IsEven ? current / 2 : (current * 3) + BigInteger.One;
            steps++;
        }

        return steps;
    }
}
