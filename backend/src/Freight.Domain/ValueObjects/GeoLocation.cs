namespace Freight.Domain.ValueObjects;

public sealed record GeoLocation
{
    private const double EarthRadiusKm = 6371.0;

    public double Latitude { get; }
    public double Longitude { get; }

    private GeoLocation(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static GeoLocation Create(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180 degrees.");
        }

        return new GeoLocation(latitude, longitude);
    }

    public double DistanceTo(GeoLocation other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var lat1 = DegreesToRadians(Latitude);
        var lat2 = DegreesToRadians(other.Latitude);
        var deltaLat = DegreesToRadians(other.Latitude - Latitude);
        var deltaLon = DegreesToRadians(other.Longitude - Longitude);

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2)
                  * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    /// <summary>
    /// The point <paramref name="fraction"/> of the way to <paramref name="other"/> by
    /// linear lat/lon interpolation - used to place a truck partway along a leg (the route
    /// model treats distance covered as tracking time elapsed, see <c>RouteProgress</c>).
    /// Not a road-following position: it only seeds a fresh OSRM call for the diverted leg,
    /// so straight-vs-road error is immaterial.
    /// </summary>
    public GeoLocation InterpolateTo(GeoLocation other, double fraction)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (fraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "Fraction must be between 0 and 1.");
        }

        return new GeoLocation(
            Latitude + (other.Latitude - Latitude) * fraction,
            Longitude + (other.Longitude - Longitude) * fraction);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
