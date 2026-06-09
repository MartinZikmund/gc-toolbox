namespace GcToolkit.Core.Numbers.Pi;

/// <summary>
/// Loads the embedded 1,000,000 decimals of π (verified against three independent sources:
/// a Chudnovsky computation, angio.net and Princeton's pi-10million.txt). The string is read
/// off the calling thread on first use and cached for the process lifetime.
/// </summary>
public sealed class PiDigitsProvider
{
    /// <summary>How many decimals of π the embedded resource carries.</summary>
    public const int DecimalCount = 1_000_000;

    private const string ResourceName = "GcToolkit.Core.Numbers.Pi.PiDecimals.txt";

    private static readonly Lazy<Task<string>> _decimals = new(() => Task.Run(Load));

    /// <summary>Returns the decimals of π (position 1 = first digit after the decimal point).</summary>
    public Task<string> GetDecimalsAsync() => _decimals.Value;

    private static string Load()
    {
        using var stream = typeof(PiDigitsProvider).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().Trim();
    }
}
