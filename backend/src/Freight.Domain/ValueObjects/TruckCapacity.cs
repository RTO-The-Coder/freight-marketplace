namespace Freight.Domain.ValueObjects;

/// <summary>
/// A Truck's fixed capacity ceiling, derived from its <see cref="Fleet.TruckSize"/> at
/// creation. Remaining capacity is deliberately not stored here - per the domain model,
/// "remaining" must always be derived (from the route's Pickup stops still outstanding,
/// see <see cref="Fleet.Truck.RemainingCapacity"/>) rather than tracked as a
/// separately-mutated field, to avoid a duplicated, driftable number.
/// </summary>
public sealed record TruckCapacity
{
    public Capacity Total { get; private set; } = null!;

    // EF Core materializes owned types through a parameterless constructor and sets the
    // properties above via reflection.
    private TruckCapacity()
    {
    }

    public TruckCapacity(Capacity total)
    {
        ArgumentNullException.ThrowIfNull(total);

        Total = total;
    }
}
