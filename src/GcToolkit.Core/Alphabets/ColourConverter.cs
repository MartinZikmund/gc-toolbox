using System.Globalization;

namespace GcToolkit.Core.Alphabets;

/// <summary>
/// A colour in the canonical sRGB pipeline: integer Red/Green/Blue channels in <c>[0, 255]</c>.
/// Construction clamps each channel, so out-of-range inputs never throw. This is the single
/// source of truth from which every other colour model is derived (issue #42, geocachingtoolbox
/// "Colour conversion" parity).
/// </summary>
public readonly record struct RgbColour
{
    public RgbColour(int r, int g, int b)
    {
        R = Clamp(r);
        G = Clamp(g);
        B = Clamp(b);
    }

    public int R { get; }

    public int G { get; }

    public int B { get; }

    /// <summary>Upper-case <c>#RRGGBB</c> hex string.</summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public CmyColour ToCmy() => ColourConverter.ToCmy(this);

    public CmykColour ToCmyk() => ColourConverter.ToCmyk(this);

    public HslColour ToHsl() => ColourConverter.ToHsl(this);

    public HsvColour ToHsv() => ColourConverter.ToHsv(this);

    public HsiColour ToHsi() => ColourConverter.ToHsi(this);

    public YiqColour ToYiq() => ColourConverter.ToYiq(this);

    public YuvColour ToYuv() => ColourConverter.ToYuv(this);

    public YCbCrColour ToYCbCr() => ColourConverter.ToYCbCr(this);

    private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
}

/// <summary>Cyan / Magenta / Yellow, each a percentage in <c>[0, 100]</c>.</summary>
public readonly record struct CmyColour(double C, double M, double Y);

/// <summary>Cyan / Magenta / Yellow / Key(black), each a percentage in <c>[0, 100]</c>.</summary>
public readonly record struct CmykColour(double C, double M, double Y, double K);

/// <summary>Hue (degrees <c>[0, 360)</c>), Saturation and Lightness (percentages <c>[0, 100]</c>).</summary>
public readonly record struct HslColour(double H, double S, double L);

/// <summary>Hue (degrees), Saturation and Brightness/Value (percentages). HSB and HSV are the same model.</summary>
public readonly record struct HsvColour(double H, double S, double V);

/// <summary>Hue (degrees), Saturation and Intensity (percentages).</summary>
public readonly record struct HsiColour(double H, double S, double I);

/// <summary>
/// Rec.601 YIQ, with every component normalized to a percentage <c>[0, 100]</c> the way the
/// reference site presents it: Y from its <c>[0, 1]</c> range, and I/Q from their signed ranges
/// mapped so 0 sits at 50%.
/// </summary>
public readonly record struct YiqColour(double Y, double I, double Q);

/// <summary>Rec.601 YUV as percentages (Y from <c>[0, 1]</c>; U/V signed ranges mapped so 0 is 50%).</summary>
public readonly record struct YuvColour(double Y, double U, double V);

/// <summary>Rec.601 YCbCr as percentages (8-bit studio range mapped to <c>[0, 100]</c>).</summary>
public readonly record struct YCbCrColour(double Y, double Cb, double Cr);

/// <summary>
/// Pure, stateless colour-model conversions around a canonical sRGB centre (<see cref="RgbColour"/>,
/// 8-bit channels). Any model converts to RGB and back, so editing one model and recomputing the
/// rest is a round trip through RGB. Conventions match geocachingtoolbox.com "Colour conversion":
/// HSL/HSV/HSI use 0–360° hue + percentages; CMY/CMYK use percentages; YIQ/YUV/YCbCr use the
/// <b>Rec.601</b> (BT.601) luma/chroma matrices and are surfaced as percentages.
///
/// <para>Note: some conversions (notably CMYK → RGB) are not standardized and are device-dependent
/// in real colour-management systems; the naive formulae used here are deterministic and reversible
/// but may differ from a printer's profile.</para>
/// </summary>
public static class ColourConverter
{
    // ---- Hex ----

