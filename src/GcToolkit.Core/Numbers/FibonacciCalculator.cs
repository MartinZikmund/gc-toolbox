using System.Numerics;

namespace GcToolkit.Core.Numbers;

/// <summary>One Fibonacci number together with its zero-based position.</summary>
public readonly record struct FibonacciEntry(int Index, BigInteger Value);

/// <summary>
/// Result of looking a value up in the Fibonacci sequence. For members <see cref="Index"/> is the
/// first occurrence (the duplicated value 1 reports F(1), not F(2)); for non-members
/// <see cref="Below"/>/<see cref="Above"/> are the nearest sequence neighbors.
/// </summary>
public sealed record FibonacciLookup(bool IsMember, int Index, FibonacciEntry? Below, FibonacciEntry? Above);

/// <summary>
/// Computes Fibonacci numbers with zero-based indexing (F(0) = 0, F(1) = 1) up to
/// <see cref="MaxIndex"/>, using the fast-doubling identities with memoization so even
/// F(100,000) (~20,899 digits) resolves in milliseconds.
/// </summary>
public sealed class FibonacciCalculator
{
    public const int MaxIndex = 100_000;

    /// <summary>Decimal digits of F(<see cref="MaxIndex"/>) — the largest digit count any member has.</summary>
    public const int MaxDigitCount = 20_899;

    // F(2) must be seeded too: Fib(2) would otherwise recurse into itself (k = 1, k + 1 = 2).
    private readonly Dictionary<int, BigInteger> _cache = new() { [0] = BigInteger.Zero, [1] = BigInteger.One, [2] = BigInteger.One };
    private readonly Lock _gate = new();

    /// <summary>Returns F(<paramref name="index"/>).</summary>
    public BigInteger At(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, MaxIndex);

        lock (_gate)
        {
            return Fib(index);
        }
    }

    /// <summary>Returns the entries from F(<paramref name="fromIndex"/>) through F(<paramref name="toIndex"/>), inclusive.</summary>
    public IReadOnlyList<FibonacciEntry> Range(int fromIndex, int toIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fromIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(toIndex, MaxIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(toIndex, fromIndex);

        // Seed with fast doubling, then walk forward — one addition per entry.
        var current = At(fromIndex);
        var next = fromIndex + 1 <= MaxIndex ? At(fromIndex + 1) : BigInteger.Zero;

        List<FibonacciEntry> entries = new(toIndex - fromIndex + 1);
        for (var i = fromIndex; i <= toIndex; i++)
        {
            entries.Add(new FibonacciEntry(i, current));
            (current, next) = (next, current + next);
        }

        return entries;
    }

    /// <summary>
    /// Locates <paramref name="value"/> in the sequence. Returns <see langword="null"/> when the value
    /// is negative or exceeds F(<see cref="MaxIndex"/>); otherwise reports membership with the index of
    /// the first occurrence, or the nearest neighbors for a non-member.
    /// </summary>
    public FibonacciLookup? Locate(BigInteger value)
    {
        if (value.Sign < 0 || value > At(MaxIndex))
        {
            return null;
        }

        if (value.IsZero)
        {
            return new FibonacciLookup(true, 0, null, null);
        }

        if (value.IsOne)
        {
            return new FibonacciLookup(true, 1, null, null);
        }

        // From F(3) = 2 on the sequence is strictly increasing — binary search the first index
        // whose value is >= the target.
        var lo = 3;
        var hi = MaxIndex;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (At(mid) < value)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return At(lo) == value
            ? new FibonacciLookup(true, lo, null, null)
            : new FibonacciLookup(
                false,
                -1,
                new FibonacciEntry(lo - 1, At(lo - 1)),
                new FibonacciEntry(lo, At(lo)));
    }

    /// <summary>Returns every member with exactly <paramref name="digitCount"/> decimal digits (F(0) = 0 counts as one digit).</summary>
    public IReadOnlyList<FibonacciEntry> WithDigitCount(int digitCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digitCount, 1);

        if (digitCount > MaxDigitCount)
        {
            return [];
        }

        // Digit count never decreases along the sequence — binary search the first index reaching it.
        var lo = 0;
        var hi = MaxIndex;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (GetDigitCount(At(mid)) < digitCount)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        if (GetDigitCount(At(lo)) != digitCount)
        {
            return [];
        }

        List<FibonacciEntry> entries = [];
        foreach (var entry in Range(lo, Math.Min(lo + 64, MaxIndex)))
        {
            if (GetDigitCount(entry.Value) != digitCount)
            {
                break;
            }

            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>Decimal digit count of a non-negative value (0 has one digit). Exact — no
    /// floating-point rounding near powers of ten.</summary>
    public static int GetDigitCount(BigInteger value) => value.ToString().Length;

    private BigInteger Fib(int n)
    {
        if (_cache.TryGetValue(n, out var cached))
        {
            return cached;
        }

        // Fast doubling: F(2k) = F(k)·(2·F(k+1) − F(k)); F(2k+1) = F(k)² + F(k+1)².
        var k = n / 2;
        var fk = Fib(k);
        var fk1 = Fib(k + 1);
        var result = n % 2 == 0
            ? fk * ((fk1 << 1) - fk)
            : fk * fk + fk1 * fk1;

        _cache[n] = result;
        return result;
    }
}
