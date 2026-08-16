using Freight.Domain.Common;

namespace Freight.Domain.Tracking;

public sealed class RouteProgress : Entity
{
    public Guid TruckId { get; }
    public int CurrentLegIndex { get; internal set; }
    public int TicksElapsedInCurrentLeg { get; internal set; }

    public RouteProgress(Guid truckId, int currentLegIndex = 0, int ticksElapsedInCurrentLeg = 0)
    {
        if (truckId == Guid.Empty)
        {
            throw new ArgumentException("Truck id cannot be empty.", nameof(truckId));
        }

        if (currentLegIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentLegIndex), currentLegIndex,
                "Current leg index cannot be negative.");
        }

        if (ticksElapsedInCurrentLeg < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksElapsedInCurrentLeg), ticksElapsedInCurrentLeg,
                "Ticks elapsed in current leg cannot be negative.");
        }

        TruckId = truckId;
        CurrentLegIndex = currentLegIndex;
        TicksElapsedInCurrentLeg = ticksElapsedInCurrentLeg;
    }
}
