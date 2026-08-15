namespace Freight.Domain.ValueObjects;

public sealed record TruckCapacity
{
    public Capacity Total { get; }
    public Capacity Remaining { get; }

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
