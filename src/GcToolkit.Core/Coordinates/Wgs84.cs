namespace GcToolkit.Core.Coordinates;

/// <summary>The WGS84 reference ellipsoid constants shared by the UTM projection and the geodesy routines.</summary>
internal static class Wgs84
{
    /// <summary>Semi-major axis (equatorial radius) in metres.</summary>
    public const double A = 6378137.0;

    /// <summary>Flattening, <c>1 / 298.257223563</c>.</summary>
    public const double F = 1.0 / 298.257223563;

    /// <summary>Semi-minor axis (polar radius) in metres.</summary>
    public const double B = A * (1.0 - F);

    /// <summary>First eccentricity squared, <c>e² = f(2 − f)</c>.</summary>
    public const double E2 = F * (2.0 - F);
}
