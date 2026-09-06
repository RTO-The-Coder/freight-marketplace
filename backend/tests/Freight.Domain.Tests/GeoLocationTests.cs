using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class GeoLocationTests
{
    [Fact]
    public void DistanceTo_BerlinToMunich_ReturnsKnownApproximateDistance()
    {
        var berlin = GeoLocation.Create(52.5200, 13.4050);
        var munich = GeoLocation.Create(48.1351, 11.5820);

        var distanceKm = berlin.DistanceTo(munich);

        Assert.InRange(distanceKm, 500, 510);
    }

    [Fact]
    public void DistanceTo_SameCoordinate_ReturnsZero()
    {
        var point = GeoLocation.Create(52.5200, 13.4050);

        var distanceKm = point.DistanceTo(point);

        Assert.Equal(0, distanceKm, precision: 6);
    }

    [Fact]
    public void DistanceTo_IsSymmetric()
    {
        var hamburg = GeoLocation.Create(53.5511, 9.9937);
        var frankfurt = GeoLocation.Create(50.1109, 8.6821);

        var forward = hamburg.DistanceTo(frankfurt);
        var backward = frankfurt.DistanceTo(hamburg);

        Assert.Equal(forward, backward, precision: 9);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void Create_OutOfRangeCoordinates_Throws(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeoLocation.Create(latitude, longitude));
    }

    [Fact]
    public void InterpolateTo_ZeroFraction_ReturnsStart()
    {
        var start = GeoLocation.Create(52.0, 13.0);
        var end = GeoLocation.Create(48.0, 11.0);

        var point = start.InterpolateTo(end, 0);

        Assert.Equal(52.0, point.Latitude, precision: 9);
        Assert.Equal(13.0, point.Longitude, precision: 9);
    }

    [Fact]
    public void InterpolateTo_FullFraction_ReturnsEnd()
    {
        var start = GeoLocation.Create(52.0, 13.0);
        var end = GeoLocation.Create(48.0, 11.0);

        var point = start.InterpolateTo(end, 1);

        Assert.Equal(48.0, point.Latitude, precision: 9);
        Assert.Equal(11.0, point.Longitude, precision: 9);
    }

    [Fact]
    public void InterpolateTo_HalfFraction_ReturnsMidpoint()
    {
        var start = GeoLocation.Create(52.0, 13.0);
        var end = GeoLocation.Create(48.0, 11.0);

        var point = start.InterpolateTo(end, 0.5);

        Assert.Equal(50.0, point.Latitude, precision: 9);
        Assert.Equal(12.0, point.Longitude, precision: 9);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void InterpolateTo_FractionOutOfRange_Throws(double fraction)
    {
        var start = GeoLocation.Create(52.0, 13.0);
        var end = GeoLocation.Create(48.0, 11.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => start.InterpolateTo(end, fraction));
    }
}
