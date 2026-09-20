using Freight.Domain.Common;
using Freight.Domain.Fleet.ValueObjects;

namespace Freight.Application.Fleet;

public sealed record AssignShipmentToTruckRequest(
    Guid TruckId,
    Guid ShipmentId,
    int PickupInsertIndex,
    int DeliveryInsertIndex,
    DateTime? TripStartTime = null);

public sealed record AssignShipmentToTruckResponse(int StopCount);

public sealed record ShipmentFeasibilityResponse(
    bool IsFeasible,
    Guid? ViolatingStopId,
    string? Reason,
    int TotalPlannedWaitTicks = 0);

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
        return new ShipmentFeasibilityResponse(
            feasibility.IsFeasible, feasibility.ViolatingStopId, feasibility.ViolationReason, totalWaitTicks);
    }

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
