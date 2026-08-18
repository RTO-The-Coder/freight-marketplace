using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class CapacityTests
{
    [Fact]
    public void CanAccommodate_WhenSufficientCapacity_ReturnsTrue()
    {
        var truckCapacity = Capacity.Create(weightKg: 1000, volumeCubicMeters: 20);
        var cargo = Capacity.Create(weightKg: 500, volumeCubicMeters: 10);

        Assert.True(truckCapacity.CanAccommodate(cargo));
    }

    [Fact]
    public void CanAccommodate_WhenInsufficientWeight_ReturnsFalse()
    {
        var truckCapacity = Capacity.Create(weightKg: 100, volumeCubicMeters: 20);
        var cargo = Capacity.Create(weightKg: 500, volumeCubicMeters: 10);

        Assert.False(truckCapacity.CanAccommodate(cargo));
    }

    [Fact]
    public void CanAccommodate_WhenInsufficientVolume_ReturnsFalse()
    {
        var truckCapacity = Capacity.Create(weightKg: 1000, volumeCubicMeters: 5);
        var cargo = Capacity.Create(weightKg: 500, volumeCubicMeters: 10);

        Assert.False(truckCapacity.CanAccommodate(cargo));
    }

    [Fact]
    public void CanAccommodate_SufficientWeightButInsufficientVolume_ReturnsFalse()
    {
        var truckCapacity = Capacity.Create(weightKg: 1000, volumeCubicMeters: 2);
        var cargo = Capacity.Create(weightKg: 500, volumeCubicMeters: 10);

        Assert.False(truckCapacity.CanAccommodate(cargo));
    }

    [Fact]
    public void CanAccommodate_SufficientVolumeButInsufficientWeight_ReturnsFalse()
    {
        var truckCapacity = Capacity.Create(weightKg: 100, volumeCubicMeters: 50);
        var cargo = Capacity.Create(weightKg: 500, volumeCubicMeters: 10);

        Assert.False(truckCapacity.CanAccommodate(cargo));
    }

    [Fact]
    public void Subtract_PartialLoad_ReturnsRemainingCapacity()
    {
        var truckCapacity = Capacity.Create(weightKg: 1000, volumeCubicMeters: 20);
        var existingLoad = Capacity.Create(weightKg: 300, volumeCubicMeters: 5);

        var remaining = truckCapacity.Subtract(existingLoad);

        Assert.Equal(700, remaining.WeightKg);
        Assert.Equal(15, remaining.VolumeCubicMeters);
    }

    [Fact]
    public void Subtract_MoreThanAvailable_Throws()
    {
        var truckCapacity = Capacity.Create(weightKg: 100, volumeCubicMeters: 5);
        var tooMuch = Capacity.Create(weightKg: 200, volumeCubicMeters: 1);

        Assert.Throws<InvalidOperationException>(() => truckCapacity.Subtract(tooMuch));
    }

    [Fact]
    public void Create_NegativeWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Capacity.Create(weightKg: -1, volumeCubicMeters: 5));
    }
}
