using System.Globalization;

namespace GcToolkit.Core.Alphabets;

/// <summary>The colour bands an axial resistor can carry (IEC 60062 electronic colour code).</summary>
public enum ResistorColor
{
    Black,
    Brown,
    Red,
    Orange,
    Yellow,
    Green,
    Blue,
    Violet,
    Grey,
    White,
    Gold,
    Silver,

    /// <summary>The absence of a band — only meaningful in the tolerance position (±20%).</summary>
    None,
}

/// <summary>How many colour bands the resistor has.</summary>
public enum ResistorBandCount
{
    /// <summary>digit, digit, multiplier, tolerance.</summary>
    Four = 4,

    /// <summary>digit, digit, digit, multiplier, tolerance.</summary>
    Five = 5,

    /// <summary>digit, digit, digit, multiplier, tolerance, temperature coefficient.</summary>
    Six = 6,
}

/// <summary>
/// The decoded electrical properties of a resistor: its nominal <see cref="Resistance"/> in ohms, its
/// <see cref="TolerancePercent"/>, and (6-band only) its <see cref="TemperatureCoefficient"/> in ppm/K.
/// </summary>
public sealed record ResistorValue(decimal Resistance, decimal TolerancePercent, int? TemperatureCoefficient);

/// <summary>The outcome of decoding colour bands: either a <see cref="Value"/> or an error reason.</summary>
public readonly record struct ResistorDecodeResult(bool Success, ResistorValue? Value, ResistorError Error)
{
    public static ResistorDecodeResult Ok(ResistorValue value) => new(true, value, ResistorError.None);

    public static ResistorDecodeResult Fail(ResistorError error) => new(false, null, error);
}

/// <summary>
/// The outcome of encoding a value: either the <see cref="Bands"/> or an error reason.
/// <see cref="IsApproximate"/> is set when the requested value has no exact band representation and
/// the closest representable one was produced instead.
/// </summary>
public readonly record struct ResistorEncodeResult(
    bool Success,
    IReadOnlyList<ResistorColor>? Bands,
    ResistorError Error,
    bool IsApproximate = false)
{
    public static ResistorEncodeResult Ok(IReadOnlyList<ResistorColor> bands, bool isApproximate = false)
        => new(true, bands, ResistorError.None, isApproximate);

    public static ResistorEncodeResult Fail(ResistorError error) => new(false, null, error);
}

/// <summary>Why a decode/encode failed, so the UI can show a specific message.</summary>
public enum ResistorError
{
    None,
    WrongBandCount,
    InvalidDigitBand,
    InvalidMultiplierBand,
    InvalidToleranceBand,
    InvalidTemperatureCoefficientBand,
    ValueOutOfRange,
    UnrepresentableValue,
    UnrepresentableTolerance,
    UnrepresentableTemperatureCoefficient,
}

/// <summary>
/// A pure, stateless translator for the resistor colour code. Decodes 4-, 5- and 6-band colour
/// sequences into resistance / tolerance / temperature-coefficient, and — beyond the
/// geocachingtoolbox.com tool, which only decodes — encodes a resistance value (with band count and
/// tolerance) back into the canonical colour bands. The colour tables follow IEC 60062 (verified
/// against Wikipedia's "Electronic color code").
/// </summary>
public sealed class ResistorCode
{
    /// <summary>Significant-figure digit (0–9) for each colour; black=0 … white=9.</summary>
    private static readonly IReadOnlyDictionary<ResistorColor, int> Digits = new Dictionary<ResistorColor, int>
    {
        [ResistorColor.Black] = 0,
        [ResistorColor.Brown] = 1,
        [ResistorColor.Red] = 2,
        [ResistorColor.Orange] = 3,
        [ResistorColor.Yellow] = 4,
        [ResistorColor.Green] = 5,
        [ResistorColor.Blue] = 6,
        [ResistorColor.Violet] = 7,
        [ResistorColor.Grey] = 8,
        [ResistorColor.White] = 9,
    };

    /// <summary>Multiplier (power of ten) for each colour, including the fractional gold/silver bands.</summary>
    private static readonly IReadOnlyDictionary<ResistorColor, decimal> Multipliers = new Dictionary<ResistorColor, decimal>
    {
        [ResistorColor.Silver] = 0.01m,
        [ResistorColor.Gold] = 0.1m,
        [ResistorColor.Black] = 1m,
        [ResistorColor.Brown] = 10m,
        [ResistorColor.Red] = 100m,
        [ResistorColor.Orange] = 1_000m,
        [ResistorColor.Yellow] = 10_000m,
        [ResistorColor.Green] = 100_000m,
        [ResistorColor.Blue] = 1_000_000m,
        [ResistorColor.Violet] = 10_000_000m,
        [ResistorColor.Grey] = 100_000_000m,
        [ResistorColor.White] = 1_000_000_000m,
    };

