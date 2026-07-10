using System.Numerics;

namespace GcToolkit.Core.Numbers;

/// <summary>
/// A bit sieve of Eratosthenes over the odd numbers up to the 1,000,000th prime (15,485,863),
/// answering n-th prime, prime position, and prime counting queries. ~1 MB of bits plus a
/// per-word cumulative index, built once (~tens of ms) and shared via <see cref="GetSharedAsync"/>.
/// </summary>
public sealed class PrimeSieve
{
    /// <summary>The sieve upper bound — the 1,000,000th prime.</summary>
    public const int Limit = 15_485_863;

    /// <summary>Number of primes up to <see cref="Limit"/>.</summary>
    public const int PrimeCount = 1_000_000;

    private static readonly Lazy<Task<PrimeSieve>> _shared = new(() => Task.Run(() => new PrimeSieve()));

    // Bit i represents the odd number 2i + 1; prime 2 is handled explicitly everywhere.
    private readonly ulong[] _bits;

    // _cumulative[w] = count of odd primes in words [0, w) — lets every query end in one popcount.
    private readonly int[] _cumulative;

    public PrimeSieve()
    {
        var bitCount = (Limit + 1) / 2;
        _bits = new ulong[(bitCount + 63) / 64];
        Array.Fill(_bits, ulong.MaxValue);
        _bits[0] &= ~1ul; // 1 is not prime

        for (long p = 3; p * p <= Limit; p += 2)
        {
            if ((_bits[p >> 7] & 1ul << (int)(p >> 1 & 63)) == 0)
            {
                continue;
            }

            for (var multiple = p * p; multiple <= Limit; multiple += 2 * p)
            {
                _bits[multiple >> 7] &= ~(1ul << (int)(multiple >> 1 & 63));
            }
        }

        // Clear the padding bits past the limit so popcounts stay exact.
        for (var i = bitCount; i < _bits.Length * 64; i++)
        {
            _bits[i >> 6] &= ~(1ul << (i & 63));
        }

        _cumulative = new int[_bits.Length];
        var running = 0;
        for (var w = 0; w < _bits.Length; w++)
        {
            _cumulative[w] = running;
            running += BitOperations.PopCount(_bits[w]);
        }
    }

    /// <summary>The shared instance, built off-thread on first use and cached for the app lifetime.</summary>
    public static Task<PrimeSieve> GetSharedAsync() => _shared.Value;

    /// <summary>The <paramref name="n"/>-th prime (1-based; n = 1..1,000,000).</summary>
    public long NthPrime(int n)
    {
        if (n is < 1 or > PrimeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, $"Ordinal must be between 1 and {PrimeCount}.");
        }

        if (n == 1)
        {
            return 2;
        }

        // Rank among odd primes, then binary-search the cumulative index for its word.
        var rank = n - 1;
        var lo = 0;
        var hi = _bits.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (_cumulative[mid] < rank)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        var word = _bits[lo];
        for (var remaining = rank - _cumulative[lo]; ; remaining--)
        {
            var bit = BitOperations.TrailingZeroCount(word);
            if (remaining == 1)
            {
                return 2L * (lo * 64 + bit) + 1;
            }

            word &= word - 1;
        }
    }

    /// <summary>The 1-based ordinal of <paramref name="value"/> among the primes, or
    /// <see langword="null"/> when it is not a prime within 2..<see cref="Limit"/>.</summary>
    public int? PositionOf(long value)
    {
        if (value is < 2 or > Limit)
        {
            return null;
        }

        if (value == 2)
        {
            return 1;
        }

        if ((value & 1) == 0)
        {
            return null;
        }

        var index = (int)(value >> 1);
        var word = index >> 6;
        var bit = index & 63;
        if ((_bits[word] & 1ul << bit) == 0)
        {
            return null;
        }

        // 1 for the prime 2, plus odd primes before it, plus itself.
        return _cumulative[word] + BitOperations.PopCount(_bits[word] & (1ul << bit) - 1) + 2;
    }

    /// <summary>π(value): how many primes are ≤ <paramref name="value"/> (value ≤ <see cref="Limit"/>).</summary>
    public int CountUpTo(long value)
    {
        if (value > Limit)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value must be at most {Limit}.");
        }

        if (value < 2)
        {
            return 0;
        }

        var index = (int)((value - 1) >> 1); // highest odd number ≤ value
        var word = index >> 6;
        var bit = index & 63;
        var oddPrimes = _cumulative[word] + BitOperations.PopCount(_bits[word] & ((1ul << bit) - 1 | 1ul << bit));

        return oddPrimes + 1;
    }
}
