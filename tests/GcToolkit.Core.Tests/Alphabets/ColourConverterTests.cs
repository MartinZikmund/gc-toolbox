using GcToolkit.Core.Alphabets;

namespace GcToolkit.Core.Tests.Alphabets;

[TestClass]
public class ColourConverterTests
{
    private const double Tol = 0.6;

    // ---- Hex parsing ----

    [DataTestMethod]
    [DataRow("#FF8000", 255, 128, 0)]
    [DataRow("FF8000", 255, 128, 0)]
    [DataRow("#ffffff", 255, 255, 255)]
    [DataRow("#000000", 0, 0, 0)]
    public void TryParseHex_FullForm_ParsesChannels(string hex, int r, int g, int b)
    {
        Assert.IsTrue(ColourConverter.TryParseHex(hex, out var rgb));
        Assert.AreEqual(r, rgb.R);
        Assert.AreEqual(g, rgb.G);
        Assert.AreEqual(b, rgb.B);
    }

    [DataTestMethod]
    [DataRow("#abc", 0xAA, 0xBB, 0xCC)]
    [DataRow("abc", 0xAA, 0xBB, 0xCC)]
    [DataRow("#0f0", 0x00, 0xFF, 0x00)]
    public void TryParseHex_ShorthandThreeDigit_ExpandsEachNibble(string hex, int r, int g, int b)
    {
        Assert.IsTrue(ColourConverter.TryParseHex(hex, out var rgb));
        Assert.AreEqual(r, rgb.R);
        Assert.AreEqual(g, rgb.G);
        Assert.AreEqual(b, rgb.B);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("#12")]
    [DataRow("#GGGGGG")]
    [DataRow("#12345")]
    [DataRow("xyz")]
    public void TryParseHex_Invalid_ReturnsFalse(string hex)
        => Assert.IsFalse(ColourConverter.TryParseHex(hex, out _));

    [TestMethod]
    public void ToHex_FormatsUppercaseWithHash()
        => Assert.AreEqual("#FF8000", new RgbColour(255, 128, 0).ToHex());

    [TestMethod]
    public void ToHex_PadsSingleDigitChannels()
        => Assert.AreEqual("#0A0B0C", new RgbColour(10, 11, 12).ToHex());

    // ---- Channel clamping ----

    [DataTestMethod]
    [DataRow(-5, 0)]
    [DataRow(300, 255)]
    [DataRow(128, 128)]
    public void RgbColour_Construction_ClampsChannels(int input, int expected)
    {
        var c = new RgbColour(input, input, input);
        Assert.AreEqual(expected, c.R);
        Assert.AreEqual(expected, c.G);
        Assert.AreEqual(expected, c.B);
    }

    // ---- HSL known values ----

    [DataTestMethod]
    [DataRow(255, 0, 0, 0.0, 100.0, 50.0)]      // red
    [DataRow(0, 255, 0, 120.0, 100.0, 50.0)]    // green
    [DataRow(0, 0, 255, 240.0, 100.0, 50.0)]    // blue
    [DataRow(255, 255, 255, 0.0, 0.0, 100.0)]   // white
    [DataRow(0, 0, 0, 0.0, 0.0, 0.0)]           // black
    public void ToHsl_PrimaryColours_MatchKnownValues(int r, int g, int b, double h, double s, double l)
    {
        var hsl = new RgbColour(r, g, b).ToHsl();
        Assert.AreEqual(h, hsl.H, Tol, "H");
        Assert.AreEqual(s, hsl.S, Tol, "S");
        Assert.AreEqual(l, hsl.L, Tol, "L");
    }

    // ---- HSV known values ----

    [DataTestMethod]
    [DataRow(255, 0, 0, 0.0, 100.0, 100.0)]     // red
    [DataRow(0, 255, 0, 120.0, 100.0, 100.0)]   // green
    [DataRow(0, 0, 255, 240.0, 100.0, 100.0)]   // blue
    [DataRow(255, 255, 255, 0.0, 0.0, 100.0)]   // white
    [DataRow(0, 0, 0, 0.0, 0.0, 0.0)]           // black
    public void ToHsv_PrimaryColours_MatchKnownValues(int r, int g, int b, double h, double s, double v)
    {
        var hsv = new RgbColour(r, g, b).ToHsv();
        Assert.AreEqual(h, hsv.H, Tol, "H");
        Assert.AreEqual(s, hsv.S, Tol, "S");
        Assert.AreEqual(v, hsv.V, Tol, "V");
    }

    // ---- CMYK known values ----

    [DataTestMethod]
    [DataRow(255, 0, 0, 0.0, 100.0, 100.0, 0.0)]     // red
    [DataRow(0, 255, 0, 100.0, 0.0, 100.0, 0.0)]     // green
    [DataRow(0, 0, 255, 100.0, 100.0, 0.0, 0.0)]     // blue
    [DataRow(255, 255, 255, 0.0, 0.0, 0.0, 0.0)]     // white
    [DataRow(0, 0, 0, 0.0, 0.0, 0.0, 100.0)]         // black
    public void ToCmyk_PrimaryColours_MatchKnownValues(int r, int g, int b, double c, double m, double y, double k)
    {
        var cmyk = new RgbColour(r, g, b).ToCmyk();
        Assert.AreEqual(c, cmyk.C, Tol, "C");
        Assert.AreEqual(m, cmyk.M, Tol, "M");
        Assert.AreEqual(y, cmyk.Y, Tol, "Y");
        Assert.AreEqual(k, cmyk.K, Tol, "K");
    }

