using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Fleet;

/// <summary>Where a truck is now, and the stop it is heading to (null at the office).</summary>
public sealed record TruckLocation(GeoLocation Position, Guid? HeadingToStopId, double LegProgressFraction);

/// <summary>
/// "Where is this truck right now?" - shared by the truck's own position
/// (<see cref="GetTruckPositionHandler"/>) and the shipper's view of an in-transit shipment
/// riding on it. A truck with no open trip sits at its company's office. A truck on an open
/// trip is somewhere along the leg toward its next pending stop: the point
/// <see cref="RouteProgress.GetProgressFraction"/> of the way from the last stop it reached
/// (or the office, if it has not reached one yet) to that next stop. Straight-line
/// interpolation - the route model assumes distance covered tracks time elapsed; no OSRM call.
/// </summary>
public static class TruckPositionLocator
{
    public static TruckLocation Locate(Truck truck, Trip? trip, GeoLocation office)
    {
        var nextStop = trip?.NextStop;

        if (trip is null || nextStop is null || truck.CurrentProgress is null)
        {
            return new TruckLocation(office, null, 0);
        }

        var legStart = trip.Stops.LastOrDefault(stop => stop.Status == StopStatus.Reached)?.Location ?? office;
        var fraction = truck.CurrentProgress.GetProgressFraction();
        return new TruckLocation(legStart.InterpolateTo(nextStop.Location, fraction), nextStop.Id, fraction);
    }
}
