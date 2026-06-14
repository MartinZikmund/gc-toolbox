namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Distance, bearing, midpoint and projection on the WGS84 ellipsoid. Distances and the destination
/// point use Vincenty's iterative formulae (sub-millimetre accuracy), falling back to the spherical
/// (haversine / great-circle) solution when Vincenty fails to converge near antipodal points.
/// </summary>
public static class Geodesy
{
    private const double MeanRadius = 6_371_008.8; // IUGG mean Earth radius, for the spherical fallback
    private const int MaxIterations = 200;
    private const double ConvergenceTolerance = 1e-12;

    /// <summary>The ellipsoidal (geodesic) distance between two coordinates, in metres.</summary>
    public static double DistanceMeters(GeoCoordinate a, GeoCoordinate b)
        => TryVincentyInverse(a, b, out var distance, out _, out _) ? distance : HaversineMeters(a, b);

    /// <summary>The initial great-circle/geodesic bearing from <paramref name="a"/> to <paramref name="b"/>, in <c>[0, 360)</c> degrees.</summary>
    public static double InitialBearingDegrees(GeoCoordinate a, GeoCoordinate b)
        => TryVincentyInverse(a, b, out _, out var initial, out _) ? initial : SphericalInitialBearing(a, b);

    /// <summary>The final bearing arriving at <paramref name="b"/> when travelling from <paramref name="a"/>, in <c>[0, 360)</c> degrees.</summary>
    public static double FinalBearingDegrees(GeoCoordinate a, GeoCoordinate b)
    {
        if (TryVincentyInverse(a, b, out _, out _, out var final))
        {
            return final;
        }

        // Spherical fallback: the final bearing is the reverse of the initial bearing of the return leg.
        return Normalize360(SphericalInitialBearing(b, a) + 180.0);
    }

    /// <summary>The great-circle midpoint of two coordinates.</summary>
    public static GeoCoordinate Midpoint(GeoCoordinate a, GeoCoordinate b)
    {
        var phi1 = Rad(a.Latitude);
        var phi2 = Rad(b.Latitude);
        var lambda1 = Rad(a.Longitude);
        var deltaLambda = Rad(b.Longitude - a.Longitude);

        var bx = Math.Cos(phi2) * Math.Cos(deltaLambda);
        var by = Math.Cos(phi2) * Math.Sin(deltaLambda);

        var phiM = Math.Atan2(
            Math.Sin(phi1) + Math.Sin(phi2),
            Math.Sqrt((Math.Cos(phi1) + bx) * (Math.Cos(phi1) + bx) + by * by));
        var lambdaM = lambda1 + Math.Atan2(by, Math.Cos(phi1) + bx);

        return new GeoCoordinate(Deg(phiM), NormalizeLongitude(Deg(lambdaM)));
    }

    /// <summary>The coordinate reached by travelling <paramref name="distanceMeters"/> from
    /// <paramref name="start"/> along <paramref name="bearingDegrees"/> (Vincenty direct, spherical fallback).</summary>
    public static GeoCoordinate Destination(GeoCoordinate start, double distanceMeters, double bearingDegrees)
        => TryVincentyDirect(start, distanceMeters, bearingDegrees, out var destination)
            ? destination
            : SphericalDestination(start, distanceMeters, bearingDegrees);

    // ---- Vincenty inverse ----

