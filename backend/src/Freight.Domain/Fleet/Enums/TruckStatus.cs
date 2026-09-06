namespace Freight.Domain.Fleet.Enums;

/// <summary>
/// Operational state of a <see cref="Truck"/> - derived from the route and driver
/// assignment via <see cref="Truck.DetermineStatus(Trip?)"/>, never set directly.
/// </summary>
public enum TruckStatus
{
    /// <summary>Parked at its office - no open trip, or heading back to base.</summary>
    AtOffice,

    /// <summary>Driving a leg toward its next stop.</summary>
    Running,

    /// <summary>Reached its next stop but that stop's time window has not opened yet - parked, waiting.</summary>
    Parked,

    /// <summary>No driver assignment.</summary>
    Idle
}
