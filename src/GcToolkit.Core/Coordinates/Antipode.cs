namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Computes the antipode of a geographic coordinate — the point diametrically opposite on Earth's
/// surface, reached by a straight line through the planet's centre. The latitude hemisphere flips
/// (the magnitude is unchanged) and the longitude becomes <c>lon ± 180</c>, wrapped into
/// <c>[-180, 180]</c>. The operation is its own inverse: <c>Of(Of(c)) == c</c>.
/// </summary>
public static class Antipode
{
    /// <summary>The antipode of <paramref name="coordinate"/>.</summary>
    public static GeoCoordinate Of(GeoCoordinate coordinate)
        => new(-coordinate.Latitude, AntipodeLongitude(coordinate.Longitude));

    /// <summary>The antipodal longitude: shift by 180° and wrap into <c>(-180, 180]</c>.</summary>
    private static double AntipodeLongitude(double longitude)
    {
        // A west-of-meridian (negative) input gains 180; everything else loses 180. This keeps the
        // result in (-180, 180] and matches the geocachingtoolbox "lon' = 180 - |lon|" worked example.
        var shifted = longitude > 0.0 ? longitude - 180.0 : longitude + 180.0;

        // ±180 is its own antipode meridian; normalise the -180 edge to +180 so the hemisphere reads E.
        return shifted == -180.0 ? 180.0 : shifted;
    }
}
