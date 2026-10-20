using Freight.Domain.Client;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;

namespace Freight.Application.Fleet;

public sealed record AssignShipmentToTruckRequest(
    Guid TruckId,
    Guid ShipmentId,
    int PickupInsertIndex,
    int DeliveryInsertIndex,
    DateTime? TripStartTime = null);

public sealed record AssignShipmentToTruckResponse(int StopCount);

/// <summary>
/// The dry-run verdict, plus the route as it would look: <paramref name="Stops"/> are the
/// remaining (Pending) stops after the insertion in route order, Office last, each with its
/// forecast arrival (null when capacity rejected the insertion before any forecast ran);
/// <paramref name="AddedDistanceKm"/> is how much longer the trip gets.
/// </summary>
public sealed record ShipmentFeasibilityResponse(
    bool IsFeasible,
    Guid? ViolatingStopId,
    string? Reason,
    int TotalPlannedWaitTicks = 0,
    IReadOnlyList<ProposedStopDto>? Stops = null,
    double AddedDistanceKm = 0);

/// <param name="IsNew">One of the two stops this insertion adds.</param>
public sealed record ProposedStopDto(
    StopKind Kind,
    string PlaceName,
    DateTime? ProjectedArrival,
    bool IsNew);

/// <summary>
/// Assigns a booked shipment to a truck's route at caller-specified insertion points, using
/// <see cref="ShipmentInsertionPlanner"/> to measure the road legs the insertion creates,
/// preview it on a clone, and run the window/capacity feasibility check - and, only if
/// feasible, commits the real insertion (<see cref="Freight.Domain.Fleet.Trip.AssignShipment"/>),
/// its planned wait-for-window, and - for a new trip - the drivers' fresh compliance ledgers.
/// </summary>
public sealed class AssignShipmentToTruckHandler(IUnitOfWork unitOfWork, ShipmentInsertionPlanner insertionPlanner)
{
    private static readonly IReadOnlyDictionary<StopRef, int> EmptyWaits = new Dictionary<StopRef, int>();

    /// <summary>
    /// Runs the same route/window/capacity feasibility check <see cref="AssignShipmentAsync"/>
    /// does - loads the truck, finds or opens its trip, measures the road legs the
    /// insertion would create, previews it on a clone, and evaluates - but never commits.
    /// A dry run for the UI: "could this shipment go on this truck at these positions?"
    /// </summary>
    public async Task<ShipmentFeasibilityResponse> CheckFeasibilityAsync(
        AssignShipmentToTruckRequest request, CancellationToken cancellationToken = default)
    {
        var prepared = await insertionPlanner.PlanAsync(request, cancellationToken);
        var feasibility = prepared.Feasibility;
        var totalWaitTicks = feasibility.PlannedWaitTicks?.Values.Sum() ?? 0;
        var stops = await ProposedStopsAsync(prepared, cancellationToken);
        var addedDistanceKm = prepared.Preview.TotalPlannedDistanceKm - prepared.Trip.TotalPlannedDistanceKm;
        return new ShipmentFeasibilityResponse(
            feasibility.IsFeasible, feasibility.ViolatingStopId, feasibility.ViolationReason, totalWaitTicks,
            stops, addedDistanceKm);
    }

    /// <summary>The preview's Pending stops in route order with their place names and forecast arrivals.</summary>
    private async Task<IReadOnlyList<ProposedStopDto>> ProposedStopsAsync(
        PreparedInsertion prepared, CancellationToken cancellationToken)
    {
        var pending = prepared.Preview.Stops
            .Where(stop => stop.Status == StopStatus.Pending)
            .OrderBy(stop => stop.Sequence)
            .ToList();

        var otherShipmentIds = pending
            .Where(stop => stop.ShipmentId is { } id && id != prepared.Shipment.Id)
            .Select(stop => stop.ShipmentId!.Value)
            .Distinct()
            .ToList();
        var shipments = otherShipmentIds.Count == 0
            ? []
            : (await unitOfWork.Shipments.GetByIdsAsync(otherShipmentIds, cancellationToken)).ToDictionary(shipment => shipment.Id);
        shipments[prepared.Shipment.Id] = prepared.Shipment;

        var company = await unitOfWork.TruckingCompanies.GetByIdAsync(prepared.Trip.TruckingCompanyId, cancellationToken);
        var arrivals = prepared.Feasibility.ProjectedArrivals;

        return pending
            .Select(stop => new ProposedStopDto(
                stop.Kind,
                PlaceName(stop, shipments, company) ?? string.Empty,
                arrivals is not null && arrivals.TryGetValue(stop.Id, out var arrival) ? arrival : null,
                stop.ShipmentId == prepared.Shipment.Id))
            .ToList();
    }

    private static string? PlaceName(Stop stop, Dictionary<Guid, Shipment> shipments, TruckingCompany? company) =>
        stop.Kind switch
        {
            StopKind.Pickup when stop.ShipmentId is { } id && shipments.TryGetValue(id, out var shipment) => shipment.PickupPlaceName,
            StopKind.Delivery when stop.ShipmentId is { } id && shipments.TryGetValue(id, out var shipment) => shipment.DeliveryPlaceName,
            StopKind.Office => company?.OfficePlaceName,
            _ => null,
        };

    public async Task<AssignShipmentToTruckResponse> AssignShipmentAsync(AssignShipmentToTruckRequest request, CancellationToken cancellationToken = default)
    {
        var prepared = await insertionPlanner.PlanAsync(request, cancellationToken);

        if (!prepared.Feasibility.IsFeasible)
        {
            throw new InvalidOperationException(
                $"Cannot assign shipment '{prepared.Shipment.Id}' to {prepared.Truck.TruckName}: {prepared.Feasibility.ViolationReason}");
        }

        var (_, truck, shipment, trip, _, isNewTrip, legPlan, stopInputs) = prepared;

        if (isNewTrip)
        {
            unitOfWork.Trips.Add(trip);

            // A fresh trip: every assigned driver starts it fully rested, anchored at
            // the trip's planned departure. Adding to an EXISTING trip must not reset
            // ledgers - the drivers' accumulated hours carry through the trip.
            truck.BeginTripCompliance(trip.StartedAt);

            trip.RecordDriver(truck.DriverAssignment!.PrimaryDriver);
            if (truck.DriverAssignment.SecondaryDriver is { } secondary)
            {
                trip.RecordDriver(secondary);
            }

            // The truck's progress still describes the previous trip's finished
            // return-to-office leg - drop it so SyncProgressToNextStop starts a fresh leg
            // instead of banking that old leg into this trip's totals.
            truck.ClearRouteProgress();
        }

        var previousNextStopId = trip.NextStop?.Id;

        trip.AssignShipment(
            shipment.Id, stopInputs.ShipmentSize, stopInputs.PickupLocation, stopInputs.DeliveryLocation,
            stopInputs.OfficeLocation, request.PickupInsertIndex, request.DeliveryInsertIndex, legPlan);

        // Persist the wait-for-window the feasibility walk computed onto the real stops -
        // keyed by StopRef so it lands on the just-inserted stops (different ids than the
        // preview clone's) and on any downstream stop whose arrival shifted.
        trip.SetPlannedWaits(prepared.Feasibility.PlannedWaitTicks ?? EmptyWaits);

        truck.SyncProgressToNextStop(trip, previousNextStopId);

        shipment.AssignToCompany(truck.TruckingCompanyId!.Value);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssignShipmentToTruckResponse(trip.Stops.Count);
    }
}
