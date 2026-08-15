using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class GeoCoordinateTests
{
    [Fact]
    public void DistanceTo_BerlinToMunich_ReturnsKnownApproximateDistance()
    {
        var berlin = new GeoCoordinate(52.5200, 13.4050);
        var munich = new GeoCoordinate(48.1351, 11.5820);

        var distanceKm = berlin.DistanceTo(munich);

        Assert.InRange(distanceKm, 500, 510);
    }

    [Fact]
    public void DistanceTo_SameCoordinate_ReturnsZero()
    {
        var point = new GeoCoordinate(52.5200, 13.4050);

        var distanceKm = point.DistanceTo(point);

        Assert.Equal(0, distanceKm, precision: 6);
    }

    [Fact]
    public void DistanceTo_IsSymmetric()
    {
        var hamburg = new GeoCoordinate(53.5511, 9.9937);
        var frankfurt = new GeoCoordinate(50.1109, 8.6821);

        var forward = hamburg.DistanceTo(frankfurt);
        var backward = frankfurt.DistanceTo(hamburg);

        Assert.Equal(forward, backward, precision: 9);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void Constructor_OutOfRangeCoordinates_Throws(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinate(latitude, longitude));
    }
}
