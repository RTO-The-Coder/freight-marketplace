using Freight.Domain.Common;
using Freight.Domain.Fleet;

namespace Freight.Application.Fleet;

public sealed record GetTruckEtasRequest(Guid TruckId);

public sealed record TruckEtaStopDto(
    Guid StopId,
    Guid? ShipmentId,
    StopKind Kind,
    StopStatus Status,
    int Sequence,
    DateTime? ReachedAt,
    DateTime? ProjectedArrival);

public sealed record TruckEtasDto(
    Guid TruckId,
    Guid? TripId,
    DateTime? ProjectionStart,
    IReadOnlyList<TruckEtaStopDto> Stops);

/// <summary>
/// Projected arrival time at every stop of a truck's current open trip, computed by
/// <see cref="RouteEtaCalculator"/> - the same forward route walk the shipment-insertion
/// feasibility check uses. Reached stops report their actual <c>ReachedAt</c> and no
/// projection; still-Pending stops report the projected arrival. A truck with no open
/// trip returns an empty stop list.
///
/// The projection runs from where the truck's primary-driver ledger currently stands
/// (its <c>LastEvaluatedSimulatedTime</c>), carrying the truck's live leg progress into
/// the first Pending leg.
/// </summary>
public sealed class GetTruckEtasHandler(IUnitOfWork unitOfWork, RouteEtaCalculator routeEtaCalculator)
{
    public async Task<TruckEtasDto> HandleAsync(GetTruckEtasRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
        if (trip is null)
        {
            return new TruckEtasDto(truck.Id, null, null, []);
        }

        if (truck.DriverAssignment is null)
        {
            throw new InvalidOperationException($"Truck '{truck.Id}' has an open trip but no driver assignment.");
        }

        var driver = truck.DriverAssignment.PrimaryDriver;
        var ledger = driver.ComplianceState
            ?? throw new InvalidOperationException($"Open trip '{trip.Id}' has no compliance ledger for its primary driver.");

        var projectionStart = ledger.LastEvaluatedSimulatedTime;

        var etas = routeEtaCalculator.CalculateEtas(
            trip, truck.CurrentProgress, ledger, driver.Rules, projectionStart);

        var stops = trip.Stops
            .Select(stop => new TruckEtaStopDto(
                stop.Id,
                stop.ShipmentId,
                stop.Kind,
                stop.Status,
                stop.Sequence,
                stop.ReachedAt,
                etas.TryGetValue(stop.Id, out var eta) ? eta : null))
            .ToList();

        return new TruckEtasDto(truck.Id, trip.Id, projectionStart, stops);
    }
}
