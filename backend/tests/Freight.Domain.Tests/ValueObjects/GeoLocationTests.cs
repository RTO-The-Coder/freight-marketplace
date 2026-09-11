using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests.ValueObjects;

public class GeoLocationTests
{
    private static GeoLocation Berlin() => GeoLocation.Create(52.5200, 13.4050);
    private static GeoLocation Munich() => GeoLocation.Create(48.1351, 11.5820);

    [Theory]
    [InlineData(-90, 0)]
    [InlineData(90, 0)]
    [InlineData(0, -180)]
    [InlineData(0, 180)]
    public void Create_BoundaryLatLon_Succeeds(double latitude, double longitude)
    {
        var location = GeoLocation.Create(latitude, longitude);

        Assert.Equal(latitude, location.Latitude);
        Assert.Equal(longitude, location.Longitude);
    }

    [Theory]
    [InlineData(-90.0001, 0)]
    [InlineData(90.0001, 0)]
    public void Create_LatitudeOutOfRange_Throws(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeoLocation.Create(latitude, longitude));
    }

    [Theory]
    [InlineData(0, -180.0001)]
    [InlineData(0, 180.0001)]
    public void Create_LongitudeOutOfRange_Throws(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeoLocation.Create(latitude, longitude));
    }

    [Fact]
    public void DistanceTo_SamePoint_IsZero()
    {
        var berlin = Berlin();

        Assert.Equal(0, berlin.DistanceTo(berlin), precision: 6);
    }

    [Fact]
    public void DistanceTo_IsSymmetric()
    {
        var berlin = Berlin();
        var munich = Munich();

        Assert.Equal(berlin.DistanceTo(munich), munich.DistanceTo(berlin), precision: 6);
    }

    [Fact]
    public void DistanceTo_KnownCityPair_MatchesExpectedKilometers()
    {
        var berlin = Berlin();
        var munich = Munich();

        var distanceKm = berlin.DistanceTo(munich);

        Assert.InRange(distanceKm, 500, 508);
    }

    [Fact]
    public void DistanceTo_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Berlin().DistanceTo(null!));
    }

    [Fact]
    public void InterpolateTo_FractionZero_ReturnsOrigin()
    {
        var berlin = Berlin();

        var result = berlin.InterpolateTo(Munich(), 0);

        Assert.Equal(berlin, result);
    }

    [Fact]
    public void InterpolateTo_FractionOne_ReturnsTarget()
    {
        var munich = Munich();

        var result = Berlin().InterpolateTo(munich, 1);

        Assert.Equal(munich, result);
    }

    [Fact]
    public void InterpolateTo_FractionHalf_ReturnsMidpoint()
    {
        var berlin = Berlin();
        var munich = Munich();

        var result = berlin.InterpolateTo(munich, 0.5);

        Assert.Equal((berlin.Latitude + munich.Latitude) / 2, result.Latitude, precision: 9);
        Assert.Equal((berlin.Longitude + munich.Longitude) / 2, result.Longitude, precision: 9);
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(1.0001)]
    public void InterpolateTo_FractionOutOfRange_Throws(double fraction)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Berlin().InterpolateTo(Munich(), fraction));
    }

    [Fact]
    public void InterpolateTo_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Berlin().InterpolateTo(null!, 0.5));
    }
}