    /// <summary>Tolerance (±%) for each colour that can occupy the tolerance band.</summary>
    private static readonly IReadOnlyDictionary<ResistorColor, decimal> Tolerances = new Dictionary<ResistorColor, decimal>
    {
        [ResistorColor.Brown] = 1m,
        [ResistorColor.Red] = 2m,
        [ResistorColor.Green] = 0.5m,
        [ResistorColor.Blue] = 0.25m,
        [ResistorColor.Violet] = 0.1m,
        [ResistorColor.Grey] = 0.05m,
        [ResistorColor.Gold] = 5m,
        [ResistorColor.Silver] = 10m,
        [ResistorColor.None] = 20m,
    };

    /// <summary>Temperature coefficient (ppm/K) for each colour that can occupy the 6th band.</summary>
    private static readonly IReadOnlyDictionary<ResistorColor, int> TemperatureCoefficients = new Dictionary<ResistorColor, int>
    {
        [ResistorColor.Black] = 250,
        [ResistorColor.Brown] = 100,
        [ResistorColor.Red] = 50,
        [ResistorColor.Orange] = 15,
        [ResistorColor.Yellow] = 25,
        [ResistorColor.Green] = 20,
        [ResistorColor.Blue] = 10,
        [ResistorColor.Violet] = 5,
        [ResistorColor.Grey] = 1,
    };

    /// <summary>Colours valid as a significant digit, in digit order (black=0 … white=9).</summary>
    public static IReadOnlyList<ResistorColor> DigitColors { get; } =
        [.. Digits.OrderBy(p => p.Value).Select(p => p.Key)];

    /// <summary>Colours valid as the multiplier band, ascending by multiplier (silver … white).</summary>
    public static IReadOnlyList<ResistorColor> MultiplierColors { get; } =
        [.. Multipliers.OrderBy(p => p.Value).Select(p => p.Key)];

    /// <summary>Colours valid as the tolerance band, ascending by tolerance (blue … none).</summary>
    public static IReadOnlyList<ResistorColor> ToleranceColors { get; } =
        [.. Tolerances.OrderBy(p => p.Value).Select(p => p.Key)];

    /// <summary>Colours valid as the temperature-coefficient band, ascending by ppm/K (grey … black).</summary>
    public static IReadOnlyList<ResistorColor> TemperatureCoefficientColors { get; } =
        [.. TemperatureCoefficients.OrderBy(p => p.Value).Select(p => p.Key)];

    /// <summary>The significant-figure digit (0–9) for <paramref name="color"/>, or −1 if it has none.</summary>
    public static int DigitValue(ResistorColor color) => Digits.TryGetValue(color, out var d) ? d : -1;

    /// <summary>The multiplier for <paramref name="color"/>, or <see langword="null"/> if it is not a multiplier colour.</summary>
    public static decimal? MultiplierValue(ResistorColor color) => Multipliers.TryGetValue(color, out var m) ? m : null;

    /// <summary>The tolerance (±%) for <paramref name="color"/>, or <see langword="null"/> if it is not a tolerance colour.</summary>
    public static decimal? ToleranceValue(ResistorColor color) => Tolerances.TryGetValue(color, out var t) ? t : null;

    /// <summary>The temperature coefficient (ppm/K) for <paramref name="color"/>, or <see langword="null"/> if it has none.</summary>
    public static int? TemperatureCoefficientValue(ResistorColor color)
        => TemperatureCoefficients.TryGetValue(color, out var c) ? c : null;

    /// <summary>
    /// A representative <c>#RRGGBB</c> swatch for each band colour, so a UI can paint a colour chip
    /// without knowing the palette. "None" maps to a transparent-looking light grey placeholder.
    /// </summary>
    public static string SwatchHex(ResistorColor color) => color switch
    {
        ResistorColor.Black => "#1A1A1A",
        ResistorColor.Brown => "#7A4A1E",
        ResistorColor.Red => "#D32F2F",
        ResistorColor.Orange => "#F57C00",
        ResistorColor.Yellow => "#FBC02D",
        ResistorColor.Green => "#388E3C",
        ResistorColor.Blue => "#1976D2",
        ResistorColor.Violet => "#7B1FA2",
        ResistorColor.Grey => "#9E9E9E",
        ResistorColor.White => "#FAFAFA",
        ResistorColor.Gold => "#C9A227",
        ResistorColor.Silver => "#C0C0C0",
        _ => "#E0E0E0",
    };

    /// <summary>The number of significant-digit bands for <paramref name="count"/> (two for 4-band, three otherwise).</summary>
    public static int DigitBandCount(ResistorBandCount count) => count == ResistorBandCount.Four ? 2 : 3;