    private static bool TryVincentyInverse(GeoCoordinate p1, GeoCoordinate p2, out double distance, out double initialBearing, out double finalBearing)
    {
        distance = 0.0;
        initialBearing = 0.0;
        finalBearing = 0.0;

        var a = Wgs84.A;
        var b = Wgs84.B;
        var f = Wgs84.F;

        var phi1 = Rad(p1.Latitude);
        var phi2 = Rad(p2.Latitude);
        var l = Rad(p2.Longitude - p1.Longitude);

        if (p1.Latitude == p2.Latitude && p1.Longitude == p2.Longitude)
        {
            return true; // coincident points: zero distance, zero bearings
        }

        var tanU1 = (1.0 - f) * Math.Tan(phi1);
        var cosU1 = 1.0 / Math.Sqrt(1.0 + tanU1 * tanU1);
        var sinU1 = tanU1 * cosU1;

        var tanU2 = (1.0 - f) * Math.Tan(phi2);
        var cosU2 = 1.0 / Math.Sqrt(1.0 + tanU2 * tanU2);
        var sinU2 = tanU2 * cosU2;

        var lambda = l;
        double sinLambda, cosLambda, sinSigma, cosSigma, sigma, cosSqAlpha, cos2SigmaM;
        var iterations = 0;

        do
        {
            sinLambda = Math.Sin(lambda);
            cosLambda = Math.Cos(lambda);

            var sinSigmaSq =
                cosU2 * sinLambda * (cosU2 * sinLambda) +
                (cosU1 * sinU2 - sinU1 * cosU2 * cosLambda) * (cosU1 * sinU2 - sinU1 * cosU2 * cosLambda);
            sinSigma = Math.Sqrt(sinSigmaSq);
            if (sinSigma == 0.0)
            {
                return true; // coincident
            }

            cosSigma = sinU1 * sinU2 + cosU1 * cosU2 * cosLambda;
            sigma = Math.Atan2(sinSigma, cosSigma);
            var sinAlpha = cosU1 * cosU2 * sinLambda / sinSigma;
            cosSqAlpha = 1.0 - sinAlpha * sinAlpha;
            cos2SigmaM = cosSqAlpha == 0.0 ? 0.0 : cosSigma - 2.0 * sinU1 * sinU2 / cosSqAlpha; // equatorial line
            var c = f / 16.0 * cosSqAlpha * (4.0 + f * (4.0 - 3.0 * cosSqAlpha));
            var lambdaPrev = lambda;
            lambda = l + (1.0 - c) * f * sinAlpha *
                (sigma + c * sinSigma * (cos2SigmaM + c * cosSigma * (-1.0 + 2.0 * cos2SigmaM * cos2SigmaM)));

            if (Math.Abs(lambda - lambdaPrev) < ConvergenceTolerance)
            {
                break;
            }
        }
        while (++iterations < MaxIterations);

        if (iterations >= MaxIterations || double.IsNaN(lambda))
        {
            return false; // failed to converge (near-antipodal) -> caller uses the spherical fallback
        }

        var uSq = cosSqAlpha * (a * a - b * b) / (b * b);
        var bigA = 1.0 + uSq / 16384.0 * (4096.0 + uSq * (-768.0 + uSq * (320.0 - 175.0 * uSq)));
        var bigB = uSq / 1024.0 * (256.0 + uSq * (-128.0 + uSq * (74.0 - 47.0 * uSq)));
        var deltaSigma = bigB * sinSigma * (cos2SigmaM + bigB / 4.0 *
            (cosSigma * (-1.0 + 2.0 * cos2SigmaM * cos2SigmaM) -
             bigB / 6.0 * cos2SigmaM * (-3.0 + 4.0 * sinSigma * sinSigma) * (-3.0 + 4.0 * cos2SigmaM * cos2SigmaM)));

        distance = b * bigA * (sigma - deltaSigma);
        initialBearing = Normalize360(Deg(Math.Atan2(cosU2 * sinLambda, cosU1 * sinU2 - sinU1 * cosU2 * cosLambda)));
        finalBearing = Normalize360(Deg(Math.Atan2(cosU1 * sinLambda, -sinU1 * cosU2 + cosU1 * sinU2 * cosLambda)));
        return true;
    }

    // ---- Vincenty direct ----

