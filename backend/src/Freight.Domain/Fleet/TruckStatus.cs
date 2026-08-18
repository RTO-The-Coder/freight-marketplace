namespace Freight.Domain.Fleet;

/// <summary>
/// Operational state of a <see cref="Truck"/> - derived from the route and the current
/// driver assignment via <see cref="Truck.DetermineStatus"/>, never set directly. This
/// replaces the old free-set <c>MovementState</c>.
/// </summary>
public enum TruckStatus
{
    AtOffice,
    Running,
    Idle
}
