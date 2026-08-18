using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class TruckCapacityTests
{
    [Fact]
    public void Constructor_SetsTotal()
    {
        var total = Capacity.Create(1000, 20);

        var capacity = new TruckCapacity(total);

        Assert.Equal(total, capacity.Total);
    }

    [Fact]
    public void Constructor_NullTotal_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TruckCapacity(null!));
    }
}
