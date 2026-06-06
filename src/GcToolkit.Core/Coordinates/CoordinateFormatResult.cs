namespace GcToolkit.Core.Coordinates;

/// <summary>One coordinate rendered in a single <see cref="CoordinateFormat"/>: the format tag and its
/// formatted <see cref="Value"/>. The pure result the conversion tool turns into a copyable row.</summary>
public readonly record struct CoordinateFormatResult(CoordinateFormat Format, string Value);
