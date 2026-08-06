using System.IO.Compression;

namespace GcToolkit.Core.Numbers.GoldenRatio;

/// <summary>
/// Lazily loads the first 1,000,000 decimals of φ = (1+√5)/2 from a gzipped embedded resource.
/// The digits were generated offline as isqrt(5·10^(2·(1,000,000+100))) via two independent
/// algorithms (CPython <c>math.isqrt</c> and libmpdec <c>Decimal.sqrt</c>) and verified against
/// OEIS A001622 (first 99,999 decimals) and the University of Arizona 50,000-digit φ file, so the
/// tool works fully offline. Decompression runs once on the thread pool; the string is cached
/// process-wide (~2 MB).
/// </summary>
public sealed class GoldenRatioDigitsProvider
{
    public const int MaxDecimals = 1_000_000;

    private const string ResourceName = "GcToolkit.Core.Numbers.GoldenRatio.GoldenRatioDigits.gz";

    private static readonly Lazy<Task<string>> _decimals = new(
        () => Task.Run(LoadDecimals), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The decimals of φ ("6180…"), without the leading "1.".</summary>
    public Task<string> GetDecimalsAsync() => _decimals.Value;

    private static string LoadDecimals()
    {
        using var stream = typeof(GoldenRatioDigitsProvider).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{ResourceName}'.");
        using GZipStream gzip = new(stream, CompressionMode.Decompress);
        using StreamReader reader = new(gzip);

        var decimals = reader.ReadToEnd();
        return decimals.Length == MaxDecimals
            ? decimals
            : throw new InvalidOperationException(
                $"Expected {MaxDecimals} φ decimals but the embedded resource holds {decimals.Length}.");
    }
}
