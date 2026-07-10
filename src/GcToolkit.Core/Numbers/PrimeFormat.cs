using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Numbers;

/// <summary>Formats prime factorizations for display (e.g. 12 → "2² × 3").</summary>
public static class PrimeFormat
{
    private const string SuperscriptDigits = "⁰¹²³⁴⁵⁶⁷⁸⁹";

    public static string FormatFactorization(IEnumerable<PrimeFactor> factors)
        => string.Join(" × ", factors.Select(FormatFactor));

    private static string FormatFactor(PrimeFactor factor)
    {
        var prime = factor.Prime.ToString(CultureInfo.InvariantCulture);
        return factor.Exponent == 1 ? prime : prime + ToSuperscript(factor.Exponent);
    }

    private static string ToSuperscript(int exponent)
    {
        StringBuilder builder = new();
        foreach (var digit in exponent.ToString(CultureInfo.InvariantCulture))
        {
            builder.Append(SuperscriptDigits[digit - '0']);
        }

        return builder.ToString();
    }
}
