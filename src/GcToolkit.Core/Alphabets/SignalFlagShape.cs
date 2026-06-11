namespace GcToolkit.Core.Alphabets;

/// <summary>A point in flag-relative coordinates: the hoist (left) edge is x = 0, the flag height is 1.</summary>
public readonly record struct SignalFlagPoint(double X, double Y);

/// <summary>One solid-color shape of a flag design. Shapes are painted in declaration order.</summary>
public abstract record SignalFlagShape(SignalFlagColor Color);

/// <summary>A filled polygon in flag-relative coordinates.</summary>
public sealed record SignalFlagPolygon(SignalFlagColor Color, IReadOnlyList<SignalFlagPoint> Points) : SignalFlagShape(Color);

/// <summary>A filled circle in flag-relative coordinates.</summary>
public sealed record SignalFlagCircle(SignalFlagColor Color, double CenterX, double CenterY, double Radius) : SignalFlagShape(Color);
