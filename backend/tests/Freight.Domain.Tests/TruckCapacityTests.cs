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
    public void AssignShipment_ReducesRemainingOnly()
    {
        var capacity = new TruckCapacity(new Capacity(1000, 20));

        var afterAssign = capacity.AssignShipment(new Capacity(300, 5));

        Assert.Equal(1000, afterAssign.Total.WeightKg);
        Assert.Equal(20, afterAssign.Total.VolumeCubicMeters);
        Assert.Equal(700, afterAssign.Remaining.WeightKg);
        Assert.Equal(15, afterAssign.Remaining.VolumeCubicMeters);
    }

    [Fact]
    public void AssignShipment_Twice_AccumulatesReduction()
    {
        var capacity = new TruckCapacity(new Capacity(1000, 20));

        var afterBothAssignments = capacity.AssignShipment(new Capacity(300, 5)).AssignShipment(new Capacity(200, 5));

        Assert.Equal(500, afterBothAssignments.Remaining.WeightKg);
        Assert.Equal(10, afterBothAssignments.Remaining.VolumeCubicMeters);
    }

    [Fact]
    public void AssignShipment_MoreThanRemaining_Throws()
    {
        var capacity = new TruckCapacity(new Capacity(100, 5));

        Assert.Throws<InvalidOperationException>(() => capacity.AssignShipment(new Capacity(200, 1)));
    }

    [Fact]
    public void Constructor_NullTotal_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TruckCapacity(null!));
    }
}