    // ---- CMY known values ----

    [DataTestMethod]
    [DataRow(255, 0, 0, 0.0, 100.0, 100.0)]      // red
    [DataRow(255, 255, 255, 0.0, 0.0, 0.0)]      // white
    [DataRow(0, 0, 0, 100.0, 100.0, 100.0)]      // black
    public void ToCmy_PrimaryColours_MatchKnownValues(int r, int g, int b, double c, double m, double y)
    {
        var cmy = new RgbColour(r, g, b).ToCmy();
        Assert.AreEqual(c, cmy.C, Tol, "C");
        Assert.AreEqual(m, cmy.M, Tol, "M");
        Assert.AreEqual(y, cmy.Y, Tol, "Y");
    }

    // ---- Round-trips through each model (RGB -> model -> RGB) ----

    private static readonly RgbColour[] Samples =
    [
        new(255, 128, 0),
        new(18, 52, 86),
        new(123, 200, 75),
        new(255, 255, 255),
        new(0, 0, 0),
        new(64, 64, 64),
        new(200, 50, 150),
    ];

    [TestMethod]
    public void HslRoundTrip_PreservesRgb()
    {
        foreach (var c in Samples)
        {
            var back = ColourConverter.FromHsl(c.ToHsl());
            AssertClose(c, back, "HSL");
        }
    }

    [TestMethod]
    public void HsvRoundTrip_PreservesRgb()
    {
        foreach (var c in Samples)
        {
            var back = ColourConverter.FromHsv(c.ToHsv());
            AssertClose(c, back, "HSV");
        }
    }

    [TestMethod]
    public void HsiRoundTrip_PreservesRgb()
    {
        foreach (var c in Samples)
        {
            var back = ColourConverter.FromHsi(c.ToHsi());
            AssertClose(c, back, "HSI", 2);
        }
    }

    [TestMethod]
    public void CmykRoundTrip_PreservesRgb()
    {
        foreach (var c in Samples)
        {
            var back = ColourConverter.FromCmyk(c.ToCmyk());
            AssertClose(c, back, "CMYK");
        }
    }

    [TestMethod]
    public void CmyRoundTrip_PreservesRgb()
    {
        foreach (var c in Samples)
        {
            var back = ColourConverter.FromCmy(c.ToCmy());
            AssertClose(c, back, "CMY");
        }
    }

    [TestMethod]
    public void YiqRoundTrip_PreservesRgb()
    {
        foreach (var c in Samples)
        {
            var back = ColourConverter.FromYiq(c.ToYiq());
            AssertClose(c, back, "YIQ", 2);
        }
    }

    [TestMethod]
    public void YuvRoundTrip_PreservesRgb()
    {
        foreach (var c in Samples)
        {
            var back = ColourConverter.FromYuv(c.ToYuv());
            AssertClose(c, back, "YUV", 2);
        }
    }

    [TestMethod]
    public void YCbCrRoundTrip_PreservesRgb()
    {
        foreach (var c in Samples)
        {
            var back = ColourConverter.FromYCbCr(c.ToYCbCr());
            AssertClose(c, back, "YCbCr", 2);
        }
    }

    // ---- YCbCr known value (Rec.601): white maps to Y=235, Cb=Cr=128 in 8-bit; as % Y=100 ----

    [TestMethod]
    public void ToYCbCr_White_HasFullLumaAndNeutralChroma()
    {
        var yc = new RgbColour(255, 255, 255).ToYCbCr();
        Assert.AreEqual(100.0, yc.Y, Tol, "Y");
        Assert.AreEqual(50.0, yc.Cb, Tol, "Cb");
        Assert.AreEqual(50.0, yc.Cr, Tol, "Cr");
    }

    [TestMethod]
    public void ToYCbCr_Black_HasZeroLumaAndNeutralChroma()
    {
        var yc = new RgbColour(0, 0, 0).ToYCbCr();
        Assert.AreEqual(0.0, yc.Y, Tol, "Y");
        Assert.AreEqual(50.0, yc.Cb, Tol, "Cb");
        Assert.AreEqual(50.0, yc.Cr, Tol, "Cr");
    }

    private static void AssertClose(RgbColour expected, RgbColour actual, string model, int delta = 1)
    {
        Assert.IsTrue(Math.Abs(expected.R - actual.R) <= delta, $"{model} R: {expected.R} vs {actual.R}");
        Assert.IsTrue(Math.Abs(expected.G - actual.G) <= delta, $"{model} G: {expected.G} vs {actual.G}");
        Assert.IsTrue(Math.Abs(expected.B - actual.B) <= delta, $"{model} B: {expected.B} vs {actual.B}");
    }
}
