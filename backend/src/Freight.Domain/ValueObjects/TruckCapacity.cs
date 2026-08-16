namespace Freight.Domain.ValueObjects;

public sealed record TruckCapacity
{
    public Capacity Total { get; private set; } = null!;
    public Capacity Remaining { get; private set; } = null!;

    // EF Core cannot bind Total/Remaining through either constructor below (both are
    // owned-type navigations, and EF's constructor injection only binds scalar
    // properties) - this parameterless constructor exists solely so EF's materializer
    // can construct an instance and set the properties above via reflection.
    private TruckCapacity()
    {
    }

    private TruckCapacity(Capacity total, Capacity remaining)
    {
        Total = total;
        Remaining = remaining;
    }

    public TruckCapacity(Capacity total)
        : this(total, remaining: total)
    {
        ArgumentNullException.ThrowIfNull(total);
    }

    public TruckCapacity LoadCargo(Capacity cargo)
    {
        ArgumentNullException.ThrowIfNull(cargo);

        return new TruckCapacity(Total, Remaining.Subtract(cargo));
    }
}