    /// <summary>
    /// Decodes a colour-band sequence. The length determines the layout (4/5/6 bands); any colour that
    /// cannot occupy its position yields a typed failure rather than throwing.
    /// </summary>
    public ResistorDecodeResult Decode(IReadOnlyList<ResistorColor> bands)
    {
        if (bands is null || !IsKnownBandCount(bands.Count, out var count))
        {
            return ResistorDecodeResult.Fail(ResistorError.WrongBandCount);
        }

        var digitCount = DigitBandCount(count);

        long significant = 0;
        for (var i = 0; i < digitCount; i++)
        {
            var digit = DigitValue(bands[i]);
            if (digit < 0)
            {
                return ResistorDecodeResult.Fail(ResistorError.InvalidDigitBand);
            }

            significant = significant * 10 + digit;
        }

        if (MultiplierValue(bands[digitCount]) is not decimal multiplier)
        {
            return ResistorDecodeResult.Fail(ResistorError.InvalidMultiplierBand);
        }

        if (ToleranceValue(bands[digitCount + 1]) is not decimal tolerance)
        {
            return ResistorDecodeResult.Fail(ResistorError.InvalidToleranceBand);
        }

        int? tempco = null;
        if (count == ResistorBandCount.Six)
        {
            if (TemperatureCoefficientValue(bands[5]) is not int ppm)
            {
                return ResistorDecodeResult.Fail(ResistorError.InvalidTemperatureCoefficientBand);
            }

            tempco = ppm;
        }

        var resistance = significant * multiplier;
        return ResistorDecodeResult.Ok(new ResistorValue(resistance, tolerance, tempco));
    }

    /// <summary>
    /// Encodes <paramref name="resistance"/> ohms into colour bands for the requested
    /// <paramref name="bandCount"/> and <paramref name="tolerancePercent"/>. The value is rounded to the
    /// available number of significant digits (2 for 4-band, 3 otherwise) and scaled by a power-of-ten
    /// multiplier; values that can't be represented (≤0, no matching tolerance/tempco colour, or out of
    /// multiplier range) return a typed failure.
    /// </summary>
    public ResistorEncodeResult Encode(
        decimal resistance,
        ResistorBandCount bandCount,
        decimal tolerancePercent,
        int? temperatureCoefficient = null)
    {
        if (resistance <= 0m)
        {
            return ResistorEncodeResult.Fail(ResistorError.ValueOutOfRange);
        }

        if (ColorForTolerance(tolerancePercent) is not ResistorColor toleranceColor)
        {
            return ResistorEncodeResult.Fail(ResistorError.UnrepresentableTolerance);
        }

        ResistorColor? tempcoColor = null;
        if (bandCount == ResistorBandCount.Six)
        {
            // Default to the most common 50 ppm/K (red) when the caller doesn't specify one.
            var ppm = temperatureCoefficient ?? 50;
            if (ColorForTemperatureCoefficient(ppm) is not ResistorColor tc)
            {
                return ResistorEncodeResult.Fail(ResistorError.UnrepresentableTemperatureCoefficient);
            }

            tempcoColor = tc;
        }

        var digitCount = DigitBandCount(bandCount);
        if (!TryGetSignificantAndExponent(resistance, digitCount, out var significant, out var exponent, out var isApproximate))
        {
            return ResistorEncodeResult.Fail(ResistorError.UnrepresentableValue);
        }

        if (ColorForMultiplierExponent(exponent) is not ResistorColor multiplierColor)
        {
            return ResistorEncodeResult.Fail(ResistorError.UnrepresentableValue);
        }

        var bands = new List<ResistorColor>(bandCount == ResistorBandCount.Six ? 6 : (int)bandCount);

        // Significant digits, most-significant first (zero-padded to the band count).
        var digitsText = significant.ToString(CultureInfo.InvariantCulture).PadLeft(digitCount, '0');
        for (var i = 0; i < digitCount; i++)
        {
            bands.Add(DigitColors[digitsText[i] - '0']);
        }

        bands.Add(multiplierColor);
        bands.Add(toleranceColor);
        if (tempcoColor is ResistorColor c)
        {
            bands.Add(c);
        }

        return ResistorEncodeResult.Ok(bands, isApproximate);
    }

    /// <summary>
    /// The significant digits of <paramref name="bands"/> concatenated as they appear on the resistor
    /// (brown-black-red → <c>102</c>) — what a cache puzzle usually wants, rather than the ohm value.
    /// Returns an empty string when the band count or a digit colour is invalid.
    /// </summary>
    public static string DigitString(IReadOnlyList<ResistorColor> bands)
    {
        if (bands is null || !IsKnownBandCount(bands.Count, out var count))
        {
            return string.Empty;
        }

        var digitCount = DigitBandCount(count);
        var digits = new char[digitCount];
        for (var i = 0; i < digitCount; i++)
        {
            var digit = DigitValue(bands[i]);
            if (digit < 0)
            {
                return string.Empty;
            }

            digits[i] = (char)('0' + digit);
        }

        return new string(digits);
    }

