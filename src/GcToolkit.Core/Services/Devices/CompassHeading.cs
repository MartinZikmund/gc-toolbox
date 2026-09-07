using System;

namespace GcToolkit.Core.Services.Devices;

/// <summary>One compass reading, already normalised to 0..360.</summary>
/// <param name="MagneticNorthDegrees">Heading relative to magnetic north, 0..360.</param>
/// <param name="TrueNorthDegrees">Heading relative to true north, or <see langword="null"/> when the
/// platform cannot resolve it (Android without a granted ACCESS_FINE_LOCATION, most non-mobile heads).</param>
/// <param name="Timestamp">When the sensor reported the reading.</param>
public readonly record struct CompassHeading(
    double MagneticNorthDegrees,
    double? TrueNorthDegrees,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Builds a heading from a raw platform reading, or <see langword="null"/> when the magnetic heading
    /// is not a finite number — a stale driver emitting NaN must leave the last good bearing on screen
    /// rather than render "NaN°".
    /// </summary>
    /// <remarks>
    /// The platforms disagree on how they say "no true north", and none of them says it with a null:
    /// Uno's Android compass passes <see cref="double.NaN"/> when no <c>Geolocator</c> fix is available,
    /// its iOS compass forwards <c>CLHeading.TrueHeading</c>, which is -1 in the same situation, and only
    /// WinRT actually returns null. All three collapse to null here.
    /// </remarks>
    public static CompassHeading? TryCreate(double magneticNorthDegrees, double? trueNorthDegrees, DateTimeOffset timestamp)
    {
        if (!double.IsFinite(magneticNorthDegrees))
        {
            return null;
        }

        return new CompassHeading(
            Normalize(magneticNorthDegrees),
            trueNorthDegrees is { } trueNorth && double.IsFinite(trueNorth) && trueNorth >= 0d
                ? Normalize(trueNorth)
                : null,
            timestamp);
    }

    private static double Normalize(double degrees) => ((degrees % 360d) + 360d) % 360d;
}
