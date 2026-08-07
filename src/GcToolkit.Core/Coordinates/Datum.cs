namespace GcToolkit.Core.Coordinates;

/// <summary>
/// A geodetic datum: a reference <see cref="Ellipsoid"/> plus the Helmert transformation from this
/// datum to WGS84. <see cref="Dx"/>/<see cref="Dy"/>/<see cref="Dz"/> are the geocentric translation
/// (metres); <see cref="Rx"/>/<see cref="Ry"/>/<see cref="Rz"/> are rotations (arc-seconds, position-
/// vector convention) and <see cref="S"/> the scale (ppm) — all zero for a 3-parameter (Molodensky)
/// datum. The transform direction is <c>local -> WGS84</c>; <see cref="DatumTransform"/> applies the
/// inverse for the other way.
/// </summary>
public readonly record struct Datum(
    string Code,
    Ellipsoid Ellipsoid,
    double Dx,
    double Dy,
    double Dz,
    double Rx = 0.0,
    double Ry = 0.0,
    double Rz = 0.0,
    double S = 0.0)
{
    /// <summary><see langword="true"/> when the transform is the identity on the WGS84 ellipsoid.</summary>
    public bool IsIdentity =>
        Dx == 0.0 && Dy == 0.0 && Dz == 0.0 && Rx == 0.0 && Ry == 0.0 && Rz == 0.0 && S == 0.0
        && Ellipsoid.A == 6378137.0 && Ellipsoid.InvF == 298.257223563;
}