    private static bool TryVincentyDirect(GeoCoordinate start, double distance, double bearingDegrees, out GeoCoordinate destination)
    {
        destination = default;

        var a = Wgs84.A;
        var b = Wgs84.B;
        var f = Wgs84.F;

        var phi1 = Rad(start.Latitude);
        var alpha1 = Rad(bearingDegrees);

        var tanU1 = (1.0 - f) * Math.Tan(phi1);
        var cosU1 = 1.0 / Math.Sqrt(1.0 + tanU1 * tanU1);
        var sinU1 = tanU1 * cosU1;

        var sinAlpha1 = Math.Sin(alpha1);
        var cosAlpha1 = Math.Cos(alpha1);

        var sigma1 = Math.Atan2(tanU1, cosAlpha1);
        var sinAlpha = cosU1 * sinAlpha1;
        var cosSqAlpha = 1.0 - sinAlpha * sinAlpha;
        var uSq = cosSqAlpha * (a * a - b * b) / (b * b);
        var bigA = 1.0 + uSq / 16384.0 * (4096.0 + uSq * (-768.0 + uSq * (320.0 - 175.0 * uSq)));
        var bigB = uSq / 1024.0 * (256.0 + uSq * (-128.0 + uSq * (74.0 - 47.0 * uSq)));

        var sigma = distance / (b * bigA);
        double sinSigma, cosSigma, cos2SigmaM, sigmaPrev;
        var iterations = 0;

        do
        {
            cos2SigmaM = Math.Cos(2.0 * sigma1 + sigma);
            sinSigma = Math.Sin(sigma);
            cosSigma = Math.Cos(sigma);
            var deltaSigma = bigB * sinSigma * (cos2SigmaM + bigB / 4.0 *
                (cosSigma * (-1.0 + 2.0 * cos2SigmaM * cos2SigmaM) -
                 bigB / 6.0 * cos2SigmaM * (-3.0 + 4.0 * sinSigma * sinSigma) * (-3.0 + 4.0 * cos2SigmaM * cos2SigmaM)));
            sigmaPrev = sigma;
            sigma = distance / (b * bigA) + deltaSigma;
        }
        while (Math.Abs(sigma - sigmaPrev) > ConvergenceTolerance && ++iterations < MaxIterations);

        if (iterations >= MaxIterations || double.IsNaN(sigma))
        {
            return false;
        }

        var tmp = sinU1 * sinSigma - cosU1 * cosSigma * cosAlpha1;
        var phi2 = Math.Atan2(
            sinU1 * cosSigma + cosU1 * sinSigma * cosAlpha1,
            (1.0 - f) * Math.Sqrt(sinAlpha * sinAlpha + tmp * tmp));
        var lambda = Math.Atan2(sinSigma * sinAlpha1, cosU1 * cosSigma - sinU1 * sinSigma * cosAlpha1);
        var c = f / 16.0 * cosSqAlpha * (4.0 + f * (4.0 - 3.0 * cosSqAlpha));
        var l = lambda - (1.0 - c) * f * sinAlpha *
            (sigma + c * sinSigma * (cos2SigmaM + c * cosSigma * (-1.0 + 2.0 * cos2SigmaM * cos2SigmaM)));

        var lon2 = start.Longitude + Deg(l);
        destination = new GeoCoordinate(Deg(phi2), NormalizeLongitude(lon2));
        return true;
    }

    // ---- Spherical fallbacks ----

    private static double HaversineMeters(GeoCoordinate a, GeoCoordinate b)
    {
        var phi1 = Rad(a.Latitude);
        var phi2 = Rad(b.Latitude);
        var deltaPhi = Rad(b.Latitude - a.Latitude);
        var deltaLambda = Rad(b.Longitude - a.Longitude);

        var h = Math.Sin(deltaPhi / 2.0) * Math.Sin(deltaPhi / 2.0) +
                Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2.0) * Math.Sin(deltaLambda / 2.0);
        return MeanRadius * 2.0 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1.0 - h));
    }

    private static double SphericalInitialBearing(GeoCoordinate a, GeoCoordinate b)
    {
        var phi1 = Rad(a.Latitude);
        var phi2 = Rad(b.Latitude);
        var deltaLambda = Rad(b.Longitude - a.Longitude);

        var y = Math.Sin(deltaLambda) * Math.Cos(phi2);
        var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);
        return Normalize360(Deg(Math.Atan2(y, x)));
    }

    private static GeoCoordinate SphericalDestination(GeoCoordinate start, double distance, double bearingDegrees)
    {
        var delta = distance / MeanRadius;
        var theta = Rad(bearingDegrees);
        var phi1 = Rad(start.Latitude);
        var lambda1 = Rad(start.Longitude);

        var phi2 = Math.Asin(Math.Sin(phi1) * Math.Cos(delta) + Math.Cos(phi1) * Math.Sin(delta) * Math.Cos(theta));
        var lambda2 = lambda1 + Math.Atan2(
            Math.Sin(theta) * Math.Sin(delta) * Math.Cos(phi1),
            Math.Cos(delta) - Math.Sin(phi1) * Math.Sin(phi2));
        return new GeoCoordinate(Deg(phi2), NormalizeLongitude(Deg(lambda2)));
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180.0;

    private static double Deg(double radians) => radians * 180.0 / Math.PI;

    private static double Normalize360(double degrees) => (degrees % 360.0 + 360.0) % 360.0;

    private static double NormalizeLongitude(double lon) => (lon + 540.0) % 360.0 - 180.0;
}
