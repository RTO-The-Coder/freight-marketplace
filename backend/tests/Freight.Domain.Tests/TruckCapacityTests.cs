using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class TruckCapacityTests
{
    [Fact]
    public void Constructor_RemainingStartsEqualToTotal()
    {
        var total = new Capacity(1000, 20);

        var capacity = new TruckCapacity(total);

        Assert.Equal(total, capacity.Total);
        Assert.Equal(total, capacity.Remaining);
    }

    [Fact]
    public void LoadCargo_ReducesRemainingOnly()
    {
        var capacity = new TruckCapacity(new Capacity(1000, 20));

        var afterLoad = capacity.LoadCargo(new Capacity(300, 5));

        Assert.Equal(1000, afterLoad.Total.WeightKg);
        Assert.Equal(20, afterLoad.Total.VolumeCubicMeters);
        Assert.Equal(700, afterLoad.Remaining.WeightKg);
        Assert.Equal(15, afterLoad.Remaining.VolumeCubicMeters);
    }

    [Fact]
    public void LoadCargo_Twice_AccumulatesReduction()
    {
        var capacity = new TruckCapacity(new Capacity(1000, 20));

        var afterBothLoads = capacity.LoadCargo(new Capacity(300, 5)).LoadCargo(new Capacity(200, 5));

        Assert.Equal(500, afterBothLoads.Remaining.WeightKg);
        Assert.Equal(10, afterBothLoads.Remaining.VolumeCubicMeters);
    }

    [Fact]
    public void LoadCargo_MoreThanRemaining_Throws()
    {
        var capacity = new TruckCapacity(new Capacity(100, 5));

        Assert.Throws<InvalidOperationException>(() => capacity.LoadCargo(new Capacity(200, 1)));
    }

    [Fact]
    public void Constructor_NullTotal_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TruckCapacity(null!));
    }
}