    /// <summary>
    /// Leniently parses a hex colour string. Accepts an optional leading <c>#</c> and either
    /// <c>RRGGBB</c> (6 hex digits) or the shorthand <c>RGB</c> (3 hex digits, each nibble doubled,
    /// so <c>#abc</c> == <c>#aabbcc</c>). Returns <see langword="false"/> for anything else.
    /// </summary>
    public static bool TryParseHex(string? hex, out RgbColour colour)
    {
        colour = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var s = hex.Trim();
        if (s.StartsWith('#'))
        {
            s = s[1..];
        }

        if (s.Length == 3)
        {
            // Expand each nibble: "abc" -> "aabbcc".
            s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        }

        if (s.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        colour = new RgbColour(r, g, b);
        return true;
    }

    // ---- CMY ----

    public static CmyColour ToCmy(RgbColour c)
        => new(
            (1.0 - c.R / 255.0) * 100.0,
            (1.0 - c.G / 255.0) * 100.0,
            (1.0 - c.B / 255.0) * 100.0);

    public static RgbColour FromCmy(CmyColour c)
        => new(
            ToByte((1.0 - Pct(c.C)) * 255.0),
            ToByte((1.0 - Pct(c.M)) * 255.0),
            ToByte((1.0 - Pct(c.Y)) * 255.0));

    // ---- CMYK ----

    public static CmykColour ToCmyk(RgbColour c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var k = 1.0 - Math.Max(r, Math.Max(g, b));
        if (k >= 1.0 - 1e-9)
        {
            // Pure black: chroma channels are undefined; report 0 as the site does.
            return new CmykColour(0, 0, 0, 100);
        }

        var cyan = (1.0 - r - k) / (1.0 - k);
        var magenta = (1.0 - g - k) / (1.0 - k);
        var yellow = (1.0 - b - k) / (1.0 - k);
        return new CmykColour(cyan * 100.0, magenta * 100.0, yellow * 100.0, k * 100.0);
    }

    public static RgbColour FromCmyk(CmykColour c)
    {
        var k = Pct(c.K);
        var r = (1.0 - Pct(c.C)) * (1.0 - k);
        var g = (1.0 - Pct(c.M)) * (1.0 - k);
        var b = (1.0 - Pct(c.Y)) * (1.0 - k);
        return new RgbColour(ToByte(r * 255.0), ToByte(g * 255.0), ToByte(b * 255.0));
    }

    // ---- HSL ----

    public static HslColour ToHsl(RgbColour c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2.0;
        var (h, _) = HueAndChroma(r, g, b, max, min);

        var delta = max - min;
        var s = delta <= 1e-9 ? 0.0 : delta / (1.0 - Math.Abs(2.0 * l - 1.0));
        return new HslColour(h, s * 100.0, l * 100.0);
    }

    public static RgbColour FromHsl(HslColour hsl)
    {
        var h = NormalizeHue(hsl.H);
        var s = Pct(hsl.S);
        var l = Pct(hsl.L);
        var chroma = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        var m = l - chroma / 2.0;
        return FromHueChroma(h, chroma, m);
    }

    // ---- HSV / HSB ----

    public static HsvColour ToHsv(RgbColour c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var (h, _) = HueAndChroma(r, g, b, max, min);
        var s = max <= 1e-9 ? 0.0 : (max - min) / max;
        return new HsvColour(h, s * 100.0, max * 100.0);
    }

    public static RgbColour FromHsv(HsvColour hsv)
    {
        var h = NormalizeHue(hsv.H);
        var s = Pct(hsv.S);
        var v = Pct(hsv.V);
        var chroma = v * s;
        var m = v - chroma;
        return FromHueChroma(h, chroma, m);
    }

    // ---- HSI ----

    public static HsiColour ToHsi(RgbColour c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var (h, _) = HueAndChroma(r, g, b, max, min);
        var i = (r + g + b) / 3.0;
        var s = i <= 1e-9 ? 0.0 : 1.0 - min / i;
        return new HsiColour(h, s * 100.0, i * 100.0);
    }

    public static RgbColour FromHsi(HsiColour hsi)
    {
        var h = NormalizeHue(hsi.H);
        var s = Pct(hsi.S);
        var i = Pct(hsi.I);

        // Standard HSI -> RGB by 120° sectors.
        var hRad = h * Math.PI / 180.0;
        double r, g, b;
        if (h < 120.0)
        {
            b = i * (1.0 - s);
            r = i * (1.0 + s * Math.Cos(hRad) / Math.Cos((60.0 - h) * Math.PI / 180.0));
            g = 3.0 * i - (r + b);
        }
        else if (h < 240.0)
        {
            var h2 = h - 120.0;
            r = i * (1.0 - s);
            g = i * (1.0 + s * Math.Cos(h2 * Math.PI / 180.0) / Math.Cos((60.0 - h2) * Math.PI / 180.0));
            b = 3.0 * i - (r + g);
        }
        else
        {
            var h2 = h - 240.0;
            g = i * (1.0 - s);
            b = i * (1.0 + s * Math.Cos(h2 * Math.PI / 180.0) / Math.Cos((60.0 - h2) * Math.PI / 180.0));
            r = 3.0 * i - (g + b);
        }

        return new RgbColour(ToByte(r * 255.0), ToByte(g * 255.0), ToByte(b * 255.0));
    }

    // ---- YIQ (Rec.601) — surfaced as percentages ----
    // I range ≈ [-0.5957, 0.5957], Q range ≈ [-0.5226, 0.5226]; mapped so 0 -> 50%.
    private const double IRange = 0.5957;
    private const double QRange = 0.5226;

    public static YiqColour ToYiq(RgbColour c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var y = 0.299 * r + 0.587 * g + 0.114 * b;
        var i = 0.595716 * r - 0.274453 * g - 0.321263 * b;
        var q = 0.211456 * r - 0.522591 * g + 0.311135 * b;
        return new YiqColour(y * 100.0, SignedToPct(i, IRange), SignedToPct(q, QRange));
    }

    public static RgbColour FromYiq(YiqColour c)
    {
        var y = Pct(c.Y);
        var i = PctToSigned(c.I, IRange);
        var q = PctToSigned(c.Q, QRange);
        var r = y + 0.9563 * i + 0.6210 * q;
        var g = y - 0.2721 * i - 0.6474 * q;
        var b = y - 1.1070 * i + 1.7046 * q;
        return new RgbColour(ToByte(r * 255.0), ToByte(g * 255.0), ToByte(b * 255.0));
    }

    // ---- YUV (Rec.601) — surfaced as percentages ----
    // U range ≈ [-0.436, 0.436], V range ≈ [-0.615, 0.615]; mapped so 0 -> 50%.
    private const double URange = 0.436;
    private const double VRange = 0.615;

    public static YuvColour ToYuv(RgbColour c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var y = 0.299 * r + 0.587 * g + 0.114 * b;
        var u = -0.14713 * r - 0.28886 * g + 0.436 * b;
        var v = 0.615 * r - 0.51499 * g - 0.10001 * b;
        return new YuvColour(y * 100.0, SignedToPct(u, URange), SignedToPct(v, VRange));
    }

    public static RgbColour FromYuv(YuvColour c)
    {
        var y = Pct(c.Y);
        var u = PctToSigned(c.U, URange);
        var v = PctToSigned(c.V, VRange);
        var r = y + 1.13983 * v;
        var g = y - 0.39465 * u - 0.58060 * v;
        var b = y + 2.03211 * u;
        return new RgbColour(ToByte(r * 255.0), ToByte(g * 255.0), ToByte(b * 255.0));
    }

    // ---- YCbCr (Rec.601, 8-bit studio range) — surfaced as percentages ----
    // Y' in [16, 235], Cb/Cr in [16, 240]; each mapped to [0, 100].

    public static YCbCrColour ToYCbCr(RgbColour c)
    {
        double r = c.R, g = c.G, b = c.B;
        var y = 16.0 + (65.481 * r + 128.553 * g + 24.966 * b) / 255.0;
        var cb = 128.0 + (-37.797 * r - 74.203 * g + 112.0 * b) / 255.0;
        var cr = 128.0 + (112.0 * r - 93.786 * g - 18.214 * b) / 255.0;
        return new YCbCrColour(
            (y - 16.0) / 219.0 * 100.0,
            (cb - 16.0) / 224.0 * 100.0,
            (cr - 16.0) / 224.0 * 100.0);
    }

    public static RgbColour FromYCbCr(YCbCrColour c)
    {
        var y = Pct(c.Y) * 219.0 + 16.0;
        var cb = Pct(c.Cb) * 224.0 + 16.0;
        var cr = Pct(c.Cr) * 224.0 + 16.0;
        var r = 255.0 / 219.0 * (y - 16.0) + 255.0 / 224.0 * 1.402 * (cr - 128.0);
        var g = 255.0 / 219.0 * (y - 16.0)
                - 255.0 / 224.0 * 1.772 * 0.114 / 0.587 * (cb - 128.0)
                - 255.0 / 224.0 * 1.402 * 0.299 / 0.587 * (cr - 128.0);
        var b = 255.0 / 219.0 * (y - 16.0) + 255.0 / 224.0 * 1.772 * (cb - 128.0);
        return new RgbColour(ToByte(r), ToByte(g), ToByte(b));
    }

    // ---- shared helpers ----

    /// <summary>Hue in degrees (0–360) plus chroma, from already-normalized [0,1] channels.</summary>
    private static (double Hue, double Chroma) HueAndChroma(double r, double g, double b, double max, double min)
    {
        var chroma = max - min;
        if (chroma <= 1e-9)
        {
            return (0.0, 0.0);
        }

        double h;
        if (max == r)
        {
            h = ((g - b) / chroma) % 6.0;
        }
        else if (max == g)
        {
            h = (b - r) / chroma + 2.0;
        }
        else
        {
            h = (r - g) / chroma + 4.0;
        }

        h *= 60.0;
        if (h < 0.0)
        {
            h += 360.0;
        }

        return (h, chroma);
    }

    /// <summary>Builds an RGB colour from a hue (deg), chroma and match value (HSL/HSV common tail).</summary>
    private static RgbColour FromHueChroma(double h, double chroma, double m)
    {
        var hp = h / 60.0;
        var x = chroma * (1.0 - Math.Abs(hp % 2.0 - 1.0));
        double r = 0, g = 0, b = 0;
        switch ((int)hp)
        {
            case 0: r = chroma; g = x; break;
            case 1: r = x; g = chroma; break;
            case 2: g = chroma; b = x; break;
            case 3: g = x; b = chroma; break;
            case 4: r = x; b = chroma; break;
            default: r = chroma; b = x; break;
        }

        return new RgbColour(ToByte((r + m) * 255.0), ToByte((g + m) * 255.0), ToByte((b + m) * 255.0));
    }

    private static double NormalizeHue(double h)
    {
        h %= 360.0;
        return h < 0.0 ? h + 360.0 : h;
    }

    /// <summary>Percentage [0,100] -> fraction [0,1], clamped.</summary>
    private static double Pct(double p) => Math.Clamp(p / 100.0, 0.0, 1.0);

    /// <summary>Maps a signed value in [-range, range] to a percentage [0,100] with 0 at 50%.</summary>
    private static double SignedToPct(double value, double range) => (value / range / 2.0 + 0.5) * 100.0;

    /// <summary>Inverse of <see cref="SignedToPct"/>.</summary>
    private static double PctToSigned(double pct, double range) => (Math.Clamp(pct, 0.0, 100.0) / 100.0 - 0.5) * 2.0 * range;

    private static int ToByte(double v) => (int)Math.Round(Math.Clamp(v, 0.0, 255.0), MidpointRounding.AwayFromZero);
}
