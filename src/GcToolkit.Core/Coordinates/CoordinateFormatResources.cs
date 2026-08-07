namespace GcToolkit.Core.Coordinates;

/// <summary>Maps a <see cref="CoordinateFormat"/> to the resource key for its short display label, so
/// the ViewModel and its tests share one source of truth for the per-format row headings.</summary>
public static class CoordinateFormatResources
{
    /// <summary>The <c>CoordConvFormat*</c> resource key naming <paramref name="format"/>.</summary>
    public static string LabelKey(CoordinateFormat format) => format switch
    {
        CoordinateFormat.DecimalDegrees => "CoordConvFormatDecimalDegrees",
        CoordinateFormat.DegreesDecimalMinutes => "CoordConvFormatDegreesDecimalMinutes",
        CoordinateFormat.DegreesMinutesSeconds => "CoordConvFormatDegreesMinutesSeconds",
        CoordinateFormat.Utm => "CoordConvFormatUtm",
        CoordinateFormat.Mgrs => "CoordConvFormatMgrs",
        CoordinateFormat.Usng => "CoordConvFormatUsng",
        CoordinateFormat.DutchRd => "CoordConvFormatDutchRd",
        CoordinateFormat.BritishGrid => "CoordConvFormatBritishGrid",
        _ => "CoordConvFormatDecimalDegrees",
    };
}
