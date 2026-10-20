using Freight.Domain.Client;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Fleet;

public sealed record GetTruckEtasRequest(Guid TruckId);

public sealed record TruckEtaStopDto(
    Guid StopId,
    Guid? ShipmentId,
    StopKind Kind,
    StopStatus Status,
    int Sequence,
    DateTime? ReachedAt,
    DateTime? ProjectedArrival,
    int WaitTimeTick,
    int WaitTimeTickElapsed);

public sealed record TruckEtasDto(
    Guid TruckId,
    Guid? TripId,
    DateTime? ProjectionStart,
    TruckStatus Status,
    WaitingInfo? Waiting,
    IReadOnlyList<TruckEtaStopDto> Stops);

/// <summary>
/// Projected arrival time at every stop of a truck's current open trip - see
/// <see cref="TruckEtaProjector"/> for how it is projected. Reached stops report their
/// actual <c>ReachedAt</c> and no projection; still-Pending stops report the projected
/// physical arrival and any planned/served wait. A truck with no open trip returns an
/// empty stop list.
/// </summary>
public sealed class GetTruckEtasHandler(IUnitOfWork unitOfWork, RouteEtaCalculator routeEtaCalculator)
{
    private readonly TruckEtaProjector projector = new(routeEtaCalculator);

    public async Task<TruckEtasDto> GetTruckEtasAsync(GetTruckEtasRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
        if (trip is null)
        {
            return new TruckEtasDto(truck.Id, null, null, truck.DetermineStatus(null), null, []);
        }

        var stopWindows = TruckEtaProjector.StopWindows(trip, await LoadPendingShipmentsAsync(trip, cancellationToken));
        var projection = projector.Project(truck, trip, stopWindows);

        var stops = trip.Stops
            .Select(stop => new TruckEtaStopDto(
                stop.Id,
                stop.ShipmentId,
                stop.Kind,
                stop.Status,
                stop.Sequence,
                stop.ReachedAt,
                projection.Etas.TryGetValue(stop.Id, out var eta) ? eta : null,
                stop.WaitTimeTick,
                stop.WaitTimeTickElapsed))
            .ToList();

        return new TruckEtasDto(truck.Id, trip.Id, projection.ProjectionStart, projection.Status, projection.Waiting, stops);
    }

    /// <summary>The shipments of the trip's Pending Pickup/Delivery stops, keyed by id.</summary>
    private async Task<IReadOnlyDictionary<Guid, Shipment>> LoadPendingShipmentsAsync(
        Trip trip, CancellationToken cancellationToken)
    {
        var shipments = new Dictionary<Guid, Shipment>();

        foreach (var stop in trip.Stops)
        {
            if (stop.Status != StopStatus.Pending || stop.ShipmentId is not { } shipmentId
                || shipments.ContainsKey(shipmentId))
            {
                continue;
            }

            shipments[shipmentId] = await unitOfWork.Shipments.GetByIdAsync(shipmentId, cancellationToken)
                ?? throw new InvalidOperationException($"Shipment '{shipmentId}' referenced by stop '{stop.Id}' was not found.");
        }

        return shipments;
    }
}
