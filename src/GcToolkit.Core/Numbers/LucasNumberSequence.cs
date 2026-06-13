using System.Numerics;

namespace GcToolkit.Core.Numbers;

/// <summary>
/// The Lucas sequence — the Fibonacci-style recurrence seeded with <c>L(0) = 2</c>, <c>L(1) = 1</c> and
/// <c>L(n) = L(n-1) + L(n-2)</c> (zero-based). Uses <see cref="BigInteger"/> so values never overflow
/// (e.g. <c>L(10000)</c> has 2090 digits) and lazily extends a cached list so repeated lookups are cheap.
/// </summary>
/// <remarks>
/// The sequence is strictly increasing from index 1 onward (1, 3, 4, 7, …), so a Lucas value other than
/// the duplicated low values maps to a single index; <see cref="IndexOf"/> exploits that for an
/// unambiguous reverse lookup. <see cref="MaxIndex"/> guards against runaway input rather than letting a
/// huge request exhaust memory.
/// </remarks>
public sealed class LucasNumberSequence
{
    /// <summary>Largest accepted index. <c>L(200000)</c> is enormous but bounded; beyond this we reject
    /// rather than risk an out-of-memory allocation.</summary>
    public const int MaxIndex = 200_000;

    // Cache seeded with the first two anchors; index i holds L(i). Extended on demand.
    private readonly List<BigInteger> _cache = [2, 1];

    /// <summary>Returns <c>L(n)</c> for <paramref name="n"/> in <c>[0, <see cref="MaxIndex"/>]</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative or above <see cref="MaxIndex"/>.</exception>
    public BigInteger At(int n)
    {
        if (n < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "Index must be zero or positive.");
        }

        if (n > MaxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, $"Index must not exceed {MaxIndex}.");
        }

        EnsureComputed(n);
        return _cache[n];
    }

    /// <summary>Enumerates <c>(index, L(index))</c> for every index in the inclusive range
    /// <c>[<paramref name="start"/>, <paramref name="endInclusive"/>]</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A bound is negative or above <see cref="MaxIndex"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="start"/> is greater than <paramref name="endInclusive"/>.</exception>
    public IEnumerable<(int Index, BigInteger Value)> Range(int start, int endInclusive)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Start must be zero or positive.");
        }

        if (endInclusive < 0 || endInclusive > MaxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(endInclusive), endInclusive, $"End must be in [0, {MaxIndex}].");
        }

        if (start > endInclusive)
        {
            throw new ArgumentException("Start must not be greater than end.", nameof(start));
        }

        return Enumerate(start, endInclusive);

        IEnumerable<(int, BigInteger)> Enumerate(int from, int to)
        {
            for (var i = from; i <= to; i++)
            {
                yield return (i, At(i));
            }
        }
    }

    /// <summary>Reverse lookup: the index whose Lucas value equals <paramref name="value"/>, or
    /// <see langword="null"/> when <paramref name="value"/> is not a Lucas number.</summary>
    public int? IndexOf(BigInteger value)
    {
        if (value < BigInteger.One)
        {
            // The smallest values are L1 = 1 and L0 = 2; nothing below 1 is a Lucas number.
            return null;
        }

        // Walk forward (extending the cache) until we meet or pass the value. From index 1 the sequence
        // is strictly increasing, so a single hit is the unambiguous position.
        for (var i = 1; i <= MaxIndex; i++)
        {
            var current = At(i);
            if (current == value)
            {
                return i;
            }

            if (current > value)
            {
                break;
            }
        }

        // 2 is also L0; checked separately so the increasing scan above stays simple.
        return value == 2 ? 0 : null;
    }

    /// <summary>Enumerates <c>(index, L(index))</c> for every Lucas number whose base-10 length equals
    /// <paramref name="digits"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="digits"/> is not positive.</exception>
    public IEnumerable<(int Index, BigInteger Value)> WithDigitCount(int digits)
    {
        if (digits < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(digits), digits, "Digit count must be positive.");
        }

        return Enumerate(digits);

        IEnumerable<(int, BigInteger)> Enumerate(int wanted)
        {
            for (var i = 0; i <= MaxIndex; i++)
            {
                var length = At(i).ToString().Length;
                if (length == wanted)
                {
                    yield return (i, _cache[i]);
                }
                else if (length > wanted)
                {
                    // Lengths are non-decreasing, so once we pass the target width we are done.
                    yield break;
                }
            }
        }
    }

    private void EnsureComputed(int n)
    {
        for (var i = _cache.Count; i <= n; i++)
        {
            _cache.Add(_cache[i - 1] + _cache[i - 2]);
        }
    }
}
