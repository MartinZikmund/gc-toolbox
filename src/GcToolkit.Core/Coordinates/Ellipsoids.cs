namespace GcToolkit.Core.Coordinates;

/// <summary>
/// The reference ellipsoids used by the datum table (semi-major axis and inverse flattening from
/// NIMA TR8350.2 / EPSG). Shared by <see cref="DatumRegistry"/>.
/// </summary>
public static class Ellipsoids
{
    public static readonly Ellipsoid Airy1830 = new("Airy 1830", 6377563.396, 299.3249646);
    public static readonly Ellipsoid AustralianNational = new("Australian National", 6378160.0, 298.25);
    public static readonly Ellipsoid Bessel1841 = new("Bessel 1841", 6377397.155, 299.1528128);
    public static readonly Ellipsoid Clarke1866 = new("Clarke 1866", 6378206.4, 294.9786982);
    public static readonly Ellipsoid Clarke1880 = new("Clarke 1880", 6378249.145, 293.465);
    public static readonly Ellipsoid Everest1830 = new("Everest 1830", 6377276.345, 300.8017);
    public static readonly Ellipsoid Grs1980 = new("GRS 1980", 6378137.0, 298.257222101);
    public static readonly Ellipsoid Indonesian1974 = new("Indonesian 1974", 6378160.0, 298.247);
    public static readonly Ellipsoid International1924 = new("International 1924", 6378388.0, 297.0);
    public static readonly Ellipsoid Krassovsky1940 = new("Krassovsky 1940", 6378245.0, 298.3);
    public static readonly Ellipsoid ModifiedAiry = new("Airy 1830 Modified", 6377340.189, 299.3249646);
    public static readonly Ellipsoid ModifiedFischer1960 = new("Modified Fischer 1960", 6378155.0, 298.3);
    public static readonly Ellipsoid SAm1969 = new("South American 1969", 6378160.0, 298.25);
    public static readonly Ellipsoid Wgs72 = new("WGS 72", 6378135.0, 298.26);
    public static readonly Ellipsoid Wgs84 = new("WGS 84", 6378137.0, 298.257223563);
}
