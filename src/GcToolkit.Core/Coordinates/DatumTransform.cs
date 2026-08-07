namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Shifts geographic coordinates between an arbitrary <see cref="Datum"/> and WGS84 using the datum's
/// Helmert 7-parameter transformation (3-parameter Molodensky when the rotations and scale are zero).
/// Coordinates are converted to geocentric cartesian on the source ellipsoid, the Helmert transform is
/// applied (position-vector convention), and the result is converted back to geographic on the target
/// ellipsoid. Heights are assumed zero (geocaching works in 2-D); the small height coupling this
/// introduces is well within the few-metre accuracy of the published datum parameters.
/// </summary>
public static class DatumTransform
{
    private const double ArcSecToRad = Math.PI / 180.0 / 3600.0;

    /// <summary>Converts a coordinate expressed on <paramref name="datum"/> to WGS84 latitude/longitude.</summary>
    public static GeoCoordinate ToWgs84(GeoCoordinate c, Datum datum)
    {
        if (datum.IsIdentity)
        {
            return c;
        }

        var (x, y, z) = ToCartesian(c, datum.Ellipsoid);
        var (x2, y2, z2) = Helmert(x, y, z, datum, inverse: false);
        return FromCartesian(x2, y2, z2, WgsEllipsoid);
    }

    /// <summary>Converts a WGS84 coordinate to latitude/longitude on <paramref name="datum"/>.</summary>
    public static GeoCoordinate FromWgs84(GeoCoordinate wgs84, Datum datum)
    {
        if (datum.IsIdentity)
        {
            return wgs84;
        }

        var (x, y, z) = ToCartesian(wgs84, WgsEllipsoid);
        var (x2, y2, z2) = Helmert(x, y, z, datum, inverse: true);
        return FromCartesian(x2, y2, z2, datum.Ellipsoid);
    }

    private static readonly Ellipsoid WgsEllipsoid = new("WGS 84", 6378137.0, 298.257223563);

    private static (double X, double Y, double Z) Helmert(double x, double y, double z, Datum d, bool inverse)
    {
        var rx = d.Rx * ArcSecToRad;
        var ry = d.Ry * ArcSecToRad;
        var rz = d.Rz * ArcSecToRad;
        var m = 1.0 + d.S / 1_000_000.0;

        // Published parameters run local -> WGS84.
        if (!inverse)
        {
            return (
                d.Dx + m * (x - rz * y + ry * z),
                d.Dy + m * (rz * x + y - rx * z),
                d.Dz + m * (-ry * x + rx * y + z));
        }

        // WGS84 -> local is the true inverse (undo translation, then scale, then rotate back). Merely
        // negating the parameters leaves a scale*translation residual of a couple of centimetres, which
        // is enough to flip the last metre digit of a grid reference on a round trip.
        var px = (x - d.Dx) / m;
        var py = (y - d.Dy) / m;
        var pz = (z - d.Dz) / m;
        return (
            px + rz * py - ry * pz,
            -rz * px + py + rx * pz,
            ry * px - rx * py + pz);
    }

    private static (double X, double Y, double Z) ToCartesian(GeoCoordinate c, Ellipsoid e)
    {
        var phi = Rad(c.Latitude);
        var lambda = Rad(c.Longitude);
        var e2 = e.E2;
        var nu = e.A / Math.Sqrt(1.0 - e2 * Math.Sin(phi) * Math.Sin(phi));

        var x = nu * Math.Cos(phi) * Math.Cos(lambda);
        var y = nu * Math.Cos(phi) * Math.Sin(lambda);
        var z = (1.0 - e2) * nu * Math.Sin(phi);
        return (x, y, z);
    }

    private static GeoCoordinate FromCartesian(double x, double y, double z, Ellipsoid e)
    {
        var e2 = e.E2;
        var p = Math.Sqrt(x * x + y * y);
        var phi = Math.Atan2(z, p * (1.0 - e2));

        double phiPrev;
        var iterations = 0;
        do
        {
            phiPrev = phi;
            var nu = e.A / Math.Sqrt(1.0 - e2 * Math.Sin(phi) * Math.Sin(phi));
            phi = Math.Atan2(z + e2 * nu * Math.Sin(phi), p);
        }
        while (Math.Abs(phi - phiPrev) > 1e-12 && ++iterations < 20);

        var lambda = Math.Atan2(y, x);
        return new GeoCoordinate(Deg(phi), Deg(lambda));
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180.0;

    private static double Deg(double radians) => radians * 180.0 / Math.PI;
}
