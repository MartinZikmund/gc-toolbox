namespace GcToolkit.Core.Coordinates;

/// <summary>
/// A reference ellipsoid: its <see cref="Name"/>, semi-major axis <see cref="A"/> (metres) and inverse
/// flattening <see cref="InvF"/>. <see cref="B"/>, <see cref="F"/> and <see cref="E2"/> are derived.
/// </summary>
public readonly record struct Ellipsoid(string Name, double A, double InvF)
{
    /// <summary>Flattening, <c>1 / InvF</c>.</summary>
    public double F => 1.0 / InvF;

    /// <summary>Semi-minor axis (polar radius), in metres.</summary>
    public double B => A * (1.0 - F);

    /// <summary>First eccentricity squared, <c>e² = f(2 − f)</c>.</summary>
    public double E2 => F * (2.0 - F);
}
