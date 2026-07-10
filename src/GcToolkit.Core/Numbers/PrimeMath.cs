using System.Numerics;

namespace GcToolkit.Core.Numbers;

/// <summary>
/// Deterministic primality testing, prime navigation, and factorization for the full
/// 2..2^53 range (every integer exactly representable in a double), with no precomputed
/// tables: Miller-Rabin with a witness set proven deterministic for all 64-bit values,
/// plus Pollard's rho for splitting large composites.
/// </summary>
public static class PrimeMath
{
    /// <summary>Upper bound of the supported range: 2^53.</summary>
    public const ulong MaxValue = 9_007_199_254_740_992;

    // Deterministic for all n < 3,317,044,064,679,887,385,961,981 — far beyond 2^53.
    private static readonly ulong[] _witnesses = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37];

    public static bool IsPrime(ulong n)
    {
        if (n < 2)
        {
            return false;
        }

        foreach (var p in _witnesses)
        {
            if (n == p)
            {
                return true;
            }

            if (n % p == 0)
            {
                return false;
            }
        }

        var s = BitOperations.TrailingZeroCount(n - 1);
        var d = (n - 1) >> s;

        foreach (var a in _witnesses)
        {
            if (!PassesMillerRabin(n, a, d, s))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The smallest prime strictly greater than <paramref name="n"/>, or
    /// <see langword="null"/> when it would exceed <see cref="MaxValue"/>.</summary>
    public static ulong? NextPrime(ulong n)
    {
        if (n < 2)
        {
            return 2;
        }

        // Step over even candidates; prime gaps below 2^53 are tiny, so this terminates fast.
        var candidate = n + 1 | 1;
        while (candidate <= MaxValue)
        {
            if (IsPrime(candidate))
            {
                return candidate;
            }

            candidate += 2;
        }

        return null;
    }

    /// <summary>The largest prime strictly smaller than <paramref name="n"/>, or
    /// <see langword="null"/> when none exists (n ≤ 2).</summary>
    public static ulong? PreviousPrime(ulong n)
    {
        if (n <= 2)
        {
            return null;
        }

        if (n == 3)
        {
            return 2;
        }

        var candidate = (n - 1) | 1;
        if (candidate == n)
        {
            candidate -= 2;
        }

        while (candidate >= 3)
        {
            if (IsPrime(candidate))
            {
                return candidate;
            }

            candidate -= 2;
        }

        return 2;
    }

    /// <summary>The prime closest to <paramref name="n"/> (itself when prime); on a tie the
    /// smaller prime wins.</summary>
    public static ulong NearestPrime(ulong n)
    {
        if (IsPrime(n))
        {
            return n;
        }

        var previous = PreviousPrime(n);
        var next = NextPrime(n);

        return (previous, next) switch
        {
            (null, { } up) => up,
            ({ } down, null) => down,
            ({ } down, { } up) => n - down <= up - n ? down : up,
            _ => throw new ArgumentOutOfRangeException(nameof(n), n, "No prime in range."),
        };
    }

    /// <summary>Prime factorization of <paramref name="n"/> (2..2^53), ordered by prime,
    /// with exponents merged.</summary>
    public static IReadOnlyList<PrimeFactor> Factorize(ulong n)
    {
        if (n is < 2 or > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, $"Value must be between 2 and {MaxValue}.");
        }

        SortedDictionary<ulong, int> counts = new();

        foreach (var p in _witnesses)
        {
            while (n % p == 0)
            {
                Increment(counts, p);
                n /= p;
            }
        }

        if (n > 1)
        {
            SplitFactor(n, counts);
        }

        return [.. counts.Select(pair => new PrimeFactor(pair.Key, pair.Value))];
    }

    private static void SplitFactor(ulong n, SortedDictionary<ulong, int> counts)
    {
        if (IsPrime(n))
        {
            Increment(counts, n);
            return;
        }

        var divisor = PollardRho(n);
        SplitFactor(divisor, counts);
        SplitFactor(n / divisor, counts);
    }

    private static void Increment(SortedDictionary<ulong, int> counts, ulong prime)
        => counts[prime] = counts.TryGetValue(prime, out var count) ? count + 1 : 1;

    /// <summary>Finds a non-trivial divisor of an odd composite with no factors ≤ 37.</summary>
    private static ulong PollardRho(ulong n)
    {
        for (ulong c = 1; ; c++)
        {
            ulong x = 2, y = 2, d = 1;

            while (d == 1)
            {
                x = Step(x);
                y = Step(Step(y));
                d = Gcd(x > y ? x - y : y - x, n);
            }

            if (d != n)
            {
                return d;
            }

            // The cycle collapsed (d == n) — retry with the next polynomial constant.
            ulong Step(ulong v) => (MulMod(v, v, n) + c) % n;
        }
    }

    private static ulong Gcd(ulong a, ulong b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }

    private static bool PassesMillerRabin(ulong n, ulong a, ulong d, int s)
    {
        var x = PowMod(a, d, n);
        if (x == 1 || x == n - 1)
        {
            return true;
        }

        for (var i = 1; i < s; i++)
        {
            x = MulMod(x, x, n);
            if (x == n - 1)
            {
                return true;
            }
        }

        return false;
    }

    private static ulong PowMod(ulong value, ulong exponent, ulong modulus)
    {
        var result = 1ul;
        value %= modulus;

        while (exponent > 0)
        {
            if ((exponent & 1) != 0)
            {
                result = MulMod(result, value, modulus);
            }

            value = MulMod(value, value, modulus);
            exponent >>= 1;
        }

        return result;
    }

    // UInt128 keeps a × b exact for any 64-bit operands — no overflow tricks needed.
    private static ulong MulMod(ulong a, ulong b, ulong modulus)
        => (ulong)((UInt128)a * b % modulus);
}
