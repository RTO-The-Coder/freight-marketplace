using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests.ValueObjects;

public class CapacityTests
{
    [Fact]
    public void Create_ValidInput_SetsProperties()
    {
        var capacity = Capacity.Create(1000, 10);

        Assert.Equal(1000, capacity.WeightKg);
        Assert.Equal(10, capacity.VolumeCubicMeters);
    }

    [Fact]
    public void Create_ZeroWeightAndVolume_Succeeds()
    {
        var capacity = Capacity.Create(0, 0);

        Assert.Equal(0, capacity.WeightKg);
        Assert.Equal(0, capacity.VolumeCubicMeters);
    }

    [Fact]
    public void Create_NegativeWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Capacity.Create(-1, 10));
    }

    [Fact]
    public void Create_NegativeVolume_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Capacity.Create(1000, -1));
    }

    [Theory]
    [InlineData(TruckSize.Small, 2_800, 20)]
    [InlineData(TruckSize.Medium, 9_000, 45)]
    [InlineData(TruckSize.Large, 24_000, 90)]
    public void ForTruckSize_KnownSize_ReturnsExpectedFixedCapacity(TruckSize size, double expectedWeightKg, double expectedVolume)
    {
        var capacity = Capacity.ForTruckSize(size);

        Assert.Equal(expectedWeightKg, capacity.WeightKg);
        Assert.Equal(expectedVolume, capacity.VolumeCubicMeters);
    }

    [Fact]
    public void ForTruckSize_UnknownSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Capacity.ForTruckSize((TruckSize)999));
    }

    [Fact]
    public void Equality_SameWeightAndVolume_AreEqual()
    {
        var a = Capacity.Create(1000, 10);
        var b = Capacity.Create(1000, 10);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentWeight_AreNotEqual()
    {
        var a = Capacity.Create(1000, 10);
        var b = Capacity.Create(1001, 10);

        Assert.NotEqual(a, b);
    }
}