    /// <summary>
    /// Formats an ohm value with an SI prefix and a thin no-break separator (e.g. <c>4.7 kΩ</c>,
    /// <c>0.47 Ω</c>, <c>2.2 MΩ</c>). Trailing zeros are trimmed; the value is shown in the
    /// invariant culture so it round-trips regardless of UI locale.
    /// </summary>
    public static string FormatResistance(decimal ohms)
    {
        (decimal scaled, string prefix) = ohms switch
        {
            >= 1_000_000_000m => (ohms / 1_000_000_000m, "G"),
            >= 1_000_000m => (ohms / 1_000_000m, "M"),
            >= 1_000m => (ohms / 1_000m, "k"),
            _ => (ohms, ""),
        };

        var number = scaled.ToString("0.###", CultureInfo.InvariantCulture);
        return $"{number} {prefix}Ω"; // U+03A9 GREEK CAPITAL LETTER OMEGA
    }

    private static bool IsKnownBandCount(int length, out ResistorBandCount count)
    {
        count = (ResistorBandCount)length;
        return length is 4 or 5 or 6;
    }

    private static ResistorColor? ColorForTolerance(decimal percent)
    {
        foreach (var (color, value) in Tolerances)
        {
            if (value == percent)
            {
                return color;
            }
        }

        return null;
    }

    private static ResistorColor? ColorForTemperatureCoefficient(int ppm)
    {
        foreach (var (color, value) in TemperatureCoefficients)
        {
            if (value == ppm)
            {
                return color;
            }
        }

        return null;
    }

    /// <summary>Maps a power-of-ten exponent (−2 … 9) to its multiplier colour, or <see langword="null"/> if out of range.</summary>
    private static ResistorColor? ColorForMultiplierExponent(int exponent)
    {
        var target = exponent switch
        {
            < 0 => (decimal)Math.Pow(10, exponent),
            _ => Pow10(exponent),
        };

        foreach (var (color, value) in Multipliers)
        {
            if (value == target)
            {
                return color;
            }
        }

        return null;
    }

    /// <summary>
    /// Decomposes <paramref name="resistance"/> into an integer <paramref name="significant"/> of at most
    /// <paramref name="digitCount"/> digits times <c>10^<paramref name="exponent"/></c>, choosing the
    /// exponent so the significand uses the full digit width (preserving precision). An exact
    /// representation wins; otherwise the closest representable one is returned with
    /// <paramref name="isApproximate"/> set. Returns <see langword="false"/> only when no exponent in the
    /// supported multiplier range fits at all.
    /// </summary>
    private static bool TryGetSignificantAndExponent(
        decimal resistance,
        int digitCount,
        out long significant,
        out int exponent,
        out bool isApproximate)
    {
        significant = 0;
        exponent = 0;
        isApproximate = false;

        // Highest representable significand, e.g. 99 (4-band) or 999 (5/6-band).
        var maxSignificant = (long)Pow10(digitCount) - 1;
        var minSignificant = (long)Pow10(digitCount - 1); // keep the leading digit non-zero where possible

        long? fallbackSignificant = null;
        var fallbackExponent = 0;

        // Try multiplier exponents from smallest (silver, -2) to largest (white, 9).
        for (exponent = -2; exponent <= 9; exponent++)
        {
            var scale = exponent < 0 ? 1m / Pow10(-exponent) : Pow10(exponent);
            var candidate = resistance / scale;
            var rounded = Math.Round(candidate, MidpointRounding.AwayFromZero);

            if (rounded < 1 || rounded > maxSignificant)
            {
                continue;
            }

            // Prefer the representation that fills the digit width (leading digit non-zero) when possible,
            // but accept a smaller significand at the lowest exponent (e.g. single-digit values).
            if (rounded < minSignificant && exponent != -2)
            {
                continue;
            }

            // An exact reconstruction always wins, so we never silently misreport a representable value.
            if (rounded * scale == resistance)
            {
                significant = (long)rounded;
                return true;
            }

            // Otherwise remember the first (lowest-exponent, so highest-precision) rounded candidate.
            if (fallbackSignificant is null)
            {
                fallbackSignificant = (long)rounded;
                fallbackExponent = exponent;
            }
        }

        if (fallbackSignificant is long value)
        {
            significant = value;
            exponent = fallbackExponent;
            isApproximate = true;
            return true;
        }

        return false;
    }

    private static decimal Pow10(int exponent)
    {
        decimal result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}
