using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Colour conversion (issue #42). Holds a single canonical sRGB colour (<see cref="RgbColour"/>) and
/// exposes it across all ten geocachingtoolbox.com models — Hexadecimal, RGB, CMY, CMYK, HSL,
/// HSB/HSV, HSI, YIQ, YUV, YCbCr — as a collection of <see cref="ColourModelCard"/>s the View renders
/// in a responsive wrapping grid. Editing any field re-derives the canonical colour from that model
/// and rewrites every other card (any-to-any conversion); a suppression flag breaks the resulting
/// property-change feedback loop. All maths live in the pure <see cref="ColourConverter"/>
/// (thin-VM convention). The View binds <see cref="SwatchArgb"/> for a live preview swatch.
/// </summary>
[Tool("ColourConversion", ToolCategory.Alphabets,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords =
      [
          "colour", "color", "rgb", "hex", "hexadecimal", "cmyk", "cmy", "hsl", "hsv", "hsb", "hsi",
          "yiq", "yuv", "ycbcr", "convert", "swatch", "palette",
          "barva", "barvy", "převod", "prevod", "šestnáctkový", "odstín"
      ])]
public sealed partial class ColourConversionViewModel : ToolViewModelBase
{
    private const string Pct = "%";
    private const string Deg = "°";

    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    /// <summary>Guards the write-back so re-derived fields don't re-trigger the recompute cascade.</summary>
    private bool _suppress;

    // Hex is a single combined string, so it gets its own field (not a numeric channel).
    private readonly ColourField _hex = new("ColourConversionHexLabel");

    public ColourConversionViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("ColourConversion", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;

        Models = BuildModels();
        ApplyColour(new RgbColour(255, 128, 0)); // a non-trivial default so every field is populated
    }

    /// <summary>The hexadecimal field, surfaced separately so the View can give it a wider, full-width box.</summary>
    public ColourField HexField => _hex;

    /// <summary>The ten colour-model cards, in the reference site's order.</summary>
    public ObservableCollection<ColourModelCard> Models { get; }

    /// <summary>The current colour as an opaque <c>#AARRGGBB</c> string the View turns into a brush.</summary>
    [ObservableProperty]
    public partial string SwatchArgb { get; set; } = "#FFFF8000";

    /// <summary>The current colour as <c>#RRGGBB</c> (shown as a caption next to the swatch).</summary>
    [ObservableProperty]
    public partial string SwatchHex { get; set; } = "#FF8000";

    /// <summary><see langword="true"/> when the swatch colour is dark (so the View can pick a light label).</summary>
    [ObservableProperty]
    public partial bool IsSwatchDark { get; set; }

    private ObservableCollection<ColourModelCard> BuildModels()
    {
        ColourField r = new("ColourConversionR"), g = new("ColourConversionG"), b = new("ColourConversionB");
        ColourField cmyC = new("ColourConversionCyan", Pct), cmyM = new("ColourConversionMagenta", Pct), cmyY = new("ColourConversionYellow", Pct);
        ColourField ckC = new("ColourConversionCyan", Pct), ckM = new("ColourConversionMagenta", Pct), ckY = new("ColourConversionYellow", Pct), ckK = new("ColourConversionKey", Pct);
        ColourField hslH = new("ColourConversionHue", Deg), hslS = new("ColourConversionSaturation", Pct), hslL = new("ColourConversionLightness", Pct);
        ColourField hsvH = new("ColourConversionHue", Deg), hsvS = new("ColourConversionSaturation", Pct), hsvV = new("ColourConversionBrightness", Pct);
        ColourField hsiH = new("ColourConversionHue", Deg), hsiS = new("ColourConversionSaturation", Pct), hsiI = new("ColourConversionIntensity", Pct);
        ColourField yiqY = new("ColourConversionLuma", Pct), yiqI = new("ColourConversionChromaI", Pct), yiqQ = new("ColourConversionChromaQ", Pct);
        ColourField yuvY = new("ColourConversionLuma", Pct), yuvU = new("ColourConversionChromaU", Pct), yuvV = new("ColourConversionChromaV", Pct);
        ColourField ycY = new("ColourConversionLuma", Pct), ycCb = new("ColourConversionChromaCb", Pct), ycCr = new("ColourConversionChromaCr", Pct);

        ObservableCollection<ColourModelCard> models =
        [
            new("ColourConversionRgb", [r, g, b],
                () => TryInt(r.Value, out var rr) && TryInt(g.Value, out var gg) && TryInt(b.Value, out var bb)
                    ? new RgbColour(rr, gg, bb)
                    : null,
                c => { r.SetSilently(Int(c.R)); g.SetSilently(Int(c.G)); b.SetSilently(Int(c.B)); }),

            new("ColourConversionCmy", [cmyC, cmyM, cmyY],
                () => Three(cmyC, cmyM, cmyY, out var x, out var y, out var z) ? ColourConverter.FromCmy(new CmyColour(x, y, z)) : null,
                c => { var v = c.ToCmy(); cmyC.SetSilently(Fmt(v.C)); cmyM.SetSilently(Fmt(v.M)); cmyY.SetSilently(Fmt(v.Y)); }),

            new("ColourConversionCmyk", [ckC, ckM, ckY, ckK],
                () => Four(ckC, ckM, ckY, ckK, out var x, out var y, out var z, out var w) ? ColourConverter.FromCmyk(new CmykColour(x, y, z, w)) : null,
                c => { var v = c.ToCmyk(); ckC.SetSilently(Fmt(v.C)); ckM.SetSilently(Fmt(v.M)); ckY.SetSilently(Fmt(v.Y)); ckK.SetSilently(Fmt(v.K)); }),

            new("ColourConversionHsl", [hslH, hslS, hslL],
                () => Three(hslH, hslS, hslL, out var x, out var y, out var z) ? ColourConverter.FromHsl(new HslColour(x, y, z)) : null,
                c => { var v = c.ToHsl(); hslH.SetSilently(Fmt(v.H)); hslS.SetSilently(Fmt(v.S)); hslL.SetSilently(Fmt(v.L)); }),

            new("ColourConversionHsv", [hsvH, hsvS, hsvV],
                () => Three(hsvH, hsvS, hsvV, out var x, out var y, out var z) ? ColourConverter.FromHsv(new HsvColour(x, y, z)) : null,
                c => { var v = c.ToHsv(); hsvH.SetSilently(Fmt(v.H)); hsvS.SetSilently(Fmt(v.S)); hsvV.SetSilently(Fmt(v.V)); }),

            new("ColourConversionHsi", [hsiH, hsiS, hsiI],
                () => Three(hsiH, hsiS, hsiI, out var x, out var y, out var z) ? ColourConverter.FromHsi(new HsiColour(x, y, z)) : null,
                c => { var v = c.ToHsi(); hsiH.SetSilently(Fmt(v.H)); hsiS.SetSilently(Fmt(v.S)); hsiI.SetSilently(Fmt(v.I)); }),

            new("ColourConversionYiq", [yiqY, yiqI, yiqQ],
                () => Three(yiqY, yiqI, yiqQ, out var x, out var y, out var z) ? ColourConverter.FromYiq(new YiqColour(x, y, z)) : null,
                c => { var v = c.ToYiq(); yiqY.SetSilently(Fmt(v.Y)); yiqI.SetSilently(Fmt(v.I)); yiqQ.SetSilently(Fmt(v.Q)); }),

            new("ColourConversionYuv", [yuvY, yuvU, yuvV],
                () => Three(yuvY, yuvU, yuvV, out var x, out var y, out var z) ? ColourConverter.FromYuv(new YuvColour(x, y, z)) : null,
                c => { var v = c.ToYuv(); yuvY.SetSilently(Fmt(v.Y)); yuvU.SetSilently(Fmt(v.U)); yuvV.SetSilently(Fmt(v.V)); }),

            new("ColourConversionYCbCr", [ycY, ycCb, ycCr],
                () => Three(ycY, ycCb, ycCr, out var x, out var y, out var z) ? ColourConverter.FromYCbCr(new YCbCrColour(x, y, z)) : null,
                c => { var v = c.ToYCbCr(); ycY.SetSilently(Fmt(v.Y)); ycCb.SetSilently(Fmt(v.Cb)); ycCr.SetSilently(Fmt(v.Cr)); }),
        ];

        // Wire every field's edit to a recompute from its owning card.
        _hex.ValueEdited += () => RecomputeFrom(() => ColourConverter.TryParseHex(_hex.Value, out var c) ? c : null);
        foreach (var model in models)
        {
            foreach (var field in model.Fields)
            {
                var owner = model;
                field.ValueEdited += () => RecomputeFrom(owner.Parse);
            }
        }

        return models;
    }

    /// <summary>Parses the edited model; on success, makes it canonical and recomputes every field.</summary>
    private void RecomputeFrom(Func<RgbColour?> parse)
    {
        if (_suppress)
        {
            return;
        }

        if (parse() is { } colour)
        {
            ApplyColour(colour);
        }
    }

    /// <summary>
    /// Writes <paramref name="colour"/> into the hex field, every model card, and the swatch, under the
    /// suppression flag so the cascade of value-changed callbacks doesn't recurse.
    /// </summary>
    private void ApplyColour(RgbColour colour)
    {
        _suppress = true;
        try
        {
            SwatchHex = colour.ToHex();
            SwatchArgb = $"#FF{colour.R:X2}{colour.G:X2}{colour.B:X2}";
            IsSwatchDark = colour.ToHsl().L < 50.0;

            _hex.SetSilently(colour.ToHex());
            foreach (var model in Models)
            {
                model.Write(colour);
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    private static bool Three(ColourField a, ColourField b, ColourField c, out double x, out double y, out double z)
    {
        x = y = z = 0;
        return TryNum(a.Value, out x) && TryNum(b.Value, out y) && TryNum(c.Value, out z);
    }

    private static bool Four(ColourField a, ColourField b, ColourField c, ColourField d, out double x, out double y, out double z, out double w)
    {
        w = 0;
        return Three(a, b, c, out x, out y, out z) && TryNum(d.Value, out w);
    }

    private static bool TryInt(string s, out int value)
        => int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryNum(string s, out double value)
        => double.TryParse(s?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>One decimal place, invariant culture — matches the reference site's compact output.</summary>
    private static string Fmt(double v) => Math.Round(v, 1).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>The current colour across all ten models, one per line, for "Copy all" / share.</summary>
    private string BuildAllText()
    {
        var f = Models;
        string Row(int idx) => string.Join(", ", f[idx].Fields.Select(x => $"{x.Value}{x.Unit}"));

        var sb = new StringBuilder();
        sb.AppendLine($"Hex: {_hex.Value}");
        sb.AppendLine($"RGB: {Row(0)}");
        sb.AppendLine($"CMY: {Row(1)}");
        sb.AppendLine($"CMYK: {Row(2)}");
        sb.AppendLine($"HSL: {Row(3)}");
        sb.AppendLine($"HSV: {Row(4)}");
        sb.AppendLine($"HSI: {Row(5)}");
        sb.AppendLine($"YIQ: {Row(6)}");
        sb.AppendLine($"YUV: {Row(7)}");
        sb.Append($"YCbCr: {Row(8)}");
        return sb.ToString();
    }

    [RelayCommand]
    private void CopyHex() => _clipboard.SetText(_hex.Value);

    [RelayCommand]
    private void CopyAll() => _clipboard.SetText(BuildAllText());

    [RelayCommand]
    private async Task ShareAllAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildAllText());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }
}
